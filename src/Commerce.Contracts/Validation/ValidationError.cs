namespace Commerce.Contracts.Validation;

public sealed record ValidationError(string Field, string Message);
