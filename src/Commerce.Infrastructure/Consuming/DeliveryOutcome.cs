namespace Commerce.Infrastructure.Consuming;

public enum DeliveryOutcome
{
    /// <summary>Processada (ou encaminhada para retry/DLQ com confirmação): remove da fila.</summary>
    Ack,

    /// <summary>Não foi possível concluir agora: devolve para a fila para nova entrega.</summary>
    Requeue,

    /// <summary>Rejeita sem requeue: o RabbitMQ desvia a mensagem para a DLQ (x-dead-letter-exchange).</summary>
    Reject
}
