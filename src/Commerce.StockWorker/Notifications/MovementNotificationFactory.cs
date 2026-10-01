using System.Globalization;
using Commerce.Contracts.Movements;
using Commerce.Contracts.Notifications;

namespace Commerce.StockWorker.Notifications;

public static class MovementNotificationFactory
{
    public const string NotificationType = "MovimentacaoEstoqueProcessada";

    // Formato numérico brasileiro sem depender de cultura instalada (ICU) no container.
    private static readonly NumberFormatInfo BrazilianNumbers = new()
    {
        NumberDecimalSeparator = ",",
        NumberGroupSeparator = "."
    };

    public static NotificationMessage Create(MovementMessage movement, DateTimeOffset createdAt) => new()
    {
        MessageId = Guid.NewGuid(),
        NotificationType = NotificationType,
        Recipients = movement.Recipients,
        Content = BuildContent(movement),
        CreatedAt = createdAt
    };

    private static string BuildContent(MovementMessage movement)
    {
        var items = string.Join("; ", movement.Items.Select(item =>
            $"{item.Code} ({item.Description}) x {item.Quantity.ToString("0.###", BrazilianNumbers)}"));

        var total = movement.Items.Sum(item => item.Quantity * item.Value);

        return $"Movimentação de {movement.MovementType} processada no estoque. " +
               $"Itens: {items}. Valor total: R$ {total.ToString("N2", BrazilianNumbers)}.";
    }
}
