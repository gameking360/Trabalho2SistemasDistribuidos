using Commerce.Contracts.Messaging;
using Commerce.Infrastructure.Configuration;
using Commerce.Infrastructure.Connection;
using Commerce.Infrastructure.Publishing;
using Commerce.Tests.Support;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace Commerce.Tests.Integration;

/// <summary>
/// Cenário "RabbitMQ indisponível": não precisa de broker, pois conecta numa porta onde nada está escutando.
/// </summary>
public sealed class BrokerUnavailableTests
{
    [Fact]
    public async Task PublishAsync_BrokerUnreachable_ThrowsInsteadOfLosingTheMessageSilently()
    {
        var options = Options.Create(new RabbitMqOptions
        {
            Host = "127.0.0.1",
            Port = 1,
            UserName = "commerce",
            Password = "senha-de-teste",
            PublishConfirmTimeout = TimeSpan.FromSeconds(10)
        });

        await using var connectionProvider = new RabbitMqConnectionProvider(options, NullLogger<RabbitMqConnectionProvider>.Instance);
        await using var publisher = new RabbitMqMessagePublisher(connectionProvider, options);
        var movement = TestMessages.Movement();

        var exception = await Assert.ThrowsAsync<MessagePublishException>(() => publisher.PublishAsync(
            OutgoingMessage.Json(Exchanges.Movements, RoutingKeys.MovementProcess, movement.MessageId.ToString(), movement)));

        Assert.Contains(movement.MessageId.ToString(), exception.Message);
        Assert.Contains("RabbitMQ indisponível", exception.Message);
    }
}
