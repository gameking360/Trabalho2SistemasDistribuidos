using System.Text.Json;

namespace Commerce.Contracts.Failures;

/// <summary>
/// Envelope publicado em <c>commerce.retry</c> quando o processamento de uma mensagem falha.
/// Preserva a origem (fila, exchange e routing key) para que o Retry Worker devolva a mensagem ao fluxo correto.
/// </summary>
public sealed record RetryEnvelope
{
    public string MessageId { get; init; } = string.Empty;

    public string OriginalQueue { get; init; } = string.Empty;

    public string OriginalExchange { get; init; } = string.Empty;

    public string OriginalRoutingKey { get; init; } = string.Empty;

    /// <summary>Número da tentativa que falhou (1 = primeira tentativa).</summary>
    public int Attempt { get; init; }

    public int MaxAttempts { get; init; }

    /// <summary>Payload recebido na primeira tentativa.</summary>
    public JsonElement OriginalPayload { get; init; }

    /// <summary>Payload recebido na tentativa que falhou. É ele que volta para a fila de origem.</summary>
    public JsonElement CurrentPayload { get; init; }

    public bool PayloadChanged { get; init; }

    public string ErrorReason { get; init; } = string.Empty;

    public DateTimeOffset FirstAttemptAt { get; init; }

    public DateTimeOffset LastAttemptAt { get; init; }

    public DateTimeOffset NextAttemptAt { get; init; }
}
