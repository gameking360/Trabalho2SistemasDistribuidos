namespace Commerce.Contracts.Messaging;

public static class Exchanges
{
    public const string Movements = "commerce.movements";
    public const string Notifications = "commerce.notifications";
    public const string Retry = "commerce.retry";
    public const string DeadLetter = "commerce.dlx";
}
