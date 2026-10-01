using Commerce.Contracts.Validation;

namespace Commerce.Contracts.Notifications;

public static class NotificationMessageValidator
{
    public static IReadOnlyList<ValidationError> Validate(NotificationMessage? message)
    {
        if (message is null)
            return [new ValidationError("message", "A mensagem de notificação é obrigatória.")];

        var errors = new List<ValidationError>();

        if (message.MessageId == Guid.Empty)
            errors.Add(new ValidationError("messageId", "O messageId é obrigatório."));

        if (string.IsNullOrWhiteSpace(message.NotificationType))
            errors.Add(new ValidationError("notificationType", "O tipo da notificação é obrigatório."));

        if (ValidRecipients.Of(message.Recipients).Count == 0)
            errors.Add(new ValidationError("recipients", "A notificação deve possuir ao menos um destinatário."));

        if (string.IsNullOrWhiteSpace(message.Content))
            errors.Add(new ValidationError("content", "O conteúdo da notificação é obrigatório."));

        return errors;
    }
}
