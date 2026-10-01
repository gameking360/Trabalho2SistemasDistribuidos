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
    bool Redelivered,
    RetryMetadata Retry)
{
    /// <summary>Número desta tentativa de processamento (1 = primeira entrega).</summary>
    public int Attempt => Retry.PreviousAttempts + 1;

    // Depois de um retry, exchange, routing key e payload originais vêm dos cabeçalhos, porque a entrega atual
    // pode ter chegado pelo default exchange (retorno direto para a fila de origem).
    public string OriginalExchange => Retry.OriginalExchange ?? Exchange;

    public string OriginalRoutingKey => Retry.OriginalRoutingKey ?? RoutingKey;

    public byte[] OriginalBody => Retry.OriginalPayload ?? Body;

    public static DeliveryContext From(BasicDeliverEventArgs args, string queue)
    {
        // A memória do corpo pertence ao client e só é válida durante o callback; a cópia é usada no retry/DLQ.
        var body = args.Body.ToArray();

        return new DeliveryContext(
            ResolveMessageId(args.BasicProperties.MessageId, body),
            queue,
            args.Exchange,
            args.RoutingKey,
            body,
            args.Redelivered,
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
            // Corpo inválido: segue com um identificador gerado para manter a rastreabilidade nos logs.
        }

        return $"sem-message-id-{Guid.NewGuid():N}";
    }
}
