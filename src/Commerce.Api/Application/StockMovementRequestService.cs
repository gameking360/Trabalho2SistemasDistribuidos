using Commerce.Api.Models;
using Commerce.Contracts.Messaging;
using Commerce.Contracts.Movements;
using Commerce.Infrastructure.Publishing;

namespace Commerce.Api.Application;

public sealed class StockMovementRequestService(
    IMessagePublisher publisher,
    TimeProvider timeProvider,
    ILogger<StockMovementRequestService> logger) : IStockMovementRequestService
{
    public Task<MovementRequestResult> CompleteSaleOrderAsync(MovementRequest request, Guid? idempotencyKey, CancellationToken cancellationToken) =>
        PublishAsync(MovementTypes.Exit, request, idempotencyKey, cancellationToken);

    public Task<MovementRequestResult> RegisterPurchaseEntryAsync(MovementRequest request, Guid? idempotencyKey, CancellationToken cancellationToken) =>
        PublishAsync(MovementTypes.Entry, request, idempotencyKey, cancellationToken);

    private async Task<MovementRequestResult> PublishAsync(string movementType, MovementRequest request, Guid? idempotencyKey,
        CancellationToken cancellationToken)
    {
        var movement = new MovementMessage
        {
            MessageId = idempotencyKey ?? Guid.NewGuid(),
            RequestedAt = timeProvider.GetUtcNow(),
            MovementType = movementType,
            Notify = request.Notify,
            Recipients = request.Recipients ?? [],
            Items = request.Items ?? []
        };

        var errors = MovementMessageValidator.Validate(movement);
        if (errors.Count > 0)
            return MovementRequestResult.Invalid(movement.MessageId, errors);

        // O 202 só é devolvido após o publisher confirm (mensagem persistida no broker).
        try
        {
            await publisher.PublishAsync(
                OutgoingMessage.Json(Exchanges.Movements, RoutingKeys.MovementProcess, movement.MessageId.ToString(), movement),
                cancellationToken);
        }
        catch (MessagePublishException exception)
        {
            logger.LogError("[{MessageId}] Movimentação NÃO publicada; o cliente deve reenviar a requisição. {Reason}",
                movement.MessageId, exception.Message);
            return MovementRequestResult.BrokerUnavailable(movement.MessageId);
        }

        logger.LogInformation("[{MessageId}] Publicada movimentação de {MovementType} com {ItemCount} item(ns) em {Exchange} (routing key {RoutingKey})",
            movement.MessageId, movement.MovementType, movement.Items.Count, Exchanges.Movements, RoutingKeys.MovementProcess);

        return MovementRequestResult.Accepted(
            new MovementAcceptedResponse(movement.MessageId, movement.MovementType, movement.RequestedAt));
    }
}
