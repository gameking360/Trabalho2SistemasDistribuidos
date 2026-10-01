using System.Globalization;
using Commerce.Api.Application;
using Commerce.Api.Models;
using Microsoft.AspNetCore.Mvc;

namespace Commerce.Api.Controllers;

/// <summary>
/// Traduz o resultado do caso de uso para a resposta HTTP (202, 400 ou 503).
/// </summary>
internal static class MovementRequestResultMapping
{
    private const int RetryAfterSeconds = 5;

    public static ActionResult<MovementAcceptedResponse> ToActionResult(this ControllerBase controller, MovementRequestResult result)
    {
        switch (result.Status)
        {
            case MovementRequestStatus.Accepted:
                return controller.Accepted(result.Response);

            case MovementRequestStatus.Invalid:
                foreach (var error in result.Errors)
                    controller.ModelState.AddModelError(error.Field, error.Message);
                return controller.ValidationProblem(title: "Um ou mais campos da movimentação são inválidos.");

            case MovementRequestStatus.BrokerUnavailable:
                controller.Response.Headers.RetryAfter = RetryAfterSeconds.ToString(CultureInfo.InvariantCulture);
                return controller.Problem(
                    statusCode: StatusCodes.Status503ServiceUnavailable,
                    title: "Mensageria indisponível",
                    detail: $"A movimentação {result.MessageId} não foi registrada porque o RabbitMQ não confirmou o recebimento. " +
                            $"Reenvie a requisição em alguns segundos; com o cabeçalho {ApiHeaders.IdempotencyKey} o reenvio não duplica a movimentação.");

            default:
                throw new ArgumentOutOfRangeException(nameof(result), result.Status, "Status de requisição desconhecido.");
        }
    }
}
