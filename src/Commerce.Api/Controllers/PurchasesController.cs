using Commerce.Api.Application;
using Commerce.Api.Models;
using Microsoft.AspNetCore.Mvc;

namespace Commerce.Api.Controllers;

[ApiController]
[Route("api/purchases")]
public sealed class PurchasesController(IStockMovementRequestService movements) : ControllerBase
{
    /// <summary>Registra a entrada de itens de uma compra, gerando uma movimentação de Entrada no estoque.</summary>
    /// <remarks>
    /// A API publica a movimentação em commerce.movements e responde imediatamente com 202 Accepted;
    /// o Stock Worker atualiza os saldos de forma assíncrona.
    /// </remarks>
    /// <param name="request">Itens recebidos da compra e dados da notificação.</param>
    /// <param name="idempotencyKey">
    /// UUID opcional. Reenviar a requisição com a mesma chave (por exemplo, após um 503) não aplica a movimentação
    /// duas vezes no estoque.
    /// </param>
    /// <param name="cancellationToken">Cancelamento da requisição.</param>
    /// <response code="202">Movimentação aceita: publicada e confirmada pelo RabbitMQ.</response>
    /// <response code="400">Dados inválidos; nada foi publicado.</response>
    /// <response code="503">RabbitMQ indisponível; a movimentação NÃO foi registrada e deve ser reenviada.</response>
    [HttpPost("entries")]
    [ProducesResponseType<MovementAcceptedResponse>(StatusCodes.Status202Accepted)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status503ServiceUnavailable)]
    public async Task<ActionResult<MovementAcceptedResponse>> RegisterEntry(
        MovementRequest request,
        [FromHeader(Name = ApiHeaders.IdempotencyKey)] Guid? idempotencyKey,
        CancellationToken cancellationToken)
    {
        var result = await movements.RegisterPurchaseEntryAsync(request, idempotencyKey, cancellationToken);
        return this.ToActionResult(result);
    }
}
