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
- **MultipartPreparationBenchmarks** isolates multipart form preparation and body serialization.
  It compares the current uncached reflection pattern (file detection plus property/attribute
  discovery) with a benchmark-only prototype that caches property descriptors and JSON field
  names by argument type. Both variants serialize the same representative 4 KiB upload payload.
  This is a metadata-caching experiment, not a source-generated production implementation.

The HTTP benchmarks intentionally include the current client's full request/response lifecycle.
They are not a direct comparison of alternative transport implementations. In particular, the
current client does not dispose every request/response object; allocations and retained objects
are part of what this baseline exposes, not a claim about an optimized implementation.

## Next integration targets

1. Port the discriminator prototype to actual polymorphic Telegram model types, with parity
   tests covering every discriminator and ambiguous subtype.
2. Use the multipart metadata benchmark to decide whether a production cache or generated
   metadata path is justified; add tests for dictionaries, nulls, nested JSON fields, and
   multiple files before changing production.
3. Add comparative transport prototypes only after establishing this real-client baseline.
4. Add bounded update-processing throughput tests separately from raw JSON parsing.

Do not mix first-use initialization with warmed steady-state numbers. Add cold-start benchmarks
separately when startup cost is a target. No production code is modified by this benchmark suite.
