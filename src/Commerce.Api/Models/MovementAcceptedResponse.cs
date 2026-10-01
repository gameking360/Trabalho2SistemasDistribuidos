namespace Commerce.Api.Models;

/// <summary>
/// Resposta do 202 Accepted: a movimentação foi publicada no RabbitMQ e será processada de forma assíncrona.
/// </summary>
/// <param name="MessageId">Identificador para rastrear o processamento nos logs de todos os componentes.</param>
/// <param name="MovementType">Entrada ou Saída.</param>
/// <param name="RequestedAt">Data/hora em que a movimentação foi aceita.</param>
public sealed record MovementAcceptedResponse(Guid MessageId, string MovementType, DateTimeOffset RequestedAt)
{
    public string Status { get; } = "Aceita para processamento assíncrono";
}
