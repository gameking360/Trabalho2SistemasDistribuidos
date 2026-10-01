using Commerce.Contracts.Messaging;
using Commerce.Contracts.Movements;
using Commerce.Infrastructure.Consuming;
using Commerce.Infrastructure.DependencyInjection;
using Commerce.StockWorker.Messaging;
using Commerce.StockWorker.Stock;

namespace Commerce.StockWorker;

public static class StockWorkerServiceCollectionExtensions
{
    // Prefetch 1 + um único consumidor ativo (x-single-active-consumer na fila): o broker só entrega a próxima
    // movimentação depois do ack da atual, então elas são processadas na ordem em que entraram na fila.
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
