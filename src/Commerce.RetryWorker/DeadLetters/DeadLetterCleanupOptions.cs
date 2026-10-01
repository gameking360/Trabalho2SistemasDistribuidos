namespace Commerce.RetryWorker.DeadLetters;

public sealed class DeadLetterCleanupOptions
{
    public const string SectionName = "DeadLetterCleanup";

    public bool Enabled { get; set; } = true;

    /// <summary>Intervalo entre as execuções da limpeza.</summary>
    public TimeSpan Interval { get; set; } = TimeSpan.FromHours(24);

    /// <summary>Somente mensagens isoladas há mais tempo que este período são removidas.</summary>
    public TimeSpan RetentionPeriod { get; set; } = TimeSpan.FromDays(30);
}
