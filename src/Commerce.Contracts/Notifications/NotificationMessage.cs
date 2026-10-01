namespace Commerce.Contracts.Notifications;

/// <summary>
/// Mensagem publicada no exchange fanout <c>commerce.notifications</c>. Cada fila ligada ao exchange
/// (hoje <c>notifications.queue</c>) recebe uma cópia.
/// </summary>
public sealed record NotificationMessage
{
    public Guid MessageId { get; init; }

    public string NotificationType { get; init; } = string.Empty;

    public IReadOnlyList<string> Recipients { get; init; } = [];

    public string Content { get; init; } = string.Empty;

    public DateTimeOffset CreatedAt { get; init; }
}
