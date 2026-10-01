namespace Commerce.RetryWorker.DeadLetters;

public sealed class DeadLetterCleanupOptions
{
    public const string SectionName = "DeadLetterCleanup";

    /// <summary>
    /// Desabilitado por padrão: durante a execução normal nenhuma mensagem é removida da DLQ, que fica
    /// disponível para análise manual. Habilite apenas como rotina de manutenção.
    /// </summary>
    public bool Enabled { get; set; }

    /// <summary>Intervalo entre as execuções da limpeza.</summary>
    public TimeSpan Interval { get; set; } = TimeSpan.FromHours(24);

    /// <summary>Somente mensagens isoladas há mais tempo que este período são removidas.</summary>
    public TimeSpan RetentionPeriod { get; set; } = TimeSpan.FromDays(30);
}
