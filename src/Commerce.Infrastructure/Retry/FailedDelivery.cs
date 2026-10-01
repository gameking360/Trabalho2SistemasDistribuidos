using Commerce.Infrastructure.Consuming;

namespace Commerce.Infrastructure.Retry;

public sealed record FailedDelivery(
    DeliveryContext Delivery,
    DateTimeOffset AttemptStartedAt,
    string ErrorReason,
    bool IsRetryable = true)
{
    public DateTimeOffset FirstAttemptAt => Delivery.Retry.FirstAttemptAt ?? AttemptStartedAt;
}
