using Microsoft.Extensions.DependencyInjection;

namespace Telegram.BotAPI;

/// <summary>Dependency injection registration for the optimized Telegram client.</summary>
public static class ServiceCollectionExtensions
{
    private const string HttpClientName = "Telegram.BotAPI.Optimized";

    /// <summary>Registers an optimized client for the specified bot token.</summary>
    public static IHttpClientBuilder AddOptimizedTelegramBotClient(
        this IServiceCollection services,
        string botToken,
        Action<TelegramBotClientOptions>? configure = null)
    {
        if (services is null)
            throw new ArgumentNullException(nameof(services));
        if (string.IsNullOrWhiteSpace(botToken))
            throw new ArgumentException("A bot token is required.", nameof(botToken));

        IHttpClientBuilder httpClientBuilder = services.AddHttpClient(HttpClientName);
        services.AddTransient<OptimizedTelegramBotClient>(provider =>
        {
            HttpClient httpClient = provider.GetRequiredService<IHttpClientFactory>()
                .CreateClient(HttpClientName);
            TelegramBotClientOptions options = new(botToken, httpClient);
            configure?.Invoke(options);
            return new OptimizedTelegramBotClient(options, httpClient);
        });
        services.AddTransient<ITelegramBotClient>(provider =>
            provider.GetRequiredService<OptimizedTelegramBotClient>());

        return httpClientBuilder;
    }
}
