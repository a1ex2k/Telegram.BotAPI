using BenchmarkDotNet.Attributes;
using System.Text;
using System.Text.Json;
using Telegram.BotAPI.GettingUpdates;

/// <summary>
/// Exercises the library's real Update model with representative text-message updates.
/// These are end-to-end model benchmarks, unlike the synthetic discriminator microbenchmark.
/// </summary>
[MemoryDiagnoser]
public class UpdatePayloadBenchmarks
{
    private const string SingleUpdate = """
        {
          "update_id": 987654321,
          "message": {
            "message_id": 42,
            "date": 1794850000,
            "chat": {
              "id": 123456789,
              "type": "private",
              "first_name": "Benchmark"
            },
            "from": {
              "id": 123456789,
              "is_bot": false,
              "first_name": "Benchmark",
              "username": "benchmark_user"
            },
            "text": "Measure real Telegram update deserialization."
          }
        }
        """;

    private static readonly byte[] SingleUpdateUtf8 = Encoding.UTF8.GetBytes(SingleUpdate);
    private static readonly string Batch = "[" + string.Join(",", Enumerable.Repeat(SingleUpdate, 32)) + "]";
    private static readonly byte[] BatchUtf8 = Encoding.UTF8.GetBytes(Batch);

    [Benchmark(Baseline = true, Description = "Single Update from UTF-16 string")]
    public Update? DeserializeSingleFromString() =>
        JsonSerializer.Deserialize<Update>(SingleUpdate);

    [Benchmark(Description = "Single Update from UTF-8 bytes")]
    public Update? DeserializeSingleFromUtf8() =>
        JsonSerializer.Deserialize<Update>(SingleUpdateUtf8);

    [Benchmark(Description = "32 Updates from UTF-16 string")]
    public Update[]? DeserializeBatchFromString() =>
        JsonSerializer.Deserialize<Update[]>(Batch);

    [Benchmark(Description = "32 Updates from UTF-8 bytes")]
    public Update[]? DeserializeBatchFromUtf8() =>
        JsonSerializer.Deserialize<Update[]>(BatchUtf8);
}
