using Commerce.Contracts.Messaging;
using Commerce.Contracts.Movements;
using Commerce.Contracts.Notifications;
using Commerce.Contracts.Validation;
using Commerce.Infrastructure.Consuming;
using Commerce.Infrastructure.Simulation;
using Commerce.StockWorker.Messaging;
using Commerce.StockWorker.Notifications;
using Commerce.StockWorker.Stock;
using Commerce.Tests.Support;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace Commerce.Tests.Unit;

public sealed class MovementMessageHandlerTests
{
    private readonly FakeMessagePublisher _publisher = new();
    private readonly InMemoryStockRepository _stock = new();

    [Fact]
    public async Task HandleAsync_NotifyTrue_UpdatesStockAndPublishesNotification()
    {
        var movement = TestMessages.Movement(MovementTypes.Exit, notify: true, recipients: ["cliente@exemplo.com"]);

        await CreateHandler().HandleAsync(movement, Context(), CancellationToken.None);

        Assert.Equal(-2, _stock.GetBalance("SKU-001"));

        var published = Assert.Single(_publisher.Messages);
        Assert.Equal(Exchanges.Notifications, published.Exchange);

        var notification = _publisher.Deserialize<NotificationMessage>(published);
        Assert.Equal(new[] { "cliente@exemplo.com" }, notification.Recipients);
        Assert.Equal(MovementNotificationFactory.ProcessedType, notification.NotificationType);
        Assert.Contains("SKU-001", notification.Content);
        Assert.NotEqual(Guid.Empty, notification.MessageId);
    }

    [Fact]
    public async Task HandleAsync_NotifyFalse_UpdatesStockWithoutNotification()
    {
        var movement = TestMessages.Movement(MovementTypes.Entry, notify: false);

        await CreateHandler().HandleAsync(movement, Context(), CancellationToken.None);

        Assert.Equal(2, _stock.GetBalance("SKU-001"));
        Assert.Empty(_publisher.Messages);
    }

    [Fact]
    public async Task HandleAsync_InvalidMovement_ThrowsWithoutTouchingStockOrNotifying()
    {
        var movement = TestMessages.Movement(notify: true, recipients: [], items: [TestMessages.Item(quantity: 0)]);

        var exception = await Assert.ThrowsAsync<MessageValidationException>(
            () => CreateHandler().HandleAsync(movement, Context(), CancellationToken.None));

        Assert.StartsWith("Mensagem inválida", exception.Message);
        Assert.Equal(0, _stock.GetBalance("SKU-001"));
        Assert.Empty(_publisher.Messages);
    }

    [Fact]
    public async Task HandleAsync_InvalidMovementWithRecipients_NotifiesRejectionAndThrows()
    {
        var movement = TestMessages.Movement(movementType: "Transferencia", notify: true, recipients: ["cliente@exemplo.com"]);

        await Assert.ThrowsAsync<MessageValidationException>(
            () => CreateHandler().HandleAsync(movement, Context(), CancellationToken.None));

        var published = Assert.Single(_publisher.Messages);
        Assert.Equal(Exchanges.Notifications, published.Exchange);

        var notification = _publisher.Deserialize<NotificationMessage>(published);
        Assert.Equal(MovementNotificationFactory.RejectedType, notification.NotificationType);
        Assert.Equal(new[] { "cliente@exemplo.com" }, notification.Recipients);
        Assert.Contains("Transferencia", notification.Content);
        Assert.Equal(0, _stock.GetBalance("SKU-001"));
    }

    [Fact]
    public async Task HandleAsync_InvalidMovementOnRetry_DoesNotNotifyAgain()
    {
        var movement = TestMessages.Movement(movementType: "Transferencia", notify: true, recipients: ["cliente@exemplo.com"]);

        await Assert.ThrowsAsync<MessageValidationException>(
            () => CreateHandler().HandleAsync(movement, Context(attempt: 2), CancellationToken.None));

        Assert.Empty(_publisher.Messages);
    }

    [Fact]
    public async Task HandleAsync_ProcessingFails_DoesNotNotify()
    {
        var movement = TestMessages.Movement(notify: true, recipients: ["cliente@exemplo.com"],
            items: [TestMessages.Item(code: FailureSimulator.PermanentFailureMarker)]);

        await Assert.ThrowsAsync<SimulatedFailureException>(
            () => CreateHandler(simulateFailures: true).HandleAsync(movement, Context(), CancellationToken.None));

        Assert.Empty(_publisher.Messages);
    }

    [Fact]
    public async Task HandleAsync_TemporaryFailure_SucceedsOnTheNextAttempt()
    {
        var movement = TestMessages.Movement(MovementTypes.Entry,
            items: [TestMessages.Item(code: FailureSimulator.TemporaryFailureMarker, quantity: 4)]);
        var handler = CreateHandler(simulateFailures: true);

        await Assert.ThrowsAsync<SimulatedFailureException>(
            () => handler.HandleAsync(movement, Context(attempt: 1), CancellationToken.None));
        await handler.HandleAsync(movement, Context(attempt: 2), CancellationToken.None);

        Assert.Equal(4, _stock.GetBalance(FailureSimulator.TemporaryFailureMarker));
    }

    [Fact]
    public async Task HandleAsync_RedeliveredMovement_DoesNotApplyStockTwice()
    {
        var movement = TestMessages.Movement(MovementTypes.Entry);
        var handler = CreateHandler();

        await handler.HandleAsync(movement, Context(), CancellationToken.None);
        await handler.HandleAsync(movement, Context(), CancellationToken.None);

        Assert.Equal(2, _stock.GetBalance("SKU-001"));
    }

    private MovementMessageHandler CreateHandler(bool simulateFailures = false) => new(
        new StockMovementService(_stock, Options.Create(new StockOptions { SimulatedProcessingTime = TimeSpan.Zero }),
            NullLogger<StockMovementService>.Instance),
        _publisher,
        new FailureSimulator(Options.Create(new FailureSimulationOptions { Enabled = simulateFailures })),
        new FixedTimeProvider(TestMessages.Now),
        NullLogger<MovementMessageHandler>.Instance);

    private static MessageContext Context(int attempt = 1) => new(attempt);
}
