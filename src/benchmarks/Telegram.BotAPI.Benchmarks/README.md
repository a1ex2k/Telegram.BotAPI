# Telegram.BotAPI benchmarks

This project is an experimental benchmark harness, not a production implementation.

## Run

From the repository root:

```sh
dotnet run -c Release --project src/benchmarks/Telegram.BotAPI.Benchmarks -- --filter '*Benchmarks*'
```

BenchmarkDotNet discovers all benchmark classes in the assembly. Reports include mean execution
time and allocated bytes. Compare revisions on the same machine, runtime, power profile, and build
configuration. Keep the generated artifacts.

## Benchmark suites

- **DiscriminatorBenchmarks** compares a benchmark-only reproduction of the current
  reflection + `JsonDocument` + raw-text + second-deserialization strategy with a reader-probe
  and source-generated-metadata prototype. Its DTOs are synthetic, so its results are not
  representative of whole-library speedups.
- **UpdatePayloadBenchmarks** uses the real `Telegram.BotAPI.GettingUpdates.Update` model and a
  representative text-message update. It compares UTF-16 string and UTF-8 byte input for one
  update and a batch of 32 updates.
- **TelegramBotClientBenchmarks** exercises the real `TelegramBotClient.CallMethodDirectAsync`
  pipeline against a deterministic in-memory `HttpMessageHandler`. It measures GET, JSON POST,
  and multipart upload (4 KiB) request/response paths. The handler consumes request content so
  serialization and multipart encoding are included, while network latency is excluded.

The HTTP benchmarks intentionally include the current client's full request/response lifecycle.
They are not a direct comparison of alternative transport implementations. In particular, the
current client does not dispose every request/response object; allocations and retained objects
are part of what this baseline exposes, not a claim about an optimized implementation.

## Next integration targets

1. Port the discriminator prototype to actual polymorphic Telegram model types, with parity
   tests covering every discriminator and ambiguous subtype.
2. Add comparative transport prototypes only after establishing this real-client baseline.
3. Add multipart preparation microbenchmarks if it becomes useful to separate reflection/form
   construction from the complete request pipeline.
4. Add bounded update-processing throughput tests separately from raw JSON parsing.

Do not mix first-use initialization with warmed steady-state numbers. Add cold-start benchmarks
separately when startup cost is a target. No production code is modified by this benchmark suite.
