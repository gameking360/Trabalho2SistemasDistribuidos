namespace Commerce.Infrastructure.Consuming;

/// <param name="MessageId">Identificador usado para rastrear a mensagem nos logs.</param>
/// <param name="Attempt">Tentativa atual (1 = primeira entrega, 2+ = reprocessamento via retry).</param>
public sealed record MessageContext(string MessageId, int Attempt);
