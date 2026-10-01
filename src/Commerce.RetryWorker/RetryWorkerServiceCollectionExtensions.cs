using Commerce.Infrastructure.DependencyInjection;
using Commerce.RetryWorker.DeadLetters;
using Commerce.RetryWorker.Messaging;

namespace Commerce.RetryWorker;

public static class RetryWorkerServiceCollectionExtensions
{
    public static IServiceCollection AddRetryWorker(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddRabbitMqMessaging(configuration);

        services.AddOptions<DeadLetterCleanupOptions>()
            .Bind(configuration.GetSection(DeadLetterCleanupOptions.SectionName))
            .Validate(options => !options.Enabled || (options.Interval > TimeSpan.Zero && options.RetentionPeriod > TimeSpan.Zero),
                "DeadLetterCleanup:Interval e DeadLetterCleanup:RetentionPeriod devem ser positivos.")
            .ValidateOnStart();

        services.AddHostedService<RetryQueueConsumer>();
        services.AddHostedService<DeadLetterCleanupService>();

        return services;
    }
}
