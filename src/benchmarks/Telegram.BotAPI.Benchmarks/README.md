# Telegram.BotAPI benchmarks

This project is an experimental benchmark harness, not a production implementation.

## Run

From the repository root:

```sh
dotnet run -c Release --project src/benchmarks/Telegram.BotAPI.Benchmarks -- --filter '*DiscriminatorBenchmarks*'
```

BenchmarkDotNet reports mean execution time and allocated bytes. Run on the same machine,
runtime, power profile, and build configuration when comparing revisions. Keep the generated
BenchmarkDotNet artifacts so results can be compared across commits.

## Current comparison

- **CurrentReflectionConverter** reproduces the existing discriminator converter's main
  deserialization costs: reflection-based subtype discovery (cached after first use),
  a `JsonDocument`, an intermediate raw JSON string, and a second deserialization.
- **GeneratedMetadataConverter** is an experimental fast path. It probes a copy of
  `Utf8JsonReader`, selects compile-time generated `JsonTypeInfo`, and deserializes the
  original reader directly. The source-generated context and serializer options are reused.

The benchmark DTOs deliberately model two subtypes sharing a discriminator, so the fast path
also measures subtype selection by payload fields. They are isolated benchmark models; a
follow-up integration step should port the same approach to the library's actual Telegram
model hierarchy and verify wire compatibility before changing production code.

Do not compare first-use initialization against warmed steady-state numbers. Add separate
cold-start benchmarks if startup cost is a target.
