using Microsoft.Extensions.Options;

namespace Commerce.Infrastructure.Retry;

/// <summary>
/// Regras do retry com atraso progressivo:
/// falha 1 → 2s, falha 2 → 4s, falha 3 → 6s, falha 4 → 8s, falha 5 → 10s, falha 6 → isolamento na DLQ.
/// </summary>
public sealed class RetryPolicy(IOptions<RetryOptions> options)
{
    public int MaxAttempts => options.Value.MaxAttempts;

    /// <summary>Espera antes da próxima tentativa: número da falha × incremento configurado.</summary>
    public TimeSpan GetDelay(int failedAttempt)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(failedAttempt, 1);
        return options.Value.DelayIncrement * failedAttempt;
    }

    /// <summary>A falha da última tentativa permitida retira a mensagem do fluxo normal.</summary>
    public bool ShouldDeadLetter(int failedAttempt) => failedAttempt >= MaxAttempts;
}
