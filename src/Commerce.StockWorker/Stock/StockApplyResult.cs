namespace Commerce.StockWorker.Stock;

public sealed record StockApplyResult(bool AlreadyApplied, IReadOnlyList<StockBalanceChange> Changes)
{
    public static StockApplyResult Duplicate { get; } = new(true, []);
}
