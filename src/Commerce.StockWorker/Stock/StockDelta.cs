namespace Commerce.StockWorker.Stock;

/// <param name="ItemCode">Código do item.</param>
/// <param name="Quantity">Variação do saldo: positiva na entrada, negativa na saída.</param>
public sealed record StockDelta(string ItemCode, decimal Quantity);
