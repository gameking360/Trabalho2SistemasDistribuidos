using Commerce.Contracts.Notifications;
using Commerce.Contracts.Validation;
using Commerce.Infrastructure.Consuming;
using Commerce.Infrastructure.Simulation;
using Commerce.NotificationWorker.Delivery;

namespace Commerce.NotificationWorker.Messaging;

public sealed class NotificationMessageHandler(
    INotificationSender sender,
    FailureSimulator failureSimulator) : IMessageHandler<NotificationMessage>
{
    public async Task HandleAsync(NotificationMessage notification, MessageContext context, CancellationToken cancellationToken)
    {
        var errors = NotificationMessageValidator.Validate(notification);
        if (errors.Count > 0)
            throw new MessageValidationException(errors);

        failureSimulator.ThrowIfRequested(notification.Recipients, context.Attempt);

        await sender.SendAsync(notification, cancellationToken);
    }
}
