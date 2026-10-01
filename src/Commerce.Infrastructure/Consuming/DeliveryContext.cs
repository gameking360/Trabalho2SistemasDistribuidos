using System.Text.Json;
using Commerce.Infrastructure.Retry;
using RabbitMQ.Client.Events;

namespace Commerce.Infrastructure.Consuming;

public sealed record DeliveryContext(
    string MessageId,
    string Queue,
    string Exchange,
    string RoutingKey,
    byte[] Body,
    RetryMetadata Retry)
{
    /// <summary>Número desta tentativa de processamento (1 = primeira entrega).</summary>
    public int Attempt => Retry.PreviousAttempts + 1;

    // Após um retry a entrega pode chegar pelo default exchange; a origem real vem dos cabeçalhos.
    public string OriginalExchange => Retry.OriginalExchange ?? Exchange;

    public string OriginalRoutingKey => Retry.OriginalRoutingKey ?? RoutingKey;

    public byte[] OriginalBody => Retry.OriginalPayload ?? Body;

    public static DeliveryContext From(BasicDeliverEventArgs args, string queue)
    {
        // O corpo só é válido durante o callback do client; a cópia é usada no retry/DLQ.
        var body = args.Body.ToArray();

        return new DeliveryContext(
            ResolveMessageId(args.BasicProperties.MessageId, body),
            queue,
            args.Exchange,
            args.RoutingKey,
            body,
            RetryHeaders.Read(args.BasicProperties.Headers));
    }

    private static string ResolveMessageId(string? messageIdProperty, byte[] body)
    {
        if (!string.IsNullOrWhiteSpace(messageIdProperty))
            return messageIdProperty;

        try
        {
            using var document = JsonDocument.Parse(body);
            if (document.RootElement.ValueKind == JsonValueKind.Object &&
                document.RootElement.TryGetProperty("messageId", out var messageId) &&
                messageId.ValueKind == JsonValueKind.String &&
                !string.IsNullOrWhiteSpace(messageId.GetString()))
            {
                return messageId.GetString()!;
            }
        }
        catch (JsonException)
        {
            // Corpo inválido: usa um identificador gerado para manter a rastreabilidade.
        }

        return $"sem-message-id-{Guid.NewGuid():N}";
    }
}
