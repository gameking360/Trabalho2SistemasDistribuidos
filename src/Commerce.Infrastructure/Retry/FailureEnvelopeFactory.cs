using Commerce.Contracts.Failures;

namespace Commerce.Infrastructure.Retry;

/// <summary>
/// Monta os envelopes de retry e de DLQ a partir de uma entrega que falhou, preservando a origem da mensagem.
/// </summary>
public sealed class FailureEnvelopeFactory(RetryPolicy retryPolicy, TimeProvider timeProvider)
{
    public RetryEnvelope CreateRetryEnvelope(FailedDelivery failure)
    {
        var now = timeProvider.GetUtcNow();
        var delivery = failure.Delivery;

        return new RetryEnvelope
        {
            MessageId = delivery.MessageId,
            OriginalQueue = delivery.Queue,
            OriginalExchange = delivery.OriginalExchange,
            OriginalRoutingKey = delivery.OriginalRoutingKey,
            Attempt = delivery.Attempt,
            MaxAttempts = retryPolicy.MaxAttempts,
            OriginalPayload = JsonPayload.ToElement(delivery.OriginalBody),
            CurrentPayload = JsonPayload.ToElement(delivery.Body),
            PayloadChanged = !JsonPayload.AreEquivalent(delivery.OriginalBody, delivery.Body),
            ErrorReason = failure.ErrorReason,
            FirstAttemptAt = failure.FirstAttemptAt,
            LastAttemptAt = now,
            NextAttemptAt = now + retryPolicy.GetDelay(delivery.Attempt)
        };
    }

    public DeadLetterMessage CreateDeadLetterMessage(FailedDelivery failure)
    {
        var now = timeProvider.GetUtcNow();
        var delivery = failure.Delivery;

        return new DeadLetterMessage
        {
            MessageId = delivery.MessageId,
            OriginalQueue = delivery.Queue,
            OriginalExchange = delivery.OriginalExchange,
            OriginalRoutingKey = delivery.OriginalRoutingKey,
            Attempts = delivery.Attempt,
            ErrorReason = failure.ErrorReason,
            FirstAttemptAt = failure.FirstAttemptAt,
            LastAttemptAt = now,
            ProcessingDurationMs = (long)(now - failure.FirstAttemptAt).TotalMilliseconds,
            PayloadChanged = !JsonPayload.AreEquivalent(delivery.OriginalBody, delivery.Body),
            OriginalPayload = JsonPayload.ToElement(delivery.OriginalBody),
            CurrentPayload = JsonPayload.ToElement(delivery.Body)
        };
    }
}
