using Commerce.Contracts.Failures;
using Commerce.Contracts.Messaging;
using Commerce.Infrastructure.Consuming;
using Commerce.Infrastructure.Retry;
using Commerce.Tests.Support;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace Commerce.Tests.Unit;

public sealed class FailedMessageRouterTests
{
    private readonly FakeMessagePublisher _publisher = new();
    private readonly FailedMessageRouter _router;

    public FailedMessageRouterTests()
    {
        var policy = new RetryPolicy(Options.Create(new RetryOptions()));
        var envelopes = new FailureEnvelopeFactory(policy, new FixedTimeProvider(TestMessages.Now));
        _router = new FailedMessageRouter(_publisher, envelopes, policy, NullLogger<FailedMessageRouter>.Instance);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(3)]
    [InlineData(5)]
    public async Task RouteAsync_BeforeTheSixthFailure_PublishesEnvelopeToRetryQueue(int failedAttempt)
    {
        await _router.RouteAsync(Failure(failedAttempt), CancellationToken.None);

        var published = Assert.Single(_publisher.Messages);
        Assert.Equal(Exchanges.Retry, published.Exchange);
        Assert.Equal(RoutingKeys.Retry, published.RoutingKey);

        var envelope = _publisher.Deserialize<RetryEnvelope>(published);
        Assert.Equal(failedAttempt, envelope.Attempt);
        Assert.Equal(Queues.Movements, envelope.OriginalQueue);
    }

    [Fact]
    public async Task RouteAsync_SixthFailure_IsolatesMessageInDeadLetterQueue()
    {
        await _router.RouteAsync(Failure(6), CancellationToken.None);

        var published = Assert.Single(_publisher.Messages);
        Assert.Equal(Exchanges.DeadLetter, published.Exchange);
        Assert.Equal(RoutingKeys.DeadLetter, published.RoutingKey);

        var deadLetter = _publisher.Deserialize<DeadLetterMessage>(published);
        Assert.Equal(6, deadLetter.Attempts);
        Assert.Equal(Queues.Movements, deadLetter.OriginalQueue);
    }

    [Fact]
    public async Task RouteAsync_NonRetryableFailure_IsolatesMessageInDeadLetterQueueOnFirstAttempt()
    {
        await _router.RouteAsync(Failure(1, isRetryable: false), CancellationToken.None);

        var published = Assert.Single(_publisher.Messages);
        Assert.Equal(Exchanges.DeadLetter, published.Exchange);
        Assert.Equal(RoutingKeys.DeadLetter, published.RoutingKey);

        var deadLetter = _publisher.Deserialize<DeadLetterMessage>(published);
        Assert.Equal(1, deadLetter.Attempts);
    }

    private static FailedDelivery Failure(int failedAttempt, bool isRetryable = true)
    {
        var body = MessageJson.Serialize(TestMessages.Movement());
        var retry = failedAttempt == 1
            ? RetryMetadata.None
            : new RetryMetadata(failedAttempt - 1, TestMessages.Now.AddSeconds(-10), Exchanges.Movements, RoutingKeys.MovementProcess, body);

        var delivery = new DeliveryContext("movimentacao-1", Queues.Movements, Exchanges.Movements, RoutingKeys.MovementProcess,
            body, retry);

        return new FailedDelivery(delivery, TestMessages.Now, "Falha simulada", isRetryable);
    }
}
