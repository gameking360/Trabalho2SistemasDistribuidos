using Commerce.Contracts.Messaging;
using Commerce.Contracts.Movements;
using Commerce.Infrastructure.Publishing;
using Commerce.StockWorker;
using Commerce.StockWorker.Stock;
using Commerce.Tests.Integration.Infrastructure;
using Commerce.Tests.Support;
using Microsoft.Extensions.DependencyInjection;
using RabbitMQ.Client;

namespace Commerce.Tests.Integration;

[Collection(RabbitMqCollection.Name)]
public sealed class MovementPublishingTests(RabbitMqFixture rabbit) : IAsyncLifetime
{
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(15);

    public Task InitializeAsync() => rabbit.PurgeQueuesAsync();

    public Task DisposeAsync() => Task.CompletedTask;

    [RabbitMqFact]
    public async Task PublishMovement_StoresPersistentMessageInDurableMovementsQueue()
    {
        var movement = TestMessages.Movement();

        await rabbit.Publisher.PublishAsync(
            OutgoingMessage.Json(Exchanges.Movements, RoutingKeys.MovementProcess, movement.MessageId.ToString(), movement));

        var delivered = await rabbit.TakeMessageAsync(Queues.Movements, Timeout);
        Assert.Equal(DeliveryModes.Persistent, delivered.BasicProperties.DeliveryMode);
        Assert.Equal(movement.MessageId.ToString(), delivered.BasicProperties.MessageId);
        Assert.Equal("application/json", delivered.BasicProperties.ContentType);

        var body = MessageJson.Deserialize<MovementMessage>(delivered.Body.Span);
        Assert.Equal(movement.MessageId, body.MessageId);
        Assert.Equal(MovementTypes.Exit, body.MovementType);

        var queue = await rabbit.GetQueueInfoAsync(Queues.Movements);
        Assert.True(queue.GetProperty("durable").GetBoolean());
        Assert.True(queue.GetProperty("arguments").GetProperty("x-single-active-consumer").GetBoolean());
    }

    [RabbitMqFact]
    public async Task Movement_PublishedWhileStockWorkerIsOffline_StaysInQueueUntilTheWorkerStarts()
    {
        var movement = TestMessages.Movement(MovementTypes.Entry, items: [TestMessages.Item(code: "SKU-OFFLINE", quantity: 7)]);

        await rabbit.Publisher.PublishAsync(
            OutgoingMessage.Json(Exchanges.Movements, RoutingKeys.MovementProcess, movement.MessageId.ToString(), movement));

        Assert.Equal(1u, await rabbit.CountMessagesAsync(Queues.Movements));

        await using (var worker = await rabbit.StartWorkerAsync((services, configuration) => services.AddStockWorker(configuration)))
        {
            var stock = worker.Services.GetRequiredService<IStockRepository>();
            await RabbitMqFixture.WaitUntilAsync(() => stock.GetBalance("SKU-OFFLINE") == 7, Timeout, "processamento da movimentação");

            // O ack é enviado logo após o handler; aguarda antes de desligar o worker.
            await Task.Delay(TimeSpan.FromMilliseconds(500));
        }

        Assert.Equal(0u, await rabbit.CountMessagesAsync(Queues.Movements));
    }
}
