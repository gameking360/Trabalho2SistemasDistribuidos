namespace Commerce.Contracts.Validation;

/// <summary>
/// Lançada quando uma mensagem (ou requisição que dará origem a uma mensagem) não respeita o contrato.
/// </summary>
public sealed class MessageValidationException(IReadOnlyList<ValidationError> errors)
    : Exception($"Mensagem inválida: {string.Join(" ", errors.Select(error => error.Message))}")
{
    public IReadOnlyList<ValidationError> Errors { get; } = errors;
}
