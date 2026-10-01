using Microsoft.Extensions.Options;

namespace Commerce.Infrastructure.Simulation;

/// <summary>
/// Gatilho de falhas usado apenas para demonstrar o retry e a DLQ, sem inventar regras de negócio:
/// um código de item (estoque) ou destinatário (notificação) igual a um dos marcadores provoca a falha.
/// </summary>
public sealed class FailureSimulator(IOptions<FailureSimulationOptions> options)
{
    /// <summary>Falha nas primeiras tentativas e é processada com sucesso em seguida.</summary>
    public const string TemporaryFailureMarker = "FALHA-TEMPORARIA";

    /// <summary>Falha em todas as tentativas e termina na DLQ.</summary>
    public const string PermanentFailureMarker = "FALHA-PERMANENTE";

    public void ThrowIfRequested(IEnumerable<string?> values, int attempt)
    {
        if (!options.Value.Enabled)
            return;

        var markers = values
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Select(value => value!.Trim())
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        if (markers.Contains(PermanentFailureMarker))
            throw new SimulatedFailureException($"Falha simulada permanente ({PermanentFailureMarker}).");

        if (markers.Contains(TemporaryFailureMarker) && attempt <= options.Value.TemporaryFailures)
            throw new SimulatedFailureException(
                $"Falha simulada temporária ({TemporaryFailureMarker}) na tentativa {attempt}.");
    }
}
