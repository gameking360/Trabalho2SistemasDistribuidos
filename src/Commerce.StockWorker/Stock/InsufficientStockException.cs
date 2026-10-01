using Commerce.Contracts.Validation;

namespace Commerce.StockWorker.Stock;

public sealed class InsufficientStockException(string itemCode, decimal balance, decimal requestedQuantity)
    : NonRetryableMessageException(
        $"Estoque insuficiente para o item {itemCode}: saldo {balance}, quantidade solicitada {requestedQuantity}.")
{
    public string ItemCode { get; } = itemCode;

    public decimal Balance { get; } = balance;

    public decimal RequestedQuantity { get; } = requestedQuantity;
}
