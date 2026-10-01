namespace Commerce.Infrastructure.Topology;

public sealed record QueueDefinition(
    string Name,
    string Exchange,
    string RoutingKey,
    IReadOnlyDictionary<string, object?> Arguments);
