using Commerce.Contracts.Messaging;
using Commerce.Infrastructure.Connection;
using Microsoft.Extensions.Options;
using RabbitMQ.Client;

namespace Commerce.RetryWorker.DeadLetters;

/// <summary>
/// Limpeza periódica da DLQ: remove apenas mensagens isoladas há mais tempo que o período de retenção,
/// registrando cada remoção no log.
/// </summary>
public sealed class DeadLetterCleanupService(
    IRabbitMqConnectionProvider connectionProvider,
    IOptions<DeadLetterCleanupOptions> options,
    TimeProvider timeProvider,
    ILogger<DeadLetterCleanupService> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var settings = options.Value;
        if (!settings.Enabled)
        {
            logger.LogInformation("Limpeza periódica da DLQ desabilitada: as mensagens de {Queue} permanecem até a análise manual",
                Queues.DeadLetter);
            return;
        }

        logger.LogInformation("Limpeza periódica da DLQ habilitada: a cada {Interval}, removendo mensagens isoladas há mais de {Retention}",
            settings.Interval, settings.RetentionPeriod);

        using var timer = new PeriodicTimer(settings.Interval, timeProvider);
        while (await WaitForNextRunAsync(timer, stoppingToken))
        {
            try
            {
                var removed = await CleanupAsync(settings.RetentionPeriod, stoppingToken);
                logger.LogInformation("Limpeza da DLQ concluída: {Removed} mensagem(ns) removida(s)", removed);
            }
            catch (Exception exception) when (!stoppingToken.IsCancellationRequested)
            {
                logger.LogWarning("Falha na limpeza da DLQ ({Error}); nova tentativa no próximo ciclo", exception.Message);
            }
        }
    }

    public async Task<int> CleanupAsync(TimeSpan retentionPeriod, CancellationToken cancellationToken)
    {
        var cutoff = timeProvider.GetUtcNow() - retentionPeriod;
        var connection = await connectionProvider.GetConnectionAsync(cancellationToken);

        // As mensagens lidas e mantidas ficam sem ack; ao fechar o canal o RabbitMQ as devolve para a DLQ.
        await using var channel = await connection.CreateChannelAsync(cancellationToken: cancellationToken);

        var pending = (await channel.QueueDeclarePassiveAsync(Queues.DeadLetter, cancellationToken)).MessageCount;
        var removed = 0;

        for (var read = 0; read < pending; read++)
        {
            var message = await channel.BasicGetAsync(Queues.DeadLetter, autoAck: false, cancellationToken);
            if (message is null)
                break;

            var deadLetteredAt = GetDeadLetteredAt(message);
            if (deadLetteredAt is null || deadLetteredAt > cutoff)
                continue;

            await channel.BasicAckAsync(message.DeliveryTag, multiple: false, cancellationToken);
            removed++;

            logger.LogWarning("[{MessageId}] Removida da DLQ pela limpeza periódica (isolada em {DeadLetteredAt:O}, retenção de {Retention})",
                message.BasicProperties.MessageId, deadLetteredAt, retentionPeriod);
        }

        return removed;
    }

    // Mensagens sem timestamp nunca são removidas automaticamente.
    private static DateTimeOffset? GetDeadLetteredAt(BasicGetResult message) =>
        message.BasicProperties.IsTimestampPresent()
            ? DateTimeOffset.FromUnixTimeSeconds(message.BasicProperties.Timestamp.UnixTime)
            : null;

    private static async Task<bool> WaitForNextRunAsync(PeriodicTimer timer, CancellationToken stoppingToken)
    {
        try
        {
            return await timer.WaitForNextTickAsync(stoppingToken);
        }
        catch (OperationCanceledException)
        {
            return false;
        }
    }
}
