namespace Commerce.StockWorker.Stock;

public sealed class StockOptions
{
    public const string SectionName = "Stock";

    /// <summary>
    /// Duração simulada da rotina de estoque (acesso a banco, cálculos, integrações). É esse custo que a API
    /// evita ao publicar a movimentação em vez de processá-la durante a requisição.
    /// </summary>
    public TimeSpan SimulatedProcessingTime { get; set; } = TimeSpan.FromMilliseconds(500);
}
