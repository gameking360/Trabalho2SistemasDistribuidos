namespace Commerce.Contracts.Validation;

public abstract class NonRetryableMessageException(string message) : Exception(message);
