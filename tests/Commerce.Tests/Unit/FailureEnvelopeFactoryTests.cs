using System.Text;
using System.Text.Json;
using Commerce.Contracts.Messaging;
using Commerce.Infrastructure.Consuming;
using Commerce.Infrastructure.Retry;
using Commerce.Tests.Support;
using Microsoft.Extensions.Options;

namespace Commerce.Tests.Unit;

public sealed class FailureEnvelopeFactoryTests
{
    private static readonly DateTimeOffset Now = TestMessages.Now;

    private readonly FailureEnvelopeFactory _factory =
        new(new RetryPolicy(Options.Create(new RetryOptions())), new FixedTimeProvider(Now));

    [Fact]
    public void CreateRetryEnvelope_FirstFailure_PreservesOriginAndSchedulesRetryInTwoSeconds()
    {
        var movement = TestMessages.Movement();
        var delivery = FirstDelivery(MessageJson.Serialize(movement));
        var attemptStartedAt = Now.AddMilliseconds(-300);

        var envelope = _factory.CreateRetryEnvelope(new FailedDelivery(delivery, attemptStartedAt, "Mensagem inválida"));

        Assert.Equal(movement.MessageId.ToString(), envelope.MessageId);
        Assert.Equal(Queues.Movements, envelope.OriginalQueue);
        Assert.Equal(Exchanges.Movements, envelope.OriginalExchange);
        Assert.Equal(RoutingKeys.MovementProcess, envelope.OriginalRoutingKey);
        Assert.Equal(1, envelope.Attempt);
        Assert.Equal(6, envelope.MaxAttempts);
        Assert.Equal("Mensagem inválida", envelope.ErrorReason);
        Assert.Equal(attemptStartedAt, envelope.FirstAttemptAt);
        Assert.Equal(Now, envelope.LastAttemptAt);
        Assert.Equal(Now.AddSeconds(2), envelope.NextAttemptAt);
        Assert.False(envelope.PayloadChanged);
        Assert.Equal(movement.MessageId.ToString(), envelope.OriginalPayload.GetProperty("messageId").GetString());
        Assert.Equal(movement.MessageId.ToString(), envelope.CurrentPayload.GetProperty("messageId").GetString());
    }

    [Fact]
    public void CreateRetryEnvelope_RedeliveredByRetryWorker_KeepsOriginFromHeadersAndIncrementsAttempt()
    {
        // 3ª entrega de uma notificação: o Retry Worker a devolveu direto para a fila pelo default exchange,
        // mas o exchange original continua registrado nos cabeçalhos.
        var body = MessageJson.Serialize(TestMessages.Notification());
        var firstAttemptAt = Now.AddSeconds(-7);
        var delivery = new DeliveryContext("notificacao-1", Queues.Notifications, "", Queues.Notifications, body, false,
            new RetryMetadata(2, firstAttemptAt, Exchanges.Notifications, RoutingKeys.Notifications, body));

        var envelope = _factory.CreateRetryEnvelope(new FailedDelivery(delivery, Now, "Falha ao processar notificação"));

        Assert.Equal(3, envelope.Attempt);
        Assert.Equal(Queues.Notifications, envelope.OriginalQueue);
        Assert.Equal(Exchanges.Notifications, envelope.OriginalExchange);
        Assert.Equal(RoutingKeys.Notifications, envelope.OriginalRoutingKey);
        Assert.Equal(firstAttemptAt, envelope.FirstAttemptAt);
        Assert.Equal(Now.AddSeconds(6), envelope.NextAttemptAt);
    }

    [Fact]
    public void CreateRetryEnvelope_PayloadDifferentFromOriginal_FlagsPayloadChanged()
    {
        var original = TestMessages.Movement();
        var corrected = original with { Items = [TestMessages.Item(quantity: 5)] };
        var delivery = FirstDelivery(MessageJson.Serialize(corrected)) with
        {
            Retry = new RetryMetadata(1, Now.AddSeconds(-3), Exchanges.Movements, RoutingKeys.MovementProcess, MessageJson.Serialize(original))
        };

        var envelope = _factory.CreateRetryEnvelope(new FailedDelivery(delivery, Now, "erro"));

        Assert.True(envelope.PayloadChanged);
        Assert.Equal(2m, envelope.OriginalPayload.GetProperty("items")[0].GetProperty("quantity").GetDecimal());
        Assert.Equal(5m, envelope.CurrentPayload.GetProperty("items")[0].GetProperty("quantity").GetDecimal());
    }

    [Fact]
    public void CreateRetryEnvelope_BodyIsNotJson_PreservesRawContentAsText()
    {
        var delivery = FirstDelivery(Encoding.UTF8.GetBytes("conteúdo inválido"));

        var envelope = _factory.CreateRetryEnvelope(new FailedDelivery(delivery, Now, "JSON inválido"));

        Assert.Equal(JsonValueKind.String, envelope.CurrentPayload.ValueKind);
        Assert.Equal("conteúdo inválido", envelope.CurrentPayload.GetString());
    }

    [Fact]
    public void CreateDeadLetterMessage_SixthFailure_ContainsEverythingForManualAnalysis()
    {
        var body = MessageJson.Serialize(TestMessages.Notification());
        var firstAttemptAt = Now.AddSeconds(-30);
        var delivery = new DeliveryContext("notificacao-1", Queues.Notifications, "", Queues.Notifications, body, false,
            new RetryMetadata(5, firstAttemptAt, Exchanges.Notifications, RoutingKeys.Notifications, body));

        var deadLetter = _factory.CreateDeadLetterMessage(new FailedDelivery(delivery, Now, "Falha ao processar notificação"));

        Assert.Equal("notificacao-1", deadLetter.MessageId);
        Assert.Equal(6, deadLetter.Attempts);
        Assert.Equal(Queues.Notifications, deadLetter.OriginalQueue);
        Assert.Equal(Exchanges.Notifications, deadLetter.OriginalExchange);
        Assert.Equal(RoutingKeys.Notifications, deadLetter.OriginalRoutingKey);
        Assert.Equal("Falha ao processar notificação", deadLetter.ErrorReason);
        Assert.Equal(firstAttemptAt, deadLetter.FirstAttemptAt);
        Assert.Equal(Now, deadLetter.LastAttemptAt);
        Assert.Equal(30_000, deadLetter.ProcessingDurationMs);
        Assert.False(deadLetter.PayloadChanged);
        Assert.Equal(JsonValueKind.Object, deadLetter.OriginalPayload.ValueKind);
        Assert.Equal(JsonValueKind.Object, deadLetter.CurrentPayload.ValueKind);
    }

    private static DeliveryContext FirstDelivery(byte[] body) =>
        new(ExtractMessageId(body), Queues.Movements, Exchanges.Movements, RoutingKeys.MovementProcess, body, false, RetryMetadata.None);

    private static string ExtractMessageId(byte[] body)
    {
        try
        {
            return JsonDocument.Parse(body).RootElement.GetProperty("messageId").GetString()!;
        }
        catch (JsonException)
        {
            return "sem-id";
        }
    }
}
