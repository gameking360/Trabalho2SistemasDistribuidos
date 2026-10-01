using Commerce.Contracts.Messaging;
using Commerce.Infrastructure.Publishing;
using Microsoft.Extensions.Logging;

namespace Commerce.Infrastructure.Retry;

/// <summary>
/// Encaminha uma falha recuperável para <c>retry.queue</c> enquanto houver tentativas; falhas permanentes e tentativas esgotadas vão para a DLQ.
/// Só retorna depois do publisher confirm, para que o consumidor possa então confirmar a mensagem original.
/// </summary>
public sealed class FailedMessageRouter(
    IMessagePublisher publisher,
    FailureEnvelopeFactory envelopes,
    RetryPolicy retryPolicy,
    ILogger<FailedMessageRouter> logger)
{
    public async Task RouteAsync(FailedDelivery failure, CancellationToken cancellationToken)
    {
        if (!failure.IsRetryable || retryPolicy.ShouldDeadLetter(failure.Delivery.Attempt))
        {
            await SendToDeadLetterAsync(failure, cancellationToken);
            return;
        }

        var envelope = envelopes.CreateRetryEnvelope(failure);
        await publisher.PublishAsync(
            OutgoingMessage.Json(Exchanges.Retry, RoutingKeys.Retry, envelope.MessageId, envelope), cancellationToken);

        logger.LogWarning(
            "[{MessageId}] Enviada para retry ({RetryQueue}): tentativa {Attempt}/{MaxAttempts} de {OriginalQueue} falhou; nova tentativa em {DelaySeconds}s",
            envelope.MessageId, Queues.Retry, envelope.Attempt, envelope.MaxAttempts, envelope.OriginalQueue,
            (envelope.NextAttemptAt - envelope.LastAttemptAt).TotalSeconds);
    }

    private async Task SendToDeadLetterAsync(FailedDelivery failure, CancellationToken cancellationToken)
    {
        var deadLetter = envelopes.CreateDeadLetterMessage(failure);
        await publisher.PublishAsync(
            OutgoingMessage.Json(Exchanges.DeadLetter, RoutingKeys.DeadLetter, deadLetter.MessageId, deadLetter), cancellationToken);

        logger.LogError(
            "[{MessageId}] Enviada para DLQ ({DeadLetterQueue}) após {Attempts} tentativas em {TotalMs} ms. Origem: {OriginalQueue}. Motivo: {ErrorReason}",
            deadLetter.MessageId, Queues.DeadLetter, deadLetter.Attempts, deadLetter.ProcessingDurationMs,
            deadLetter.OriginalQueue, deadLetter.ErrorReason);
    }
}
