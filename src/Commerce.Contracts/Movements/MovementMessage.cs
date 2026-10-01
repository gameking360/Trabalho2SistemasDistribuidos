namespace Commerce.Contracts.Movements;

/// <summary>
/// Mensagem de movimentação de estoque publicada em <c>commerce.movements</c> (routing key <c>movement.process</c>).
/// </summary>
public sealed record MovementMessage
{
    public Guid MessageId { get; init; }

    public DateTimeOffset RequestedAt { get; init; }

    /// <summary>"Entrada" ou "Saída" (ver <see cref="MovementTypes"/>).</summary>
    public string MovementType { get; init; } = string.Empty;

    /// <summary>Quando verdadeiro, uma notificação é publicada após o processamento do estoque.</summary>
    public bool Notify { get; init; }

    /// <summary>Destinatários da notificação. Obrigatório quando <see cref="Notify"/> é verdadeiro.</summary>
    public IReadOnlyList<string> Recipients { get; init; } = [];

    public IReadOnlyList<MovementItem> Items { get; init; } = [];
}
