using Commerce.Contracts.Messaging;
using Commerce.Infrastructure.Publishing;

namespace Commerce.Tests.Support;

/// <summary>
/// Publisher em memória: registra as mensagens publicadas ou simula a indisponibilidade do broker.
/// </summary>
public sealed class FakeMessagePublisher : IMessagePublisher
{
    private readonly List<OutgoingMessage> _messages = [];

    public bool BrokerUnavailable { get; set; }

    public IReadOnlyList<OutgoingMessage> Messages => _messages;

    public Task PublishAsync(OutgoingMessage message, CancellationToken cancellationToken = default)
    {
        if (BrokerUnavailable)
            throw new MessagePublishException(message, "RabbitMQ indisponível (simulado)");

        _messages.Add(message);
        return Task.CompletedTask;
    }

    public T Deserialize<T>(OutgoingMessage message) => MessageJson.Deserialize<T>(message.Body.Span);
}
