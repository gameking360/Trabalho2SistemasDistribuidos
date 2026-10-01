using Commerce.Contracts.Failures;
using Commerce.Infrastructure.Publishing;
using Commerce.Infrastructure.Topology;

namespace Commerce.Infrastructure.Retry;

/// <summary>
/// Define como uma mensagem volta ao fluxo depois da espera do retry.
/// </summary>
public static class RetryRoute
{
    public static OutgoingMessage CreateRepublishMessage(RetryEnvelope envelope)
    {
        var (exchange, routingKey) = ResolveTarget(envelope);

        return new OutgoingMessage(exchange, routingKey, envelope.MessageId,
            JsonPayload.ToBody(envelope.CurrentPayload), RetryHeaders.Create(envelope));
    }

    // Republicar num fanout duplicaria a entrega nas demais filas assinantes; por isso a mensagem volta
    // apenas para a fila de origem, pelo default exchange.
    private static (string Exchange, string RoutingKey) ResolveTarget(RetryEnvelope envelope) =>
        MessagingTopology.IsFanout(envelope.OriginalExchange)
            ? (string.Empty, envelope.OriginalQueue)
            : (envelope.OriginalExchange, envelope.OriginalRoutingKey);
}
