using RabbitMQ.Client;

namespace Commerce.Infrastructure.Connection;

/// <summary>
/// Fornece a conexão única do processo com o RabbitMQ (conexões são caras; canais são criados a partir dela).
/// </summary>
public interface IRabbitMqConnectionProvider
{
    Task<IConnection> GetConnectionAsync(CancellationToken cancellationToken = default);
}
