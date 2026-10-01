using System.Collections.Concurrent;
using Commerce.Contracts.Notifications;
using Commerce.NotificationWorker.Delivery;

namespace Commerce.Tests.Integration.Infrastructure;

/// <summary>
/// Substitui o envio simulado por log para que o teste confira o que foi entregue.
/// </summary>
public sealed class CapturingNotificationSender : INotificationSender
{
    private readonly ConcurrentDictionary<Guid, TaskCompletionSource<NotificationMessage>> _deliveries = new();

    public Task SendAsync(NotificationMessage notification, CancellationToken cancellationToken)
    {
        Delivery(notification.MessageId).TrySetResult(notification);
        return Task.CompletedTask;
    }

    public Task<NotificationMessage> WaitForAsync(Guid messageId, TimeSpan timeout) =>
        Delivery(messageId).Task.WaitAsync(timeout);

    private TaskCompletionSource<NotificationMessage> Delivery(Guid messageId) =>
        _deliveries.GetOrAdd(messageId, _ => new TaskCompletionSource<NotificationMessage>(TaskCreationOptions.RunContinuationsAsynchronously));
}
