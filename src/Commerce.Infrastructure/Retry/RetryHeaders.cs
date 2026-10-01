using System.Globalization;
using System.Text;
using Commerce.Contracts.Failures;
using Commerce.Contracts.Messaging;

namespace Commerce.Infrastructure.Retry;

/// <summary>
/// Escreve e lê os cabeçalhos que acompanham uma mensagem devolvida pelo Retry Worker. O corpo continua sendo
/// o contrato original; o histórico de tentativas viaja apenas nos cabeçalhos.
/// </summary>
public static class RetryHeaders
{
    public static IReadOnlyDictionary<string, object?> Create(RetryEnvelope envelope) => new Dictionary<string, object?>
    {
        [MessageHeaders.RetryAttempt] = envelope.Attempt,
        [MessageHeaders.FirstAttemptAt] = envelope.FirstAttemptAt.ToString("O", CultureInfo.InvariantCulture),
        [MessageHeaders.OriginalExchange] = envelope.OriginalExchange,
        [MessageHeaders.OriginalRoutingKey] = envelope.OriginalRoutingKey,
        [MessageHeaders.OriginalPayload] = Encoding.UTF8.GetString(JsonPayload.ToBody(envelope.OriginalPayload))
    };

    public static RetryMetadata Read(IDictionary<string, object?>? headers)
    {
        if (headers is null || !headers.ContainsKey(MessageHeaders.RetryAttempt))
            return RetryMetadata.None;

        return new RetryMetadata(
            PreviousAttempts: Math.Max(ReadInt(headers, MessageHeaders.RetryAttempt), 0),
            FirstAttemptAt: ReadDate(headers, MessageHeaders.FirstAttemptAt),
            OriginalExchange: ReadString(headers, MessageHeaders.OriginalExchange),
            OriginalRoutingKey: ReadString(headers, MessageHeaders.OriginalRoutingKey),
            OriginalPayload: ReadBytes(headers, MessageHeaders.OriginalPayload));
    }

    // O client entrega cabeçalhos de texto como byte[] (tipo longstr do AMQP), não como string.
    private static byte[]? ReadBytes(IDictionary<string, object?> headers, string key) =>
        headers.TryGetValue(key, out var value)
            ? value switch
            {
                byte[] bytes => bytes,
                string text => Encoding.UTF8.GetBytes(text),
                _ => null
            }
            : null;

    private static string? ReadString(IDictionary<string, object?> headers, string key) =>
        ReadBytes(headers, key) is { } bytes ? Encoding.UTF8.GetString(bytes) : null;

    private static int ReadInt(IDictionary<string, object?> headers, string key) =>
        headers.TryGetValue(key, out var value)
            ? value switch
            {
                int number => number,
                long number => (int)number,
                short number => number,
                byte number => number,
                _ => int.TryParse(ReadString(headers, key), NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed) ? parsed : 0
            }
            : 0;

    private static DateTimeOffset? ReadDate(IDictionary<string, object?> headers, string key) =>
        DateTimeOffset.TryParse(ReadString(headers, key), CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var date)
            ? date
            : null;
}
