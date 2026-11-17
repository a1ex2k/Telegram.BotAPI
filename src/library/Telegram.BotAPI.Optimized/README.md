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

## Update deserialization

The optimized client deserializes `getUpdates` responses through
`IUpdateResponseDeserializer`. The default
`SourceGeneratedUpdateResponseDeserializer` uses System.Text.Json source-generated
metadata and asynchronous stream deserialization.

Replace it before registering the client:

```csharp
services.AddSingleton<IUpdateResponseDeserializer, MyUpdateResponseDeserializer>();
services.AddOptimizedTelegramBotClient(botToken);
```

Your implementation can use a custom serializer, converters, or update pipeline.
The client consumes the abstraction and does not depend on the default implementation.
The optimized project targets .NET 11.

## Benchmarks

The benchmark project includes `UpdateDeserializationBenchmarks`, which compares
reflection-based asynchronous deserialization with the source-generated implementation.
Run all benchmarks with:

```bash
dotnet run -c Release --project src/benchmarks/Telegram.BotAPI.Benchmarks
```
