namespace Commerce.Contracts.Messaging;

public static class Queues
{
    public const string Movements = "movements.queue";
    public const string Notifications = "notifications.queue";
    public const string Retry = "retry.queue";
    public const string DeadLetter = "dead-letter.queue";
}
