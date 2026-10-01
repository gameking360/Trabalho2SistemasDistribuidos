namespace Commerce.Infrastructure.Consuming;

/// <summary>
/// Regra de negócio executada para cada mensagem consumida. Qualquer exceção lançada aqui aciona o retry.
/// </summary>
public interface IMessageHandler<in TMessage>
{
    Task HandleAsync(TMessage message, MessageContext context, CancellationToken cancellationToken);
}
