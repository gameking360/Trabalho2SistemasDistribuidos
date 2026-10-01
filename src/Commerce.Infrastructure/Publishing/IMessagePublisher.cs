namespace Commerce.Infrastructure.Publishing;

public interface IMessagePublisher
{
    /// <summary>
    /// Publica a mensagem como persistente e só retorna depois que o broker confirma o recebimento
    /// (publisher confirm). Lança <see cref="MessagePublishException"/> se a confirmação não acontecer.
    /// </summary>
    Task PublishAsync(OutgoingMessage message, CancellationToken cancellationToken = default);
}
