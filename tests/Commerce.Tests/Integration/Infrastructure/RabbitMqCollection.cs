namespace Commerce.Tests.Integration.Infrastructure;

/// <summary>
/// Os testes de integração compartilham o mesmo vhost e por isso rodam em sequência, nunca em paralelo.
/// </summary>
[CollectionDefinition(Name)]
public sealed class RabbitMqCollection : ICollectionFixture<RabbitMqFixture>
{
    public const string Name = "RabbitMQ";
}
