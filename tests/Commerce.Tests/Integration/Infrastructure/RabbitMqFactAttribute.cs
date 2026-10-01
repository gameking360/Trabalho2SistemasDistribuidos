namespace Commerce.Tests.Integration.Infrastructure;

/// <summary>
/// Teste que depende de um RabbitMQ real; é ignorado (Skip) quando o broker não está acessível.
/// </summary>
public sealed class RabbitMqFactAttribute : FactAttribute
{
    public RabbitMqFactAttribute()
    {
        if (!RabbitMqTestEnvironment.IsAvailable(out var reason))
            Skip = reason;
    }
}
