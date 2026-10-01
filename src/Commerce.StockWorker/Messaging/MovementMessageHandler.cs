using Commerce.Contracts.Messaging;
using Commerce.Contracts.Movements;
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
            throw new MessageValidationException(errors);

        failureSimulator.ThrowIfRequested(movement.Items.Select(item => item.Code), context.Attempt);

        await stock.ApplyAsync(movement, cancellationToken);

        // A notificação só é gerada depois que o estoque foi atualizado com sucesso. Se a publicação falhar,
        // a movimentação vai para o retry e, na nova tentativa, o estoque não é reaplicado (idempotência).
        if (!movement.Notify)
        {
            logger.LogInformation("[{MessageId}] notify = false: nenhuma notificação gerada", movement.MessageId);
            return;
        }

        var notification = MovementNotificationFactory.Create(movement, timeProvider.GetUtcNow());
        await publisher.PublishAsync(
            OutgoingMessage.Json(Exchanges.Notifications, RoutingKeys.Notifications, notification.MessageId.ToString(), notification),
            cancellationToken);

        logger.LogInformation("[{MessageId}] Notificação {NotificationId} publicada em {Exchange} para {RecipientCount} destinatário(s)",
            movement.MessageId, notification.MessageId, Exchanges.Notifications, notification.Recipients.Count);
    }
}
