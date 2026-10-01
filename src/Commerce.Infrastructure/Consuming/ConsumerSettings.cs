namespace Commerce.Infrastructure.Consuming;

/// <param name="Queue">Fila consumida.</param>
/// <param name="MessageDescription">Nome da mensagem nos logs (ex.: "movimentação").</param>
/// <param name="PrefetchCount">Quantas mensagens sem ack o broker pode entregar a este consumidor.</param>
public sealed record ConsumerSettings<TMessage>(string Queue, string MessageDescription, ushort PrefetchCount);
