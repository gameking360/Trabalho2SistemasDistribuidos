namespace Commerce.StockWorker.Stock;

public interface IStockRepository
{
    /// <summary>
    /// Aplica as variações de saldo de uma movimentação de forma atômica. Uma movimentação já aplicada
    /// (mesmo messageId) não altera os saldos novamente.
    /// </summary>
    StockApplyResult Apply(Guid movementId, IReadOnlyList<StockDelta> deltas);

    decimal GetBalance(string itemCode);
}
