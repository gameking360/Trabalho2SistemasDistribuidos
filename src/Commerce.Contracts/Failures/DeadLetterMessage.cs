using System.Text.Json;

namespace Commerce.Contracts.Failures;

/// <summary>
/// Mensagem publicada em <c>commerce.dlx</c> → <c>dead-letter.queue</c> quando as tentativas se esgotam.
/// Reúne tudo o que é necessário para a análise manual do erro.
/// </summary>
public sealed record DeadLetterMessage
{
    public string MessageId { get; init; } = string.Empty;

    public string OriginalQueue { get; init; } = string.Empty;

    public string OriginalExchange { get; init; } = string.Empty;

    public string OriginalRoutingKey { get; init; } = string.Empty;

    public int Attempts { get; init; }

    public string ErrorReason { get; init; } = string.Empty;

    public DateTimeOffset FirstAttemptAt { get; init; }

    public DateTimeOffset LastAttemptAt { get; init; }

    /// <summary>Tempo entre a primeira e a última tentativa, incluindo as esperas do retry.</summary>
    public long ProcessingDurationMs { get; init; }

    public bool PayloadChanged { get; init; }

    public JsonElement OriginalPayload { get; init; }

    public JsonElement CurrentPayload { get; init; }
}
