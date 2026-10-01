using Commerce.Contracts.Messaging;
using Commerce.Contracts.Movements;
using Commerce.Contracts.Notifications;
using Commerce.Infrastructure.Publishing;
using Commerce.NotificationWorker;
using Commerce.NotificationWorker.Delivery;
using Commerce.StockWorker;
using Commerce.StockWorker.Stock;
using Commerce.Tests.Integration.Infrastructure;
using Commerce.Tests.Support;
using Microsoft.Extensions.DependencyInjection;
using RabbitMQ.Client;

namespace Commerce.Tests.Integration;

[Collection(RabbitMqCollection.Name)]
public sealed class ConsumptionFlowTests(RabbitMqFixture rabbit) : IAsyncLifetime
{
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(15);

    public Task InitializeAsync() => rabbit.PurgeQueuesAsync();

    public Task DisposeAsync() => Task.CompletedTask;

    [RabbitMqFact]
    public async Task StockWorker_ConsumesMovement_UpdatesStockAndPublishesNotification()
    {
        await using var worker = await rabbit.StartWorkerAsync((services, configuration) => services.AddStockWorker(configuration));
        var movement = TestMessages.Movement(MovementTypes.Entry, notify: true, recipients: ["compras@exemplo.com"],
            items: [TestMessages.Item(code: "SKU-NOTIFICA", quantity: 5)]);

        await rabbit.Publisher.PublishAsync(
            OutgoingMessage.Json(Exchanges.Movements, RoutingKeys.MovementProcess, movement.MessageId.ToString(), movement));

        var delivered = await rabbit.TakeMessageAsync(Queues.Notifications, Timeout);
        Assert.Equal(DeliveryModes.Persistent, delivered.BasicProperties.DeliveryMode);

        var notification = MessageJson.Deserialize<NotificationMessage>(delivered.Body.Span);
        Assert.Equal(new[] { "compras@exemplo.com" }, notification.Recipients);
        Assert.Contains("SKU-NOTIFICA", notification.Content);
        Assert.Equal(5, worker.Services.GetRequiredService<IStockRepository>().GetBalance("SKU-NOTIFICA"));
    }

    [RabbitMqFact]
    public async Task NotificationWorker_ConsumesNotification_AndDeliversToRecipients()
    {
        var sender = new CapturingNotificationSender();
        await using var worker = await rabbit.StartWorkerAsync((services, configuration) =>
        {
            services.AddSingleton<INotificationSender>(sender);
            services.AddNotificationWorker(configuration);
        });
        var notification = TestMessages.Notification("cliente@exemplo.com", "+55 47 99999-0000");

        await rabbit.Publisher.PublishAsync(
            OutgoingMessage.Json(Exchanges.Notifications, RoutingKeys.Notifications, notification.MessageId.ToString(), notification));

        var delivered = await sender.WaitForAsync(notification.MessageId, Timeout);
        Assert.Equal(notification.Recipients, delivered.Recipients);
        Assert.Equal(notification.Content, delivered.Content);
    }
}
