using System.Reflection;
using Commerce.Infrastructure.Configuration;
using Commerce.Infrastructure.Connection;
using Commerce.Infrastructure.Consuming;
using Commerce.Infrastructure.Publishing;
using Commerce.Infrastructure.Retry;
using Commerce.Infrastructure.Simulation;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Commerce.Infrastructure.DependencyInjection;

public static class MessagingServiceCollectionExtensions
{
    /// <summary>
    /// Registra conexão e publicação com confirms. A aplicação não inicia sem as credenciais do RabbitMQ.
    /// </summary>
    public static IServiceCollection AddRabbitMqMessaging(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddOptions<RabbitMqOptions>()
            .Bind(configuration.GetSection(RabbitMqOptions.SectionName))
            .PostConfigure(options =>
            {
                if (string.IsNullOrWhiteSpace(options.ClientName))
                    options.ClientName = Assembly.GetEntryAssembly()?.GetName().Name ?? "Commerce";
            })
            .Validate(options => !string.IsNullOrWhiteSpace(options.UserName) && !string.IsNullOrWhiteSpace(options.Password),
                "Usuário/senha do RabbitMQ não configurados. Defina RabbitMq__UserName e RabbitMq__Password " +
                "(variáveis de ambiente ou arquivo .env na raiz; veja .env.example).")
            .ValidateOnStart();

        services.TryAddSingleton(TimeProvider.System);
        services.TryAddSingleton<IRabbitMqConnectionProvider, RabbitMqConnectionProvider>();
        services.TryAddSingleton<IMessagePublisher, RabbitMqMessagePublisher>();

        return services;
    }

    /// <summary>
    /// Registra um consumidor (hosted service) para a fila informada, o handler de cada mensagem e o
    /// roteamento de falhas (retry/DLQ).
    /// </summary>
    public static IServiceCollection AddMessageConsumer<TMessage, THandler>(
        this IServiceCollection services, ConsumerSettings<TMessage> settings)
        where TMessage : class
        where THandler : class, IMessageHandler<TMessage>
    {
        services.AddFailureRouting();
        services.AddSingleton(settings);
        services.AddScoped<IMessageHandler<TMessage>, THandler>();
        services.AddHostedService<MessageConsumerService<TMessage>>();

        return services;
    }

    public static IServiceCollection AddFailureSimulation(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<FailureSimulationOptions>(configuration.GetSection(FailureSimulationOptions.SectionName));
        services.TryAddSingleton<FailureSimulator>();

        return services;
    }

    private static void AddFailureRouting(this IServiceCollection services)
    {
        if (services.Any(descriptor => descriptor.ServiceType == typeof(FailedMessageRouter)))
            return;

        services.AddOptions<RetryOptions>()
            .BindConfiguration(RetryOptions.SectionName)
            .Validate(options => options.MaxAttempts >= 1 && options.DelayIncrement > TimeSpan.Zero,
                "Retry:MaxAttempts deve ser maior que zero e Retry:DelayIncrement deve ser positivo.")
            .ValidateOnStart();

        services.TryAddSingleton<RetryPolicy>();
        services.TryAddSingleton<FailureEnvelopeFactory>();
        services.TryAddSingleton<FailedMessageRouter>();
    }
}
