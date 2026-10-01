using Commerce.Api.Models;

namespace Commerce.Api.Application;

/// <summary>
/// Casos de uso produtores: transformam a requisição em uma movimentação e a publicam no RabbitMQ.
/// </summary>
/// <remarks>
/// A chave de idempotência, quando informada, vira o messageId da movimentação: reenviar a mesma requisição
/// (por exemplo, após um 503) não aplica a movimentação duas vezes no estoque.
/// </remarks>
public interface IStockMovementRequestService
{
    /// <summary>Conclusão de pedido de venda: gera uma movimentação de Saída.</summary>
    Task<MovementRequestResult> CompleteSaleOrderAsync(MovementRequest request, Guid? idempotencyKey, CancellationToken cancellationToken);

    /// <summary>Entrada de itens de compra: gera uma movimentação de Entrada.</summary>
    Task<MovementRequestResult> RegisterPurchaseEntryAsync(MovementRequest request, Guid? idempotencyKey, CancellationToken cancellationToken);
}
