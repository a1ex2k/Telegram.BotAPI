# Telegram.BotAPI benchmarks

This project is an experimental benchmark harness, not a production implementation.

## Run

From the repository root:

```sh
dotnet run -c Release --project src/benchmarks/Telegram.BotAPI.Benchmarks -- --filter '*Benchmarks*'
```

BenchmarkDotNet reports mean execution time and allocated bytes. Compare revisions on the same
machine, runtime, power profile, and build configuration. Keep the generated artifacts.

## Benchmark suites

- **DiscriminatorBenchmarks** compares a benchmark-only reproduction of the current
  reflection + `JsonDocument` + raw-text + second-deserialization strategy with a reader-probe
  and source-generated-metadata prototype. Its DTOs are synthetic, so its results are not
  representative of whole-library speedups.
- **UpdatePayloadBenchmarks** uses the real `Telegram.BotAPI.GettingUpdates.Update` model and a
  representative text-message update. It compares UTF-16 string and UTF-8 byte input for one
  update and a batch of 32 updates. This measures the library's actual model graph, but does not
  yet compare an alternative production converter.

## Next integration targets

1. Port the discriminator prototype to actual polymorphic Telegram model types, with parity
   tests covering every discriminator and ambiguous subtype.
2. Add request/response benchmarks around `TelegramBotClient` using a deterministic in-memory
   `HttpMessageHandler`, so network latency does not hide serialization and transport overhead.
3. Add multipart upload preparation benchmarks for scalar-only, one-file, and multi-file args.
4. Add bounded update-processing throughput tests separately from raw JSON parsing.

Do not mix first-use initialization with warmed steady-state numbers. Add cold-start benchmarks
separately when startup cost is a target. No production code is modified by this benchmark suite.
