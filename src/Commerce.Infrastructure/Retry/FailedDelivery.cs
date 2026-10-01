using Commerce.Infrastructure.Consuming;

namespace Commerce.Infrastructure.Retry;

public sealed record FailedDelivery(DeliveryContext Delivery, DateTimeOffset AttemptStartedAt, string ErrorReason)
{
    public DateTimeOffset FirstAttemptAt => Delivery.Retry.FirstAttemptAt ?? AttemptStartedAt;
}
