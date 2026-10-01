using System.Net.Sockets;
using Commerce.Infrastructure.Configuration;
using Microsoft.Extensions.Configuration;

namespace Commerce.Tests.Integration.Infrastructure;

/// <summary>
/// Configuração dos testes de integração. Usa as mesmas credenciais das aplicações (.env ou variáveis de ambiente),
/// mas em um virtual host exclusivo, criado e removido pelos testes: os dados da demonstração não são tocados.
/// </summary>
public static class RabbitMqTestEnvironment
{
    public const string VirtualHost = "commerce-integration-tests";

    private static readonly Lazy<string?> UnavailableReason = new(CheckAvailability);

    public static RabbitMqOptions Options { get; } = LoadOptions();

    public static Uri ManagementUri { get; } = new(
        Environment.GetEnvironmentVariable("RabbitMqManagement__Url") ?? $"http://{Options.Host}:15672/");

    public static bool IsAvailable(out string reason)
    {
        reason = UnavailableReason.Value ?? string.Empty;
        return UnavailableReason.Value is null;
    }

    private static RabbitMqOptions LoadOptions()
    {
        DotEnvFile.Load();

        var configuration = new ConfigurationBuilder()
            .AddEnvironmentVariables()
            .Build();

        var options = configuration.GetSection(RabbitMqOptions.SectionName).Get<RabbitMqOptions>() ?? new RabbitMqOptions();
        options.VirtualHost = VirtualHost;
        options.ClientName = "Commerce.Tests";
        options.ReconnectDelay = TimeSpan.FromSeconds(1);
        return options;
    }

    private static string? CheckAvailability()
    {
        if (string.IsNullOrWhiteSpace(Options.UserName) || string.IsNullOrWhiteSpace(Options.Password))
            return "Credenciais do RabbitMQ não configuradas (arquivo .env ou RabbitMq__UserName/RabbitMq__Password).";

        if (!CanConnect(Options.Host, Options.Port) || !CanConnect(ManagementUri.Host, ManagementUri.Port))
            return $"RabbitMQ indisponível em {Options.Host}:{Options.Port} / {ManagementUri}. " +
                   "Execute 'docker compose up -d' para rodar os testes de integração.";

        return null;
    }

    private static bool CanConnect(string host, int port)
    {
        try
        {
            using var client = new TcpClient();
            return client.ConnectAsync(host, port).Wait(TimeSpan.FromSeconds(2)) && client.Connected;
        }
        catch (Exception)
        {
            return false;
        }
    }
}
