using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Commerce.Infrastructure.Configuration;
using Commerce.Infrastructure.Connection;
using Commerce.Infrastructure.Publishing;
using Commerce.Infrastructure.Topology;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using RabbitMQ.Client;
using MsOptions = Microsoft.Extensions.Options.Options;

namespace Commerce.Tests.Integration.Infrastructure;

/// <summary>
/// Prepara um virtual host isolado no RabbitMQ (via API do Management), oferece utilitários para inspecionar
/// as filas e sobe os workers reais dentro do processo de teste.
/// </summary>
public sealed class RabbitMqFixture : IAsyncLifetime
{
    private readonly HttpClient _management = new();
    private RabbitMqConnectionProvider? _connectionProvider;
    private RabbitMqMessagePublisher? _publisher;
    private IConnection? _connection;

    public RabbitMqOptions Options => RabbitMqTestEnvironment.Options;

    public IMessagePublisher Publisher => _publisher ?? throw NotInitialized();

    public IRabbitMqConnectionProvider ConnectionProvider => _connectionProvider ?? throw NotInitialized();

    public async Task InitializeAsync()
    {
        if (!RabbitMqTestEnvironment.IsAvailable(out _))
            return;

        _management.BaseAddress = new Uri(RabbitMqTestEnvironment.ManagementUri, "api/");
        _management.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic",
            Convert.ToBase64String(Encoding.UTF8.GetBytes($"{Options.UserName}:{Options.Password}")));

        await DeleteVirtualHostAsync();
        (await _management.PutAsJsonAsync(VirtualHostPath, new { description = "Testes de integração (removido ao final)" }))
            .EnsureSuccessStatusCode();
        (await _management.PutAsJsonAsync($"permissions/{VirtualHostSegment}/{Uri.EscapeDataString(Options.UserName)}",
            new { configure = ".*", write = ".*", read = ".*" })).EnsureSuccessStatusCode();

        _connection = await RabbitMqConnectionProvider.CreateFactory(Options).CreateConnectionAsync();
        await using (var channel = await _connection.CreateChannelAsync())
        {
            await MessagingTopology.DeclareAsync(channel);
        }

