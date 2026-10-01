using Commerce.Contracts.Notifications;

namespace Commerce.NotificationWorker.Delivery;

/// <summary>
/// Simula a entrega registrando no log tudo o que seria enviado ao destinatário.
/// </summary>
public sealed class LogNotificationSender(ILogger<LogNotificationSender> logger) : INotificationSender
{
    public Task SendAsync(NotificationMessage notification, CancellationToken cancellationToken)
    {
        logger.LogInformation(
            "[{MessageId}] Notificação entregue (simulação) | tipo: {NotificationType} | destinatários: {Recipients} | conteúdo: {Content}",
            notification.MessageId,
            notification.NotificationType,
            string.Join(", ", notification.Recipients),
            notification.Content);

        return Task.CompletedTask;
    }
}
