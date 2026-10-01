namespace Commerce.Infrastructure.Retry;

public sealed class RetryOptions
{
    public const string SectionName = "Retry";

    /// <summary>Número máximo de tentativas de processamento (a última falha isola a mensagem na DLQ).</summary>
    public int MaxAttempts { get; set; } = 6;

    /// <summary>Acréscimo de espera a cada nova falha (2s → 2s, 4s, 6s, 8s, 10s).</summary>
    public TimeSpan DelayIncrement { get; set; } = TimeSpan.FromSeconds(2);
}
