using System.Diagnostics.CodeAnalysis;
using System.Text.Json;
using Commerce.Contracts.Failures;
using Commerce.Contracts.Messaging;
using Commerce.Infrastructure.Configuration;
using Commerce.Infrastructure.Connection;
using Commerce.Infrastructure.Consuming;
using Commerce.Infrastructure.Publishing;
using Commerce.Infrastructure.Retry;
using Microsoft.Extensions.Options;

namespace Commerce.RetryWorker.Messaging;

/// <summary>
/// Consome a retry.queue, aguarda o horário definido em cada envelope (nextAttemptAt) e devolve a mensagem
/// ao fluxo de origem. A mensagem de retry só recebe ack depois que a republicação foi confirmada pelo broker.
/// </summary>
public sealed class RetryQueueConsumer(
    IMessagePublisher publisher,
    TimeProvider timeProvider,
    IRabbitMqConnectionProvider connectionProvider,
    IOptions<RabbitMqOptions> options,
    ILogger<RetryQueueConsumer> logger)
    : RabbitMqConsumerService(connectionProvider, options, logger)
{
    // Esperas em paralelo: uma de 10s não atrasa uma de 2s. Sem ack durante a espera, nada se perde se o worker cair.
    private const ushort MaxPendingRetries = 50;

    protected override string QueueName => Queues.Retry;

    protected override ushort PrefetchCount => MaxPendingRetries;

    protected override ushort ConcurrentDeliveries => MaxPendingRetries;

    protected override async Task<DeliveryOutcome> HandleDeliveryAsync(DeliveryContext delivery, CancellationToken cancellationToken)
    {
        if (!TryReadEnvelope(delivery, out var envelope))
            return DeliveryOutcome.Reject;

        var wait = envelope.NextAttemptAt - timeProvider.GetUtcNow();
        Logger.LogInformation(
            "[{MessageId}] Retry recebido de {OriginalQueue} (falha {Attempt}/{MaxAttempts}: {ErrorReason}); aguardando {WaitMs} ms",
            envelope.MessageId, envelope.OriginalQueue, envelope.Attempt, envelope.MaxAttempts, envelope.ErrorReason,
            Math.Max(0, (long)wait.TotalMilliseconds));

        if (wait > TimeSpan.Zero)
            await Task.Delay(wait, timeProvider, cancellationToken);

        var message = RetryRoute.CreateRepublishMessage(envelope);
        try
        {
            await publisher.PublishAsync(message, cancellationToken);
        }
        catch (MessagePublishException exception)
        {
            Logger.LogError(exception, "[{MessageId}] Falha ao devolver a mensagem para {OriginalQueue}; ela permanece na {RetryQueue}",
                envelope.MessageId, envelope.OriginalQueue, Queues.Retry);

            await Task.Delay(RetryDelay, cancellationToken);
            return DeliveryOutcome.Requeue;
        }

        Logger.LogInformation(
            "[{MessageId}] Reprocessada: devolvida para {OriginalQueue} via exchange '{Exchange}' (routing key '{RoutingKey}') para a tentativa {NextAttempt}/{MaxAttempts}",
            envelope.MessageId, envelope.OriginalQueue, message.Exchange, message.RoutingKey, envelope.Attempt + 1, envelope.MaxAttempts);

        return DeliveryOutcome.Ack;
    }

    private bool TryReadEnvelope(DeliveryContext delivery, [NotNullWhen(true)] out RetryEnvelope? envelope)
    {
        try
        {
            envelope = MessageJson.Deserialize<RetryEnvelope>(delivery.Body);
            if (!string.IsNullOrWhiteSpace(envelope.OriginalQueue) && envelope.CurrentPayload.ValueKind != JsonValueKind.Undefined)
                return true;
        }
        catch (JsonException)
        {
            // Tratado abaixo como envelope inválido.
        }

        // Sem origem não há para onde devolver: a rejeição leva a mensagem à DLQ pelo x-dead-letter-exchange.
        Logger.LogError("[{MessageId}] Envelope de retry inválido (sem fila de origem ou payload); enviado para a DLQ",
            delivery.MessageId);
        envelope = null;
        return false;
    }
}
