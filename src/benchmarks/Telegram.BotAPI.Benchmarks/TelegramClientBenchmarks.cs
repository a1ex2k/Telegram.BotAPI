using System.IO;
using System.Net;
using System.Text;
using BenchmarkDotNet.Attributes;
using Telegram.BotAPI;

[MemoryDiagnoser]
[ShortRunJob]
public class TelegramClientBenchmarks
{
    private const int ParallelRequestCount = 8;

    private HttpClient originalHttpClient = null!;
    private HttpClient optimizedHttpClient = null!;
    private TelegramBotClient original = null!;
    private OptimizedTelegramBotClient optimized = null!;

    private readonly object smallArguments = new
    {
        ChatId = 123456L,
        Text = "benchmark payload"
    };

    private readonly object largeArguments = new
    {
        ChatId = 123456L,
        Text = new string('x', 16 * 1024),
        Caption = new string('y', 4 * 1024)
    };

    private readonly byte[] streamPayload = Encoding.UTF8.GetBytes(
        "{\"chat_id\":123456,\"text\":\"benchmark stream payload\"}");

    [GlobalSetup]
    public void Setup()
    {
        originalHttpClient = new HttpClient(new StubHandler());
        optimizedHttpClient = new HttpClient(new StubHandler());

        TelegramBotClientOptions originalOptions = new("benchmark-token", originalHttpClient);
        TelegramBotClientOptions optimizedOptions = new("benchmark-token", optimizedHttpClient);

        original = new TelegramBotClient(originalOptions);
        optimized = new OptimizedTelegramBotClient(optimizedOptions, optimizedHttpClient);
    }

    [Benchmark(Baseline = true)]
    public Task<int> OriginalSmallJson() =>
        original.CallMethodAsync<int>("sendMessage", smallArguments);

    [Benchmark]
    public Task<int> OptimizedSmallJson() =>
        optimized.CallMethodAsync<int>("sendMessage", smallArguments);

    [Benchmark]
    public Task<int> OriginalLargeJson() =>
        original.CallMethodAsync<int>("sendMessage", largeArguments);

    [Benchmark]
    public Task<int> OptimizedLargeJson() =>
        optimized.CallMethodAsync<int>("sendMessage", largeArguments);

    [Benchmark]
    public Task<int> OriginalJsonStream() =>
        original.CallMethodAsync<int>("sendRaw", new MemoryStream(streamPayload, writable: false));

    [Benchmark]
    public Task<int> OptimizedJsonStream() =>
        optimized.CallMethodAsync<int>("sendRaw", new MemoryStream(streamPayload, writable: false));

    [Benchmark]
    public Task OriginalParallelSmallJson() =>
        Task.WhenAll(Enumerable.Range(0, ParallelRequestCount)
            .Select(_ => original.CallMethodAsync<int>("sendMessage", smallArguments)));

    [Benchmark]
    public Task OptimizedParallelSmallJson() =>
        Task.WhenAll(Enumerable.Range(0, ParallelRequestCount)
            .Select(_ => optimized.CallMethodAsync<int>("sendMessage", smallArguments)));

    [GlobalCleanup]
    public void Cleanup()
    {
        originalHttpClient.Dispose();
        optimizedHttpClient.Dispose();
    }

    private sealed class StubHandler : HttpMessageHandler
    {
        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
        {
            if (request.Content is not null)
                await request.Content.ReadAsByteArrayAsync(cancellationToken).ConfigureAwait(false);

            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(
                    "{\"ok\":true,\"result\":1}",
                    Encoding.UTF8,
                    "application/json")
            };
        }
    }
}
