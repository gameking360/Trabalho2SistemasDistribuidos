namespace Commerce.Contracts.Messaging;

/// <summary>
/// Cabeçalhos AMQP adicionados pelo Retry Worker ao devolver uma mensagem para a fila de origem.
/// Eles carregam o histórico de tentativas sem alterar o contrato (corpo) da mensagem original.
/// </summary>
public static class MessageHeaders
{
    public const string RetryAttempt = "retry-attempt";
    public const string FirstAttemptAt = "retry-first-attempt-at";
    public const string OriginalExchange = "retry-original-exchange";
    public const string OriginalRoutingKey = "retry-original-routing-key";
    public const string OriginalPayload = "retry-original-payload";
}
