using Commerce.Contracts.Messaging;
using RabbitMQ.Client;

namespace Commerce.Infrastructure.Topology;

/// <summary>
/// Fonte única da topologia (exchanges, filas e bindings), declarada de forma idempotente por todos os componentes.
/// </summary>
public static class MessagingTopology
{
    public static IReadOnlyList<ExchangeDefinition> ExchangeDefinitions { get; } =
    [
        new(Exchanges.Movements, ExchangeType.Direct),
        new(Exchanges.Notifications, ExchangeType.Fanout),
        new(Exchanges.Retry, ExchangeType.Direct),
        new(Exchanges.DeadLetter, ExchangeType.Direct)
    ];

    public static IReadOnlyList<QueueDefinition> QueueDefinitions { get; } =
    [
        new(Queues.Movements, Exchanges.Movements, RoutingKeys.MovementProcess, WorkQueueArguments(singleActiveConsumer: true)),
        new(Queues.Notifications, Exchanges.Notifications, RoutingKeys.Notifications, WorkQueueArguments()),
        new(Queues.Retry, Exchanges.Retry, RoutingKeys.Retry, WorkQueueArguments()),
        new(Queues.DeadLetter, Exchanges.DeadLetter, RoutingKeys.DeadLetter, new Dictionary<string, object?>())
    ];

    public static bool IsFanout(string exchange) =>
        ExchangeDefinitions.Any(definition => definition.Name == exchange && definition.Type == ExchangeType.Fanout);

    public static async Task DeclareAsync(IChannel channel, CancellationToken cancellationToken = default)
    {
        foreach (var exchange in ExchangeDefinitions)
        {
            await channel.ExchangeDeclareAsync(exchange.Name, exchange.Type, durable: true, autoDelete: false,
                cancellationToken: cancellationToken);
        }

        foreach (var queue in QueueDefinitions)
        {
            await channel.QueueDeclareAsync(queue.Name, durable: true, exclusive: false, autoDelete: false,
                arguments: new Dictionary<string, object?>(queue.Arguments), cancellationToken: cancellationToken);

            await channel.QueueBindAsync(queue.Name, queue.Exchange, queue.RoutingKey,
                cancellationToken: cancellationToken);
        }
    }

    private static Dictionary<string, object?> WorkQueueArguments(bool singleActiveConsumer = false)
    {
        var arguments = new Dictionary<string, object?>
        {
            // Rede de segurança: mensagem rejeitada sem requeue vai para a DLQ em vez de ser descartada.
            ["x-dead-letter-exchange"] = Exchanges.DeadLetter,
            ["x-dead-letter-routing-key"] = RoutingKeys.DeadLetter
        };

        // Instâncias extras ficam em standby: nunca consomem em paralelo, preservando a ordem.
        if (singleActiveConsumer)
            arguments["x-single-active-consumer"] = true;

        return arguments;
    }
}
