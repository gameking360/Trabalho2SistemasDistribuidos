using System.Text.Json;
using Commerce.Contracts.Messaging;
using Commerce.Infrastructure.Configuration;
using Commerce.Infrastructure.Connection;
using Commerce.Infrastructure.Publishing;
using Commerce.Infrastructure.Retry;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Commerce.Infrastructure.Consuming;

/// <summary>
/// Consumidor genérico das filas de trabalho: desserializa, executa o <see cref="IMessageHandler{TMessage}"/>
/// e, em caso de falha, encaminha para retry/DLQ antes de confirmar a mensagem original.
/// </summary>
public sealed class MessageConsumerService<TMessage>(
    ConsumerSettings<TMessage> settings,
    IServiceScopeFactory scopeFactory,
    FailedMessageRouter failureRouter,
    RetryPolicy retryPolicy,
    TimeProvider timeProvider,
    IRabbitMqConnectionProvider connectionProvider,
    IOptions<RabbitMqOptions> options,
    ILoggerFactory loggerFactory)
    : RabbitMqConsumerService(connectionProvider, options, loggerFactory.CreateLogger($"Commerce.Consumers.{settings.Queue}"))
    where TMessage : class
{
    protected override string QueueName => settings.Queue;

    protected override ushort PrefetchCount => settings.PrefetchCount;

    protected override async Task<DeliveryOutcome> HandleDeliveryAsync(DeliveryContext delivery, CancellationToken cancellationToken)
    {
        var startedAt = timeProvider.GetUtcNow();
        Logger.LogInformation("[{MessageId}] Consumida {MessageDescription} de {Queue} (tentativa {Attempt}/{MaxAttempts})",
            delivery.MessageId, settings.MessageDescription, settings.Queue, delivery.Attempt, retryPolicy.MaxAttempts);

        try
        {
            var message = MessageJson.Deserialize<TMessage>(delivery.Body);

            await using var scope = scopeFactory.CreateAsyncScope();
            var handler = scope.ServiceProvider.GetRequiredService<IMessageHandler<TMessage>>();
            await handler.HandleAsync(message, new MessageContext(delivery.Attempt), cancellationToken);
        }
        catch (Exception exception) when (exception is not OperationCanceledException || !cancellationToken.IsCancellationRequested)
        {
            var reason = DescribeFailure(exception);
            Logger.LogWarning("[{MessageId}] Falha na tentativa {Attempt}/{MaxAttempts} de {Queue}: {ErrorType} - {ErrorReason}",
                delivery.MessageId, delivery.Attempt, retryPolicy.MaxAttempts, settings.Queue, exception.GetType().Name, reason);
            Logger.LogDebug(exception, "[{MessageId}] Detalhes da falha", delivery.MessageId);

            return await RouteFailureAsync(new FailedDelivery(delivery, startedAt, reason), cancellationToken);
        }

        Logger.LogInformation("[{MessageId}] Sucesso no processamento da {MessageDescription} (tentativa {Attempt})",
            delivery.MessageId, settings.MessageDescription, delivery.Attempt);
        return DeliveryOutcome.Ack;
    }

    private async Task<DeliveryOutcome> RouteFailureAsync(FailedDelivery failure, CancellationToken cancellationToken)
    {
        try
        {
            // Só confirma a original depois do publisher confirm do envio para retry/DLQ.
            await failureRouter.RouteAsync(failure, cancellationToken);
            return DeliveryOutcome.Ack;
        }
        catch (MessagePublishException exception)
        {
            Logger.LogError(exception,
                "[{MessageId}] Não foi possível encaminhar a falha para retry/DLQ; a mensagem volta para {Queue} em {DelaySeconds}s",
                failure.Delivery.MessageId, settings.Queue, RetryDelay.TotalSeconds);

            // Evita um loop de reentregas enquanto o broker se recupera.
            await Task.Delay(RetryDelay, cancellationToken);
            return DeliveryOutcome.Requeue;
        }
    }

    private static string DescribeFailure(Exception exception) => exception switch
    {
        JsonException => $"Mensagem inválida: JSON malformado ou incompatível com o contrato ({exception.Message})",
        _ => exception.Message
    };
}
