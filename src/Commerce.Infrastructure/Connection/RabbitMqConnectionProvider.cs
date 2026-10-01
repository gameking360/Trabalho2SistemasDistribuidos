using Commerce.Infrastructure.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RabbitMQ.Client;

namespace Commerce.Infrastructure.Connection;

public sealed class RabbitMqConnectionProvider(
    IOptions<RabbitMqOptions> options,
    ILogger<RabbitMqConnectionProvider> logger) : IRabbitMqConnectionProvider, IAsyncDisposable
{
    private readonly ConnectionFactory _factory = CreateFactory(options.Value);
    private readonly SemaphoreSlim _lock = new(1, 1);
    private IConnection? _connection;

    public async Task<IConnection> GetConnectionAsync(CancellationToken cancellationToken = default)
    {
        // Quedas posteriores são tratadas pela recuperação automática do client.
        if (_connection is not null)
            return _connection;

        await _lock.WaitAsync(cancellationToken);
        try
        {
            if (_connection is not null)
                return _connection;

            var connection = await _factory.CreateConnectionAsync(cancellationToken);
            ObserveConnection(connection);
            _connection = connection;

            logger.LogInformation("Conectado ao RabbitMQ em {Host}:{Port} (vhost '{VirtualHost}', TLS {TlsEnabled})",
                _factory.HostName, _factory.Port, _factory.VirtualHost, _factory.Ssl.Enabled);

            return connection;
        }
        finally
        {
            _lock.Release();
        }
    }

    public static ConnectionFactory CreateFactory(RabbitMqOptions options) => new()
    {
        HostName = options.Host,
        Port = options.Port,
        VirtualHost = options.VirtualHost,
        UserName = options.UserName,
        Password = options.Password,
        ClientProvidedName = options.ClientName,
        AutomaticRecoveryEnabled = true,
        TopologyRecoveryEnabled = true,
        NetworkRecoveryInterval = options.ReconnectDelay,
        Ssl = new SslOption
        {
            Enabled = options.Tls.Enabled,
            ServerName = string.IsNullOrWhiteSpace(options.Tls.ServerName) ? options.Host : options.Tls.ServerName
        }
    };

    private void ObserveConnection(IConnection connection)
    {
        connection.ConnectionShutdownAsync += (_, args) =>
        {
            if (args.Initiator != ShutdownInitiator.Application)
                logger.LogWarning("Conexão com o RabbitMQ perdida ({Reason}). Reconexão automática a cada {DelaySeconds}s",
                    args.ReplyText, _factory.NetworkRecoveryInterval.TotalSeconds);
            return Task.CompletedTask;
        };

        connection.RecoverySucceededAsync += (_, _) =>
        {
            logger.LogInformation("Conexão com o RabbitMQ restabelecida; canais e consumidores recuperados");
            return Task.CompletedTask;
        };

        connection.ConnectionRecoveryErrorAsync += (_, args) =>
        {
            logger.LogWarning("Falha ao reconectar no RabbitMQ: {Error}", args.Exception.Message);
            return Task.CompletedTask;
        };
    }

    public async ValueTask DisposeAsync()
    {
        if (_connection is not null)
        {
            if (_connection.IsOpen)
                await _connection.CloseAsync();

            await _connection.DisposeAsync();
        }

        _lock.Dispose();
    }
}
