using Commerce.Api.Application;
using Commerce.Api.Models;
using Microsoft.AspNetCore.Mvc;

namespace Commerce.Api.Controllers;

[ApiController]
[Route("api/sales-orders")]
public sealed class SalesOrdersController(IStockMovementRequestService movements) : ControllerBase
{
    /// <summary>Conclui um pedido de venda, gerando uma movimentação de Saída no estoque.</summary>
    /// <remarks>
    /// A API publica a movimentação em commerce.movements e responde imediatamente com 202 Accepted.
    /// A baixa de estoque é feita de forma assíncrona pelo Stock Worker e, com notify = true, os destinatários
    /// são notificados depois do processamento. Use o messageId retornado para acompanhar o fluxo nos logs.
    /// </remarks>
    /// <param name="request">Itens do pedido e dados da notificação.</param>
    /// <param name="idempotencyKey">
    /// UUID opcional. Reenviar a requisição com a mesma chave (por exemplo, após um 503) não aplica a movimentação
    /// duas vezes no estoque.
    /// </param>
    /// <param name="cancellationToken">Cancelamento da requisição.</param>
    /// <response code="202">Movimentação aceita: publicada e confirmada pelo RabbitMQ.</response>
    /// <response code="400">Dados inválidos; nada foi publicado.</response>
    /// <response code="503">RabbitMQ indisponível; a movimentação NÃO foi registrada e deve ser reenviada.</response>
    [HttpPost("complete")]
    [ProducesResponseType<MovementAcceptedResponse>(StatusCodes.Status202Accepted)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status503ServiceUnavailable)]
    public async Task<ActionResult<MovementAcceptedResponse>> Complete(
        MovementRequest request,
        [FromHeader(Name = ApiHeaders.IdempotencyKey)] Guid? idempotencyKey,
        CancellationToken cancellationToken)
    {
        var result = await movements.CompleteSaleOrderAsync(request, idempotencyKey, cancellationToken);
        return this.ToActionResult(result);
    }
}
