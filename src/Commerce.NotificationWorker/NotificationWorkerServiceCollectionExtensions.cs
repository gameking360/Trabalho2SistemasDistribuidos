using Commerce.Contracts.Messaging;
using Commerce.Contracts.Notifications;
using Commerce.Infrastructure.Consuming;
using Commerce.Infrastructure.DependencyInjection;
using Commerce.NotificationWorker.Delivery;
using Commerce.NotificationWorker.Messaging;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Commerce.NotificationWorker;

public static class NotificationWorkerServiceCollectionExtensions
{
    // Notificações não exigem ordem entre si; um prefetch maior evita esperar uma ida ao broker a cada mensagem.
    private const ushort NotificationsPrefetch = 10;

    public static IServiceCollection AddNotificationWorker(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddRabbitMqMessaging(configuration);
        services.AddFailureSimulation(configuration);

        services.TryAddSingleton<INotificationSender, LogNotificationSender>();

        services.AddMessageConsumer<NotificationMessage, NotificationMessageHandler>(
            new ConsumerSettings<NotificationMessage>(Queues.Notifications, "notificação", NotificationsPrefetch));

        return services;
    }
}
