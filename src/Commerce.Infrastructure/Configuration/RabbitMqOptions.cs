namespace Commerce.Infrastructure.Configuration;

/// <summary>
/// Conexão com o RabbitMQ. Host, porta e virtual host ficam no appsettings; usuário e senha devem vir de
/// variáveis de ambiente (RabbitMq__UserName / RabbitMq__Password) e nunca do código ou do Git.
/// </summary>
public sealed class RabbitMqOptions
{
    public const string SectionName = "RabbitMq";

    public string Host { get; set; } = "localhost";

    public int Port { get; set; } = 5672;

    public string VirtualHost { get; set; } = "commerce";

    public string UserName { get; set; } = string.Empty;

    public string Password { get; set; } = string.Empty;

    /// <summary>Nome exibido na aba Connections do RabbitMQ Management (padrão: nome do projeto).</summary>
    public string ClientName { get; set; } = string.Empty;

    /// <summary>Tempo máximo de espera pelo publisher confirm do broker.</summary>
    public TimeSpan PublishConfirmTimeout { get; set; } = TimeSpan.FromSeconds(5);

    /// <summary>Intervalo entre novas tentativas de conexão e de reenvio quando o broker está indisponível.</summary>
    public TimeSpan ReconnectDelay { get; set; } = TimeSpan.FromSeconds(5);

    public RabbitMqTlsOptions Tls { get; set; } = new();
}
