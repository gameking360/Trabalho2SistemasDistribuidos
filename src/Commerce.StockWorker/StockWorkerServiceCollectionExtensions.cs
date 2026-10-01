using Commerce.Contracts.Messaging;
using Commerce.Contracts.Movements;
using Commerce.Infrastructure.Consuming;
using Commerce.Infrastructure.DependencyInjection;
using Commerce.StockWorker.Messaging;
using Commerce.StockWorker.Stock;

namespace Commerce.StockWorker;

public static class StockWorkerServiceCollectionExtensions
{
    // Prefetch 1 + single active consumer: uma movimentação por vez, na ordem da fila.
    private const ushort SequentialPrefetch = 1;

    public static IServiceCollection AddStockWorker(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddRabbitMqMessaging(configuration);
        services.AddFailureSimulation(configuration);

        services.Configure<StockOptions>(configuration.GetSection(StockOptions.SectionName));
        services.AddSingleton<IStockRepository, InMemoryStockRepository>();
        services.AddScoped<StockMovementService>();

        services.AddMessageConsumer<MovementMessage, MovementMessageHandler>(
            new ConsumerSettings<MovementMessage>(Queues.Movements, "movimentação", SequentialPrefetch));

        return services;
    }
}
