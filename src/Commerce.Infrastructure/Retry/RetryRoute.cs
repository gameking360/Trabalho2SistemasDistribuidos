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

    public static (string Exchange, string RoutingKey) ResolveTarget(RetryEnvelope envelope) =>
        MessagingTopology.IsFanout(envelope.OriginalExchange)
            // Republicar num exchange fanout entregaria a mensagem de novo a TODAS as filas assinantes
            // (ex.: e-mail e SMS no futuro), duplicando entregas que já deram certo. Nesse caso ela volta apenas
            // para a fila de origem, pelo default exchange ("" + nome da fila como routing key).
            ? (string.Empty, envelope.OriginalQueue)
            : (envelope.OriginalExchange, envelope.OriginalRoutingKey);
}
