using Commerce.Contracts.Movements;
using Commerce.Contracts.Notifications;

namespace Commerce.Tests.Support;

public static class TestMessages
{
    public static readonly DateTimeOffset Now = new(2026, 9, 30, 12, 0, 0, TimeSpan.Zero);

    public static MovementItem Item(string code = "SKU-001", decimal quantity = 2, decimal value = 59.90m) => new()
    {
        Code = code,
        Description = "Camiseta básica",
        Quantity = quantity,
        Value = value
    };

    public static MovementMessage Movement(
        string movementType = MovementTypes.Exit,
        bool notify = false,
        IReadOnlyList<string>? recipients = null,
        IReadOnlyList<MovementItem>? items = null) => new()
    {
        MessageId = Guid.NewGuid(),
        RequestedAt = Now,
        MovementType = movementType,
        Notify = notify,
        Recipients = recipients ?? [],
        Items = items ?? [Item()]
    };

    public static NotificationMessage Notification(params string[] recipients) => new()
    {
        MessageId = Guid.NewGuid(),
        NotificationType = "MovimentacaoEstoqueProcessada",
        Recipients = recipients.Length > 0 ? recipients : ["cliente@exemplo.com"],
        Content = "Movimentação de Saída processada no estoque.",
        CreatedAt = Now
    };
}
