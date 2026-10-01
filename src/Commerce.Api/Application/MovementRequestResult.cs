using Commerce.Api.Models;
using Commerce.Contracts.Validation;

namespace Commerce.Api.Application;

public sealed record MovementRequestResult(
    MovementRequestStatus Status,
    Guid MessageId,
    MovementAcceptedResponse? Response,
    IReadOnlyList<ValidationError> Errors)
{
    public static MovementRequestResult Accepted(MovementAcceptedResponse response) =>
        new(MovementRequestStatus.Accepted, response.MessageId, response, []);

    public static MovementRequestResult Invalid(Guid messageId, IReadOnlyList<ValidationError> errors) =>
        new(MovementRequestStatus.Invalid, messageId, null, errors);

    public static MovementRequestResult BrokerUnavailable(Guid messageId) =>
        new(MovementRequestStatus.BrokerUnavailable, messageId, null, []);
}
