namespace Commerce.Infrastructure.Consuming;

/// <param name="Attempt">Tentativa atual (1 = primeira entrega, 2+ = reprocessamento via retry).</param>
public sealed record MessageContext(int Attempt);
