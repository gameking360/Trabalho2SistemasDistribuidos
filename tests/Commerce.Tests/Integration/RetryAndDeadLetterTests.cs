using System.Text;
using Commerce.Contracts.Failures;
using Commerce.Contracts.Messaging;
using Commerce.Contracts.Movements;
using Commerce.Infrastructure.Publishing;
using Commerce.Infrastructure.Simulation;
using Commerce.NotificationWorker;
using Commerce.RetryWorker;
using Commerce.RetryWorker.DeadLetters;
using Commerce.StockWorker;
using Commerce.StockWorker.Stock;
using Commerce.Tests.Integration.Infrastructure;
using Commerce.Tests.Support;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using RabbitMQ.Client;

namespace Commerce.Tests.Integration;

[Collection(RabbitMqCollection.Name)]
public sealed class RetryAndDeadLetterTests(RabbitMqFixture rabbit) : IAsyncLifetime
{
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(30);

    public Task InitializeAsync() => rabbit.PurgeQueuesAsync();

    public Task DisposeAsync() => Task.CompletedTask;

    [RabbitMqFact]
    public async Task TemporaryFailure_GoesThroughRetryQueue_AndIsProcessedOnTheNextAttempt()
    {
        await using var stockWorker = await rabbit.StartWorkerAsync((services, configuration) => services.AddStockWorker(configuration));
        var movement = TestMessages.Movement(MovementTypes.Entry,
            items: [TestMessages.Item(code: FailureSimulator.TemporaryFailureMarker, quantity: 3)]);

        await rabbit.Publisher.PublishAsync(
            OutgoingMessage.Json(Exchanges.Movements, RoutingKeys.MovementProcess, movement.MessageId.ToString(), movement));

        // 1ª tentativa falha: o envelope vai para a retry.queue preservando a origem da mensagem.
        var retryMessage = await rabbit.PeekMessageAsync(Queues.Retry, Timeout);
        var envelope = MessageJson.Deserialize<RetryEnvelope>(retryMessage.Body.Span);
        Assert.Equal(movement.MessageId.ToString(), envelope.MessageId);
        Assert.Equal(1, envelope.Attempt);
        Assert.Equal(Queues.Movements, envelope.OriginalQueue);
        Assert.Equal(Exchanges.Movements, envelope.OriginalExchange);
        Assert.Equal(RoutingKeys.MovementProcess, envelope.OriginalRoutingKey);
        Assert.Contains(FailureSimulator.TemporaryFailureMarker, envelope.ErrorReason);
        Assert.Equal(TimeSpan.FromMilliseconds(100), envelope.NextAttemptAt - envelope.LastAttemptAt);

        // O Retry Worker aguarda e devolve a mensagem para commerce.movements; a 2ª tentativa tem sucesso.
        await using var retryWorker = await rabbit.StartWorkerAsync((services, configuration) => services.AddRetryWorker(configuration));
        var stock = stockWorker.Services.GetRequiredService<IStockRepository>();
        await RabbitMqFixture.WaitUntilAsync(() => stock.GetBalance(FailureSimulator.TemporaryFailureMarker) == 3, Timeout,
            "reprocessamento da movimentação após o retry");

        Assert.Equal(0u, await rabbit.CountMessagesAsync(Queues.DeadLetter));
    }

