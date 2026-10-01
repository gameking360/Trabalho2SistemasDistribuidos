using Commerce.Contracts.Notifications;

namespace Commerce.NotificationWorker.Delivery;

/// <summary>
/// Canal de entrega da notificação. Em um cenário real, cada canal (e-mail, SMS, aplicativo, auditoria) teria
/// sua própria fila ligada ao exchange fanout e sua própria implementação desta interface.
/// </summary>
public interface INotificationSender
{
    Task SendAsync(NotificationMessage notification, CancellationToken cancellationToken);
}
