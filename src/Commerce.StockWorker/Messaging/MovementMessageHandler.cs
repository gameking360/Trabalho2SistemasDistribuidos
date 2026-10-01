using Commerce.Contracts.Messaging;
using Commerce.Contracts.Movements;
using Commerce.Contracts.Notifications;
using Commerce.Contracts.Validation;
using Commerce.Infrastructure.Consuming;
using Commerce.Infrastructure.Publishing;
using Commerce.Infrastructure.Simulation;
using Commerce.StockWorker.Notifications;
using Commerce.StockWorker.Stock;

namespace Commerce.StockWorker.Messaging;

public sealed class MovementMessageHandler(
    StockMovementService stock,
    IMessagePublisher publisher,
    FailureSimulator failureSimulator,
    TimeProvider timeProvider,
    ILogger<MovementMessageHandler> logger) : IMessageHandler<MovementMessage>
{
    public async Task HandleAsync(MovementMessage movement, MessageContext context, CancellationToken cancellationToken)
    {
        var errors = MovementMessageValidator.Validate(movement);
        if (errors.Count > 0)
        {
            var exception = new MessageValidationException(errors);
            await NotifyRejectionAsync(movement, exception.Message, context, cancellationToken);
            throw exception;
        }

        failureSimulator.ThrowIfRequested(movement.Items.Select(item => item.Code), context.Attempt);

        await stock.ApplyAsync(movement, cancellationToken);

        // Notifica só depois do estoque aplicado; num retry o estoque não é reaplicado (idempotência).
        if (!movement.Notify)
        {
            logger.LogInformation("[{MessageId}] notify = false: nenhuma notificação gerada", movement.MessageId);
            return;
        }

        await PublishAsync(movement.MessageId, MovementNotificationFactory.Processed(movement, timeProvider.GetUtcNow()),
            cancellationToken);
    }

    // O solicitante é avisado uma única vez; a mensagem inválida segue para retry/DLQ como qualquer falha.
    private async Task NotifyRejectionAsync(
        MovementMessage movement, string reason, MessageContext context, CancellationToken cancellationToken)
    {
        if (context.Attempt > 1 || !movement.Notify)
            return;

        var notification = MovementNotificationFactory.Rejected(movement, reason, timeProvider.GetUtcNow());
        if (notification.Recipients.Count > 0)
            await PublishAsync(movement.MessageId, notification, cancellationToken);
    }

    private async Task PublishAsync(Guid movementId, NotificationMessage notification, CancellationToken cancellationToken)
    {
        await publisher.PublishAsync(
            OutgoingMessage.Json(Exchanges.Notifications, RoutingKeys.Notifications, notification.MessageId.ToString(), notification),
            cancellationToken);

        logger.LogInformation("[{MessageId}] Notificação {NotificationId} ({NotificationType}) publicada em {Exchange} para {RecipientCount} destinatário(s)",
            movementId, notification.MessageId, notification.NotificationType, Exchanges.Notifications, notification.Recipients.Count);
    }
}
