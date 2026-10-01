namespace Commerce.Infrastructure.Publishing;

/// <summary>
/// A mensagem não foi confirmada pelo broker: quem publicou deve tratar a falha.
/// </summary>
public sealed class MessagePublishException(OutgoingMessage message, string reason, Exception? innerException = null)
    : Exception($"Não foi possível publicar a mensagem {message.MessageId} em '{message.Exchange}' " +
                $"(routing key '{message.RoutingKey}'): {reason}.", innerException);
