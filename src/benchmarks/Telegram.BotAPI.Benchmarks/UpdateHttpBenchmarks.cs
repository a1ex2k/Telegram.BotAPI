using System.Text;
using BenchmarkDotNet.Attributes;
using Telegram.BotAPI;
using Telegram.BotAPI.Benchmarks;
using Telegram.BotAPI.GettingUpdates;

[MemoryDiagnoser]
[ShortRunJob]
public class UpdateHttpBenchmarks
{
    private static readonly byte[] Payload = Encoding.UTF8.GetBytes("""
        {"ok":true,"result":[
          {"update_id":101,"message":{"message_id":7,"date":1700000000,
            "chat":{"id":42,"type":"private"},"text":"hello"}},
          {"update_id":102,"callback_query":{"id":"query-1","from":{"id":9,"is_bot":false,"first_name":"A"},"chat_instance":"chat","data":"ok"}},
          {"update_id":103,"message":{"message_id":8,"date":1700000001,
            "chat":{"id":42,"type":"private"},"text":"another update"}},
          {"update_id":104,"message":{"message_id":9,"date":1700000002,
            "chat":{"id":42,"type":"private"},"text":"fourth update"}}
        ]}
        """);

    private MockTelegramHttpServer server = null!;
    private HttpClient originalHttpClient = null!;
    private HttpClient optimizedHttpClient = null!;
    private TelegramBotClient original = null!;
    private OptimizedTelegramBotClient optimized = null!;

    [GlobalSetup]
    public void Setup()
    {
        server = new MockTelegramHttpServer(Payload);

        originalHttpClient = new HttpClient();
        TelegramBotClientOptions originalOptions = new("benchmark-token", originalHttpClient)
        {
            ServerAddress = server.BaseAddress.ToString()
        };
        original = new TelegramBotClient(originalOptions);

        optimizedHttpClient = new HttpClient();
        TelegramBotClientOptions optimizedOptions = new("benchmark-token", optimizedHttpClient)
        {
            ServerAddress = server.BaseAddress.ToString()
        };
        optimized = new OptimizedTelegramBotClient(optimizedOptions, optimizedHttpClient);
    }

    [Benchmark(Baseline = true, Description = "Original: TCP HTTP → update deserialization")]
    public Task<IEnumerable<Update>> OriginalHttpResponseStream() =>
        original.GetUpdatesAsync();

    [Benchmark(Description = "Optimized: TCP HTTP → update deserialization")]
    public Task<IEnumerable<Update>> OptimizedHttpResponseStream() =>
        optimized.GetUpdatesAsync();

    [GlobalCleanup]
    public async Task Cleanup()
    {
        originalHttpClient.Dispose();
        optimizedHttpClient.Dispose();
        await server.DisposeAsync().ConfigureAwait(false);
    }
}
