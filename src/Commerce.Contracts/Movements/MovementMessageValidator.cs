using Commerce.Contracts.Validation;

namespace Commerce.Contracts.Movements;

/// <summary>
/// Regras do contrato de movimentação. Usado pela API (antes de publicar) e pelo Stock Worker (ao consumir),
/// já que a fila pode receber mensagens de outros produtores.
/// </summary>
public static class MovementMessageValidator
{
    public static IReadOnlyList<ValidationError> Validate(MovementMessage? message)
    {
        if (message is null)
            return [new ValidationError("message", "A mensagem de movimentação é obrigatória.")];

        var errors = new List<ValidationError>();

        if (message.MessageId == Guid.Empty)
            errors.Add(new ValidationError("messageId", "O messageId é obrigatório."));

        if (message.RequestedAt == default)
            errors.Add(new ValidationError("requestedAt", "A data/hora da solicitação (requestedAt) é obrigatória."));

        if (!MovementTypes.IsValid(message.MovementType))
            errors.Add(new ValidationError("movementType",
                $"Tipo de movimentação inválido: '{message.MovementType}'. Valores aceitos: {MovementTypes.Entry} ou {MovementTypes.Exit}."));

        ValidateItems(message.Items, errors);
        ValidateRecipients(message.Notify, message.Recipients, errors);

        return errors;
    }

    private static void ValidateItems(IReadOnlyList<MovementItem>? items, List<ValidationError> errors)
    {
        if (items is null || items.Count == 0)
        {
            errors.Add(new ValidationError("items", "A movimentação deve possuir ao menos um item."));
            return;
        }

        for (var index = 0; index < items.Count; index++)
        {
            var field = $"items[{index}]";
            var item = items[index];

            if (item is null)
            {
                errors.Add(new ValidationError(field, "O item não pode ser nulo."));
                continue;
            }

            if (string.IsNullOrWhiteSpace(item.Code))
                errors.Add(new ValidationError($"{field}.code", "O código do item é obrigatório."));

            if (string.IsNullOrWhiteSpace(item.Description))
                errors.Add(new ValidationError($"{field}.description", "A descrição do item é obrigatória."));

            if (item.Quantity <= 0)
                errors.Add(new ValidationError($"{field}.quantity", "A quantidade do item deve ser maior que zero."));

            if (item.Value < 0)
                errors.Add(new ValidationError($"{field}.value", "O valor do item não pode ser negativo."));
        }
    }

    private static void ValidateRecipients(bool notify, IReadOnlyList<string>? recipients, List<ValidationError> errors)
    {
        if (recipients is not null)
        {
            for (var index = 0; index < recipients.Count; index++)
            {
                if (string.IsNullOrWhiteSpace(recipients[index]))
                    errors.Add(new ValidationError($"recipients[{index}]", "O destinatário não pode ser vazio."));
            }
        }

        if (notify && (recipients is null || !recipients.Any(recipient => !string.IsNullOrWhiteSpace(recipient))))
            errors.Add(new ValidationError("recipients", "Informe ao menos um destinatário quando notify = true."));
    }
}
