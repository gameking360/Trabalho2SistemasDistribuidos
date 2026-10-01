namespace Commerce.StockWorker.Stock;

/// <summary>
/// Estoque em memória para a demonstração. Em produção, saldos e o registro das movimentações aplicadas
/// ficariam no mesmo banco de dados e seriam gravados na mesma transação.
/// </summary>
public sealed class InMemoryStockRepository : IStockRepository
{
    private readonly object _sync = new();
    private readonly Dictionary<string, decimal> _balances = new(StringComparer.Ordinal);
    private readonly HashSet<Guid> _appliedMovements = [];

    public StockApplyResult Apply(Guid movementId, IReadOnlyList<StockDelta> deltas)
    {
        lock (_sync)
        {
            // Entrega "pelo menos uma vez": o messageId evita aplicar uma reentrega duas vezes.
            if (_appliedMovements.Contains(movementId))
                return StockApplyResult.Duplicate;

            var aggregatedDeltas = deltas
                .GroupBy(delta => delta.ItemCode, StringComparer.Ordinal)
                .Select(group => new StockDelta(group.Key, group.Sum(delta => delta.Quantity)))
                .ToList();

            foreach (var delta in aggregatedDeltas)
            {
                var previous = _balances.GetValueOrDefault(delta.ItemCode);
                var current = previous + delta.Quantity;
                if (current < 0)
                    throw new InsufficientStockException(delta.ItemCode, previous, -delta.Quantity);
            }

            _appliedMovements.Add(movementId);

            var changes = new List<StockBalanceChange>(aggregatedDeltas.Count);
            foreach (var delta in aggregatedDeltas)
            {
                var previous = _balances.GetValueOrDefault(delta.ItemCode);
                var current = previous + delta.Quantity;
                _balances[delta.ItemCode] = current;
                changes.Add(new StockBalanceChange(delta.ItemCode, previous, current));
            }

            return new StockApplyResult(false, changes);
        }
    }

    public decimal GetBalance(string itemCode)
    {
        lock (_sync)
        {
            return _balances.GetValueOrDefault(itemCode);
        }
    }
}
