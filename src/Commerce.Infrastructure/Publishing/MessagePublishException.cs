namespace Commerce.Infrastructure.Publishing;

/// <summary>
/// A mensagem não foi confirmada pelo broker: quem publicou deve tratar a falha (nada é descartado em silêncio).
/// </summary>
public sealed class MessagePublishException(OutgoingMessage message, string reason, Exception? innerException = null)
    : Exception($"Não foi possível publicar a mensagem {message.MessageId} em '{message.Exchange}' " +
                $"(routing key '{message.RoutingKey}'): {reason}.", innerException)
{
    public string MessageId { get; } = message.MessageId;

    public string Exchange { get; } = message.Exchange;

    public string RoutingKey { get; } = message.RoutingKey;
}
