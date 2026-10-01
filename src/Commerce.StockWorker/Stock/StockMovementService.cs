using Commerce.Contracts.Movements;
using Microsoft.Extensions.Options;

namespace Commerce.StockWorker.Stock;

/// <summary>
/// Rotina de estoque: entradas somam e saídas subtraem a quantidade de cada item.
/// </summary>
public sealed class StockMovementService(
    IStockRepository repository,
    IOptions<StockOptions> options,
    ILogger<StockMovementService> logger)
{
    public async Task ApplyAsync(MovementMessage movement, CancellationToken cancellationToken)
    {
        await Task.Delay(options.Value.SimulatedProcessingTime, cancellationToken);

        var direction = MovementTypes.IsEntry(movement.MovementType) ? 1 : -1;
        var deltas = movement.Items
            .Select(item => new StockDelta(item.Code.Trim(), direction * item.Quantity))
            .ToList();

        var result = repository.Apply(movement.MessageId, deltas);
        if (result.AlreadyApplied)
        {
            logger.LogWarning("[{MessageId}] Movimentação já aplicada anteriormente (reentrega); saldos mantidos",
                movement.MessageId);
            return;
        }

        foreach (var change in result.Changes)
        {
            logger.LogInformation("[{MessageId}] Estoque do item {ItemCode}: {PreviousBalance} -> {CurrentBalance}",
                movement.MessageId, change.ItemCode, change.PreviousBalance, change.CurrentBalance);
        }

        logger.LogInformation("[{MessageId}] Movimentação de {MovementType} aplicada: {ItemCount} item(ns), valor total {TotalValue}",
            movement.MessageId, movement.MovementType, movement.Items.Count,
            movement.Items.Sum(item => item.Quantity * item.Value));
    }
}
