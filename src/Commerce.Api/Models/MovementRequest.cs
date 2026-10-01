using Commerce.Contracts.Movements;

namespace Commerce.Api.Models;

/// <summary>
/// Dados de uma movimentação recebida pela API. O tipo (Entrada ou Saída) é definido pelo endpoint chamado.
/// </summary>
public sealed record MovementRequest
{
    /// <summary>Quando verdadeiro, os destinatários são notificados depois que o estoque for atualizado.</summary>
    /// <example>true</example>
    public bool Notify { get; init; }

    /// <summary>Destinatários da notificação (obrigatório quando notify = true).</summary>
    /// <example>["cliente@exemplo.com"]</example>
    public IReadOnlyList<string>? Recipients { get; init; }

    /// <summary>Itens movimentados.</summary>
    public IReadOnlyList<MovementItem>? Items { get; init; }
}
