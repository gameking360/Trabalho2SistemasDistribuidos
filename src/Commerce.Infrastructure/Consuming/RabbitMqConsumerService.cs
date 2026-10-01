using Commerce.Infrastructure.Configuration;
using Commerce.Infrastructure.Connection;
using Commerce.Infrastructure.Topology;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;

namespace Commerce.Infrastructure.Consuming;

/// <summary>
/// Base dos consumidores: conecta (tentando novamente enquanto o broker estiver fora), declara a topologia,
/// aplica o prefetch e consome com acknowledgement manual. As classes derivadas só decidem o desfecho de cada
/// entrega; o ack acontece apenas depois desse desfecho.
/// </summary>
public abstract class RabbitMqConsumerService(
    IRabbitMqConnectionProvider connectionProvider,
    IOptions<RabbitMqOptions> options,
    ILogger logger) : BackgroundService
{
    protected ILogger Logger { get; } = logger;

    protected TimeSpan RetryDelay => options.Value.ReconnectDelay;

    protected abstract string QueueName { get; }

    protected abstract ushort PrefetchCount { get; }

    /// <summary>Quantas entregas deste canal podem ser processadas em paralelo (1 = sequencial).</summary>
    protected virtual ushort ConcurrentDeliveries => 1;

    protected abstract Task<DeliveryOutcome> HandleDeliveryAsync(DeliveryContext delivery, CancellationToken cancellationToken);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await ConsumeUntilChannelClosesAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                return;
            }
            catch (Exception exception)
            {
                Logger.LogWarning("Não foi possível consumir {Queue}: {Error}. Nova tentativa em {DelaySeconds}s",
                    QueueName, exception.Message, RetryDelay.TotalSeconds);
                await WaitAsync(RetryDelay, stoppingToken);
            }
        }
    }

    private async Task ConsumeUntilChannelClosesAsync(CancellationToken stoppingToken)
    {
        var connection = await connectionProvider.GetConnectionAsync(stoppingToken);

        await using var channel = await connection.CreateChannelAsync(
            new CreateChannelOptions(
                publisherConfirmationsEnabled: false,
                publisherConfirmationTrackingEnabled: false,
                consumerDispatchConcurrency: ConcurrentDeliveries),
            stoppingToken);

        await MessagingTopology.DeclareAsync(channel, stoppingToken);

        // O broker entrega no máximo PrefetchCount mensagens sem ack a este consumidor.
        await channel.BasicQosAsync(prefetchSize: 0, prefetchCount: PrefetchCount, global: false, stoppingToken);

        var channelClosed = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        channel.ChannelShutdownAsync += (_, _) =>
        {
            // Quedas de conexão são recuperadas pelo client (canal e consumidor incluídos). Só recriamos o canal
            // quando ele é fechado isoladamente, por exemplo depois de um erro de protocolo.
            if (connection.IsOpen)
                channelClosed.TrySetResult();
            return Task.CompletedTask;
        };

        var consumer = new AsyncEventingBasicConsumer(channel);
        consumer.ReceivedAsync += (_, args) => OnDeliveryAsync(channel, args, stoppingToken);

        await channel.BasicConsumeAsync(QueueName, autoAck: false, consumer, stoppingToken);
        Logger.LogInformation("Consumindo {Queue} com ack manual (prefetch {Prefetch}, processamento paralelo {Concurrency})",
            QueueName, PrefetchCount, ConcurrentDeliveries);

        await channelClosed.Task.WaitAsync(stoppingToken);
        Logger.LogWarning("Canal de consumo de {Queue} foi fechado; recriando", QueueName);
    }

    private async Task OnDeliveryAsync(IChannel channel, BasicDeliverEventArgs args, CancellationToken stoppingToken)
    {
        var delivery = DeliveryContext.From(args, QueueName);

        try
        {
            var outcome = await HandleDeliveryAsync(delivery, stoppingToken);
            await SettleAsync(channel, args.DeliveryTag, outcome);
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            // Sem ack: o RabbitMQ devolve a mensagem para a fila quando o canal é fechado no desligamento.
            Logger.LogInformation("[{MessageId}] Processamento interrompido pelo desligamento; a mensagem permanece em {Queue}",
                delivery.MessageId, QueueName);
        }
        catch (Exception exception)
        {
            Logger.LogError(exception, "[{MessageId}] Erro inesperado ao finalizar a entrega; devolvendo para {Queue}",
                delivery.MessageId, QueueName);
            await TryRequeueAsync(channel, args.DeliveryTag);
        }
    }

    private static async Task SettleAsync(IChannel channel, ulong deliveryTag, DeliveryOutcome outcome)
    {
        switch (outcome)
        {
            case DeliveryOutcome.Ack:
                await channel.BasicAckAsync(deliveryTag, multiple: false);
                break;
            case DeliveryOutcome.Requeue:
                await channel.BasicNackAsync(deliveryTag, multiple: false, requeue: true);
                break;
            case DeliveryOutcome.Reject:
                await channel.BasicNackAsync(deliveryTag, multiple: false, requeue: false);
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(outcome), outcome, null);
        }
    }

    private async Task TryRequeueAsync(IChannel channel, ulong deliveryTag)
    {
        try
        {
            await channel.BasicNackAsync(deliveryTag, multiple: false, requeue: true);
        }
        catch (Exception exception)
        {
            // Com o canal fechado a mensagem já volta sozinha para a fila; nada é perdido.
            Logger.LogWarning("Não foi possível devolver a entrega {DeliveryTag} para {Queue}: {Error}",
                deliveryTag, QueueName, exception.Message);
        }
    }

    protected static async Task WaitAsync(TimeSpan delay, CancellationToken cancellationToken)
    {
        try
        {
            await Task.Delay(delay, cancellationToken);
        }
        catch (OperationCanceledException)
        {
            // Desligamento solicitado durante a espera.
        }
    }
}
