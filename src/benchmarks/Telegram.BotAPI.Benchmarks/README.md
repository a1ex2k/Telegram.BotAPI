# Telegram.BotAPI client benchmarks

Run with `dotnet run -c Release --project src/benchmarks/Telegram.BotAPI.Benchmarks/Telegram.BotAPI.Benchmarks.csproj`.

The benchmark uses a local stub HTTP handler; it measures client-side serialization, request construction, response parsing, and allocations without Telegram network latency.