    [RabbitMqFact]
    public async Task PermanentFailure_IsIsolatedInDeadLetterQueue_AfterSixAttempts()
    {
        await using var stockWorker = await rabbit.StartWorkerAsync((services, configuration) => services.AddStockWorker(configuration));
        await using var retryWorker = await rabbit.StartWorkerAsync((services, configuration) => services.AddRetryWorker(configuration));
        var movement = TestMessages.Movement(items: [TestMessages.Item(code: FailureSimulator.PermanentFailureMarker)]);

        await rabbit.Publisher.PublishAsync(
            OutgoingMessage.Json(Exchanges.Movements, RoutingKeys.MovementProcess, movement.MessageId.ToString(), movement));

        var dead = await rabbit.TakeMessageAsync(Queues.DeadLetter, Timeout);
        var deadLetter = MessageJson.Deserialize<DeadLetterMessage>(dead.Body.Span);

        Assert.Equal(movement.MessageId.ToString(), deadLetter.MessageId);
        Assert.Equal(6, deadLetter.Attempts);
        Assert.Equal(Queues.Movements, deadLetter.OriginalQueue);
        Assert.Equal(Exchanges.Movements, deadLetter.OriginalExchange);
        Assert.Equal(RoutingKeys.MovementProcess, deadLetter.OriginalRoutingKey);
        Assert.Contains("Falha simulada permanente", deadLetter.ErrorReason);
        Assert.False(deadLetter.PayloadChanged);
        Assert.Equal(movement.MessageId.ToString(), deadLetter.OriginalPayload.GetProperty("messageId").GetString());

        // Esperas progressivas de 100, 200, 300, 400 e 500 ms: a escala de teste do 2s, 4s, 6s, 8s e 10s.
        Assert.True(deadLetter.ProcessingDurationMs >= 1_500, $"Duração total: {deadLetter.ProcessingDurationMs} ms");
        Assert.Equal(0u, await rabbit.CountMessagesAsync(Queues.Retry));
        Assert.Equal(0u, await rabbit.CountMessagesAsync(Queues.Movements));
    }

    [RabbitMqFact]
    public async Task NotificationFailure_IsRetriedOnItsOwnQueue_AndIsolatedInDeadLetterQueue()
    {
        await using var notificationWorker = await rabbit.StartWorkerAsync((services, configuration) => services.AddNotificationWorker(configuration));
        await using var retryWorker = await rabbit.StartWorkerAsync((services, configuration) => services.AddRetryWorker(configuration));
        var notification = TestMessages.Notification(FailureSimulator.PermanentFailureMarker);

        await rabbit.Publisher.PublishAsync(
            OutgoingMessage.Json(Exchanges.Notifications, RoutingKeys.Notifications, notification.MessageId.ToString(), notification));

        var dead = await rabbit.TakeMessageAsync(Queues.DeadLetter, Timeout);
        var deadLetter = MessageJson.Deserialize<DeadLetterMessage>(dead.Body.Span);

        Assert.Equal(notification.MessageId.ToString(), deadLetter.MessageId);
        Assert.Equal(6, deadLetter.Attempts);
        Assert.Equal(Queues.Notifications, deadLetter.OriginalQueue);
        Assert.Equal(Exchanges.Notifications, deadLetter.OriginalExchange);
        Assert.Equal(0u, await rabbit.CountMessagesAsync(Queues.Notifications));
    }

    [RabbitMqFact]
    public async Task DeadLetterCleanup_RemovesOnlyMessagesOlderThanTheRetentionPeriod()
    {
        var now = DateTimeOffset.UtcNow;
        await PublishDeadLetterAsync("isolada-ha-40-dias", now.AddDays(-40));
        await PublishDeadLetterAsync("isolada-ontem", now.AddDays(-1));

        var cleanup = new DeadLetterCleanupService(rabbit.ConnectionProvider,
            Options.Create(new DeadLetterCleanupOptions()), TimeProvider.System, NullLogger<DeadLetterCleanupService>.Instance);

        var removed = await cleanup.CleanupAsync(TimeSpan.FromDays(30), CancellationToken.None);

        Assert.Equal(1, removed);
        Assert.Equal(1u, await rabbit.CountMessagesAsync(Queues.DeadLetter));
        var remaining = await rabbit.TakeMessageAsync(Queues.DeadLetter, Timeout);
        Assert.Equal("isolada-ontem", remaining.BasicProperties.MessageId);
    }

    private async Task PublishDeadLetterAsync(string messageId, DateTimeOffset deadLetteredAt)
    {
        await using var channel = await rabbit.OpenChannelAsync();
        var properties = new BasicProperties
        {
            Persistent = true,
            MessageId = messageId,
            Timestamp = new AmqpTimestamp(deadLetteredAt.ToUnixTimeSeconds())
        };

        await channel.BasicPublishAsync(Exchanges.DeadLetter, RoutingKeys.DeadLetter, mandatory: true, properties,
            Encoding.UTF8.GetBytes("{}"));
    }
}
