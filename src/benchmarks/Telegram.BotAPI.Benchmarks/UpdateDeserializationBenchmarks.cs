using System.Text;
using System.Text.Json;
using BenchmarkDotNet.Attributes;
using Telegram.BotAPI;
using Telegram.BotAPI.GettingUpdates;

[MemoryDiagnoser]
[ShortRunJob]
public class UpdateDeserializationBenchmarks
{
    private static readonly JsonSerializerOptions Options = new();
    private readonly byte[] payload = Encoding.UTF8.GetBytes("""
        {"ok":true,"result":[
          {"update_id":101,"message":{"message_id":7,"date":1700000000,
            "chat":{"id":42,"type":"private"},"text":"hello"}},
          {"update_id":102,"callback_query":{"id":"query-1","from":{"id":9,"is_bot":false,"first_name":"A"},"chat_instance":"chat","data":"ok"}},
          {"update_id":103,"message":{"message_id":8,"date":1700000001,
            "chat":{"id":42,"type":"private"},"text":"another update"}}
        ]}
        """);

    private IUpdateResponseDeserializer deserializer = null!;

    [GlobalSetup]
    public void Setup() => deserializer = new SourceGeneratedUpdateResponseDeserializer();

    [Benchmark(Baseline = true)]
    public async Task<BotResponse<IEnumerable<Update>>> ReflectionAsync()
    {
        using MemoryStream stream = new(payload, writable: false);
        return (await JsonSerializer.DeserializeAsync<BotResponse<IEnumerable<Update>>>(
            stream, Options).ConfigureAwait(false))!;
    }

    [Benchmark]
    public async Task<BotResponse<IEnumerable<Update>>> SourceGeneratedAsync()
    {
        using MemoryStream stream = new(payload, writable: false);
        return await deserializer.DeserializeAsync(stream).ConfigureAwait(false);
    }
}