        _connectionProvider = new RabbitMqConnectionProvider(MsOptions.Create(Options), NullLogger<RabbitMqConnectionProvider>.Instance);
        _publisher = new RabbitMqMessagePublisher(_connectionProvider, MsOptions.Create(Options));
    }

    public async Task DisposeAsync()
    {
        if (_publisher is not null)
            await _publisher.DisposeAsync();

        if (_connectionProvider is not null)
            await _connectionProvider.DisposeAsync();

        if (_connection is not null)
        {
            await _connection.CloseAsync();
            await _connection.DisposeAsync();
        }

        if (_management.BaseAddress is not null)
            await DeleteVirtualHostAsync();

        _management.Dispose();
    }

    public async Task PurgeQueuesAsync()
    {
        await using var channel = await Connection.CreateChannelAsync();
        foreach (var queue in MessagingTopology.QueueDefinitions)
            await channel.QueuePurgeAsync(queue.Name);
    }

    /// <summary>Sobe um worker com o mesmo registro de serviços do Program.cs, apontando para o vhost de testes.</summary>
    public async Task<WorkerHost> StartWorkerAsync(
        Action<IServiceCollection, IConfiguration> registerServices,
        IReadOnlyDictionary<string, string?>? settings = null)
    {
        var builder = Host.CreateEmptyApplicationBuilder(new HostApplicationBuilderSettings());
        builder.Configuration.AddInMemoryCollection(DefaultSettings());
        if (settings is not null)
            builder.Configuration.AddInMemoryCollection(settings);

        if (Environment.GetEnvironmentVariable("COMMERCE_TESTS_LOGS") == "1")
            builder.Logging.AddSimpleConsole(console => console.SingleLine = true);

        registerServices(builder.Services, builder.Configuration);

        var host = builder.Build();
        await host.StartAsync();
        return new WorkerHost(host);
    }

    public async Task<uint> CountMessagesAsync(string queue)
    {
        await using var channel = await Connection.CreateChannelAsync();
        return (await channel.QueueDeclarePassiveAsync(queue)).MessageCount;
    }

    /// <summary>Retira da fila a primeira mensagem que satisfaz o filtro, aguardando até o tempo limite.</summary>
    public async Task<BasicGetResult> TakeMessageAsync(string queue, TimeSpan timeout, Func<BasicGetResult, bool>? match = null)
    {
        var deadline = DateTime.UtcNow + timeout;
        await using var channel = await Connection.CreateChannelAsync();

        while (DateTime.UtcNow < deadline)
        {
            var message = await channel.BasicGetAsync(queue, autoAck: false);
            if (message is null)
            {
                await Task.Delay(TimeSpan.FromMilliseconds(100));
                continue;
            }

            if (match is null || match(message))
            {
                await channel.BasicAckAsync(message.DeliveryTag, multiple: false);
                return message;
            }

            // Mensagens que não interessam ficam sem ack e voltam para a fila quando o canal é fechado.
        }

        throw new TimeoutException($"Nenhuma mensagem esperada chegou em {queue} em {timeout.TotalSeconds}s.");
    }

    /// <summary>Lê a primeira mensagem da fila sem removê-la.</summary>
    public async Task<BasicGetResult> PeekMessageAsync(string queue, TimeSpan timeout)
    {
        var deadline = DateTime.UtcNow + timeout;
        await using var channel = await Connection.CreateChannelAsync();

        while (DateTime.UtcNow < deadline)
        {
            var message = await channel.BasicGetAsync(queue, autoAck: false);
            if (message is not null)
            {
                await channel.BasicNackAsync(message.DeliveryTag, multiple: false, requeue: true);
                return message;
            }

            await Task.Delay(TimeSpan.FromMilliseconds(100));
        }

        throw new TimeoutException($"Nenhuma mensagem chegou em {queue} em {timeout.TotalSeconds}s.");
    }

    public static async Task WaitUntilAsync(Func<bool> condition, TimeSpan timeout, string description)
    {
        var deadline = DateTime.UtcNow + timeout;
        while (!condition())
        {
            if (DateTime.UtcNow > deadline)
                throw new TimeoutException($"Tempo esgotado aguardando: {description}.");
            await Task.Delay(TimeSpan.FromMilliseconds(100));
        }
    }

    /// <summary>Metadados da fila pela API do Management (durável, argumentos etc.).</summary>
    public async Task<JsonElement> GetQueueInfoAsync(string queue) =>
        await _management.GetFromJsonAsync<JsonElement>($"queues/{VirtualHostSegment}/{Uri.EscapeDataString(queue)}");

    /// <summary>Canal com publisher confirms para preparar cenários diretamente no broker.</summary>
    public async Task<IChannel> OpenChannelAsync() =>
        await Connection.CreateChannelAsync(new CreateChannelOptions(
            publisherConfirmationsEnabled: true, publisherConfirmationTrackingEnabled: true));

    private Dictionary<string, string?> DefaultSettings() => new()
    {
        ["RabbitMq:Host"] = Options.Host,
        ["RabbitMq:Port"] = Options.Port.ToString(),
        ["RabbitMq:VirtualHost"] = Options.VirtualHost,
        ["RabbitMq:UserName"] = Options.UserName,
        ["RabbitMq:Password"] = Options.Password,
        ["RabbitMq:ClientName"] = Options.ClientName,
        ["RabbitMq:ReconnectDelay"] = "00:00:01",
        // Mesmo comportamento do retry (2s, 4s, 6s...), em escala de 100 ms para o teste ser rápido.
        ["Retry:DelayIncrement"] = "00:00:00.100",
        ["Stock:SimulatedProcessingTime"] = "00:00:00",
        ["FailureSimulation:Enabled"] = "true"
    };

    private IConnection Connection => _connection ?? throw NotInitialized();

    private static string VirtualHostSegment => Uri.EscapeDataString(RabbitMqTestEnvironment.VirtualHost);

    private static string VirtualHostPath => $"vhosts/{VirtualHostSegment}";

    private async Task DeleteVirtualHostAsync()
    {
        var response = await _management.DeleteAsync(VirtualHostPath);
        if (response.StatusCode != HttpStatusCode.NotFound)
            response.EnsureSuccessStatusCode();
    }

    private static InvalidOperationException NotInitialized() =>
        new("RabbitMQ não inicializado para os testes de integração.");
}
