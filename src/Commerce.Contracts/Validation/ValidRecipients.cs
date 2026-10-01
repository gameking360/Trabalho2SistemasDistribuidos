namespace Commerce.Contracts.Validation;

public static class ValidRecipients
{
    public static IReadOnlyList<string> Of(IReadOnlyList<string>? recipients) =>
        recipients?.Where(recipient => !string.IsNullOrWhiteSpace(recipient)).ToList() ?? [];
}
