namespace Commerce.Infrastructure.Configuration;

/// <summary>
/// TLS é habilitado apenas por configuração (RabbitMq__Tls__Enabled=true + porta 5671 no broker).
/// </summary>
public sealed class RabbitMqTlsOptions
{
    public bool Enabled { get; set; }

    /// <summary>Nome esperado no certificado do servidor. Quando vazio, usa o Host.</summary>
    public string? ServerName { get; set; }
}
