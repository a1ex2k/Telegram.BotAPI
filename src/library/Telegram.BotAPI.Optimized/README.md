# Telegram.BotAPI.Optimized

A separate, DI-enabled implementation of the original `ITelegramBotClient`.

## Registration

```csharp
services.AddOptimizedTelegramBotClient(
    botToken,
    options =>
    {
        options.AutoRetryOnRateLimit = true;
        options.UseTestEnvironment = false;
    });
```

Resolve `ITelegramBotClient` or `OptimizedTelegramBotClient` from DI.

The project references the original Telegram.BotAPI library and does not modify its models or client implementation.
