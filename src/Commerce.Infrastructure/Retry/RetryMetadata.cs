namespace Commerce.Infrastructure.Retry;

/// <summary>
/// Histórico de tentativas de uma entrega, lido dos cabeçalhos colocados pelo Retry Worker.
/// Uma mensagem nova (primeira tentativa) não possui esses cabeçalhos.
/// </summary>
public sealed record RetryMetadata(
    int PreviousAttempts,
    DateTimeOffset? FirstAttemptAt,
    string? OriginalExchange,
    string? OriginalRoutingKey,
    byte[]? OriginalPayload)
{
    public static RetryMetadata None { get; } = new(0, null, null, null, null);
}
