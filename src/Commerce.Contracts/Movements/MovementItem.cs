namespace Commerce.Contracts.Movements;

public sealed record MovementItem
{
    /// <summary>Código (SKU) do item.</summary>
    /// <example>SKU-001</example>
    public string Code { get; init; } = string.Empty;

    /// <summary>Descrição do item.</summary>
    /// <example>Camiseta básica</example>
    public string Description { get; init; } = string.Empty;

    /// <summary>Quantidade movimentada. Deve ser maior que zero.</summary>
    /// <example>2</example>
    public decimal Quantity { get; init; }

    /// <summary>Valor unitário do item.</summary>
    /// <example>59.90</example>
    public decimal Value { get; init; }
}
