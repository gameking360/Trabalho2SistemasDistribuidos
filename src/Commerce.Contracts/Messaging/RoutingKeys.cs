namespace Commerce.Contracts.Messaging;

public static class RoutingKeys
{
    public const string MovementProcess = "movement.process";

    /// <summary>
    /// Exchanges fanout ignoram a routing key: toda fila ligada ao exchange recebe uma cópia da mensagem.
    /// </summary>
    public const string Notifications = "";

    public const string Retry = "message.retry";
    public const string DeadLetter = "message.dead-letter";
}
