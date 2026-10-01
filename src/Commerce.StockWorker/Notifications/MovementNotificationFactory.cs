using System.Globalization;
using Commerce.Contracts.Movements;
using Commerce.Contracts.Notifications;
using Commerce.Contracts.Validation;

namespace Commerce.StockWorker.Notifications;

public static class MovementNotificationFactory
{
    public const string ProcessedType = "MovimentacaoEstoqueProcessada";
    public const string RejectedType = "MovimentacaoEstoqueRejeitada";

    // Formato numérico brasileiro sem depender de cultura instalada (ICU) no container.
    private static readonly NumberFormatInfo BrazilianNumbers = new()
    {
        NumberDecimalSeparator = ",",
        NumberGroupSeparator = "."
    };

    public static NotificationMessage Processed(MovementMessage movement, DateTimeOffset createdAt) =>
        Create(ProcessedType, movement.Recipients, BuildProcessedContent(movement), createdAt);

    public static NotificationMessage Rejected(MovementMessage movement, string reason, DateTimeOffset createdAt) =>
        Create(RejectedType, ValidRecipients.Of(movement.Recipients),
            $"Movimentação {movement.MessageId} rejeitada pelo estoque. {reason}", createdAt);

    private static NotificationMessage Create(
        string type, IReadOnlyList<string> recipients, string content, DateTimeOffset createdAt) => new()
    {
        MessageId = Guid.NewGuid(),
        NotificationType = type,
        Recipients = recipients,
        Content = content,
        CreatedAt = createdAt
    };

    private static string BuildProcessedContent(MovementMessage movement)
    {
        var items = string.Join("; ", movement.Items.Select(item =>
            $"{item.Code} ({item.Description}) x {item.Quantity.ToString("0.###", BrazilianNumbers)}"));

        var total = movement.Items.Sum(item => item.Quantity * item.Value);

        return $"Movimentação de {movement.MovementType} processada no estoque. " +
               $"Itens: {items}. Valor total: R$ {total.ToString("N2", BrazilianNumbers)}.";
    }
}
