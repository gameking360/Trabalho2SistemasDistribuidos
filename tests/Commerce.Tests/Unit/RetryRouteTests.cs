using System.Text;
using System.Text.Json;
using Commerce.Contracts.Failures;
using Commerce.Contracts.Messaging;
using Commerce.Infrastructure.Retry;
using Commerce.Tests.Support;

namespace Commerce.Tests.Unit;

public sealed class RetryRouteTests
{
    [Fact]
    public void CreateRepublishMessage_PointToPointOrigin_ReturnsToOriginalExchangeAndRoutingKey()
    {
        var envelope = Envelope(Queues.Movements, Exchanges.Movements, RoutingKeys.MovementProcess);

        var message = RetryRoute.CreateRepublishMessage(envelope);

        Assert.Equal(Exchanges.Movements, message.Exchange);
        Assert.Equal(RoutingKeys.MovementProcess, message.RoutingKey);
        Assert.Equal(envelope.MessageId, message.MessageId);
        Assert.Equal(envelope.CurrentPayload.GetRawText(), Encoding.UTF8.GetString(message.Body.Span));
    }

    [Fact]
    public void CreateRepublishMessage_FanoutOrigin_ReturnsOnlyToTheOriginalQueue()
    {
        var envelope = Envelope(Queues.Notifications, Exchanges.Notifications, RoutingKeys.Notifications);

        var message = RetryRoute.CreateRepublishMessage(envelope);

        // Default exchange + nome da fila: somente a fila que falhou recebe a mensagem de novo.
        Assert.Equal(string.Empty, message.Exchange);
        Assert.Equal(Queues.Notifications, message.RoutingKey);
    }

    [Fact]
    public void RetryHeaders_RoundTrip_PreservesAttemptsAndOrigin()
    {
        var envelope = Envelope(Queues.Notifications, Exchanges.Notifications, RoutingKeys.Notifications) with { Attempt = 3 };

        // O client do RabbitMQ entrega cabeçalhos de texto como byte[].
        var receivedHeaders = RetryHeaders.Create(envelope).ToDictionary(
            header => header.Key,
            header => header.Value is string text ? Encoding.UTF8.GetBytes(text) : header.Value);

        var metadata = RetryHeaders.Read(receivedHeaders);

        Assert.Equal(3, metadata.PreviousAttempts);
        Assert.Equal(envelope.FirstAttemptAt, metadata.FirstAttemptAt);
        Assert.Equal(Exchanges.Notifications, metadata.OriginalExchange);
        Assert.Equal(RoutingKeys.Notifications, metadata.OriginalRoutingKey);
        Assert.Equal(envelope.OriginalPayload.GetRawText(), Encoding.UTF8.GetString(metadata.OriginalPayload!));
    }

    [Fact]
    public void RetryHeaders_NewMessage_HasNoRetryHistory()
    {
        Assert.Same(RetryMetadata.None, RetryHeaders.Read(null));
        Assert.Same(RetryMetadata.None, RetryHeaders.Read(new Dictionary<string, object?> { ["outro"] = "valor" }));
    }

    private static RetryEnvelope Envelope(string queue, string exchange, string routingKey)
    {
        var payload = JsonSerializer.SerializeToElement(TestMessages.Notification(), MessageJson.Options);

        return new RetryEnvelope
        {
            MessageId = Guid.NewGuid().ToString(),
            OriginalQueue = queue,
            OriginalExchange = exchange,
            OriginalRoutingKey = routingKey,
            Attempt = 1,
            MaxAttempts = 6,
            OriginalPayload = payload,
            CurrentPayload = payload,
            ErrorReason = "erro",
            FirstAttemptAt = TestMessages.Now,
            LastAttemptAt = TestMessages.Now,
            NextAttemptAt = TestMessages.Now.AddSeconds(2)
        };
    }
}
