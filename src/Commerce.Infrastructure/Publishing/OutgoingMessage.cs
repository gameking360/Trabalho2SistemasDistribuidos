using Commerce.Contracts.Messaging;

namespace Commerce.Infrastructure.Publishing;

public sealed record OutgoingMessage(
    string Exchange,
    string RoutingKey,
    string MessageId,
    ReadOnlyMemory<byte> Body,
    IReadOnlyDictionary<string, object?>? Headers = null)
{
    public static OutgoingMessage Json<T>(string exchange, string routingKey, string messageId, T payload) =>
        new(exchange, routingKey, messageId, MessageJson.Serialize(payload));
}
