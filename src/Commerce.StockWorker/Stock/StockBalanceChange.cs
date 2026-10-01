namespace Commerce.StockWorker.Stock;

public sealed record StockBalanceChange(string ItemCode, decimal PreviousBalance, decimal CurrentBalance);
