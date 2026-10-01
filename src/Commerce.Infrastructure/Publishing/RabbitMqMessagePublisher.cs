using Commerce.Infrastructure.Configuration;
using Commerce.Infrastructure.Connection;
using Commerce.Infrastructure.Topology;
using Microsoft.Extensions.Options;
using RabbitMQ.Client;
using RabbitMQ.Client.Exceptions;

namespace Commerce.Infrastructure.Publishing;

/// <summary>
/// Único ponto de publicação do sistema (API, Stock Worker, roteamento de falhas e Retry Worker).
/// Usa um canal compartilhado com publisher confirms; o client serializa os envios e cada chamada aguarda
/// apenas a confirmação da sua própria mensagem.
/// </summary>
public sealed class RabbitMqMessagePublisher(
    IRabbitMqConnectionProvider connectionProvider,
    IOptions<RabbitMqOptions> options) : IMessagePublisher, IAsyncDisposable
{
    private const string JsonContentType = "application/json";

    private readonly SemaphoreSlim _channelLock = new(1, 1);
    private IChannel? _channel;

    public async Task PublishAsync(OutgoingMessage message, CancellationToken cancellationToken = default)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(options.Value.PublishConfirmTimeout);

        try
        {
            var channel = await GetChannelAsync(timeout.Token);

            // mandatory: mensagem sem fila de destino volta do broker (PublishException) em vez de ser descartada.
            await channel.BasicPublishAsync(message.Exchange, message.RoutingKey, mandatory: true,
                CreateProperties(message), message.Body, timeout.Token);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            throw new MessagePublishException(message,
                $"o broker não confirmou o recebimento em {options.Value.PublishConfirmTimeout.TotalSeconds}s");
        }
        catch (PublishException exception)
        {
            var reason = exception.IsReturn
                ? "nenhuma fila está ligada ao exchange/routing key (mensagem devolvida pelo broker)"
                : "o broker recusou a mensagem (nack)";
            throw new MessagePublishException(message, reason, exception);
        }
        catch (Exception exception) when (exception is not OperationCanceledException and not MessagePublishException)
        {
            throw new MessagePublishException(message, $"RabbitMQ indisponível ({exception.Message})", exception);
        }
    }

    private BasicProperties CreateProperties(OutgoingMessage message) => new()
    {
        Persistent = true,
        MessageId = message.MessageId,
        ContentType = JsonContentType,
        ContentEncoding = "utf-8",
        AppId = options.Value.ClientName,
        Timestamp = new AmqpTimestamp(DateTimeOffset.UtcNow.ToUnixTimeSeconds()),
        Headers = message.Headers is null ? null : new Dictionary<string, object?>(message.Headers)
    };

    private async Task<IChannel> GetChannelAsync(CancellationToken cancellationToken)
    {
        if (_channel is { IsOpen: true })
            return _channel;

        await _channelLock.WaitAsync(cancellationToken);
        try
        {
            if (_channel is { IsOpen: true })
                return _channel;

            var connection = await connectionProvider.GetConnectionAsync(cancellationToken);

            // Canal fechado com a conexão aberta não é recuperado automaticamente: cria outro.
            if (_channel is not null && connection.IsOpen)
            {
                await _channel.DisposeAsync();
                _channel = null;
            }

            if (_channel is null)
            {
                var channel = await connection.CreateChannelAsync(
                    new CreateChannelOptions(publisherConfirmationsEnabled: true, publisherConfirmationTrackingEnabled: true),
                    cancellationToken);

                await MessagingTopology.DeclareAsync(channel, cancellationToken);
                _channel = channel;
            }

            return _channel;
        }
        finally
        {
            _channelLock.Release();
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (_channel is not null)
            await _channel.DisposeAsync();

        _channelLock.Dispose();
    }
}
