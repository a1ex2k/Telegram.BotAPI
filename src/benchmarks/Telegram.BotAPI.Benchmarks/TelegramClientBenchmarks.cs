using System.Net;
using System.Text;
using BenchmarkDotNet.Attributes;
using Telegram.BotAPI;

[MemoryDiagnoser]
[ShortRunJob]
public class TelegramClientBenchmarks
{
    private HttpClient originalHttpClient = null!;
    private HttpClient optimizedHttpClient = null!;
    private TelegramBotClient original = null!;
    private OptimizedTelegramBotClient optimized = null!;
    private readonly object arguments = new { ChatId = 123456L, Text = "benchmark payload" };

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
    public Task<int> OriginalClient() =>
        original.CallMethodAsync<int>("sendMessage", arguments);

    [Benchmark]
    public Task<int> OptimizedClient() =>
        optimized.CallMethodAsync<int>("sendMessage", arguments);

    [GlobalCleanup]
    public void Cleanup()
    {
        originalHttpClient.Dispose();
        optimizedHttpClient.Dispose();
    }

    private sealed class StubHandler : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken) =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(
                    "{\"ok\":true,\"result\":1}",
                    Encoding.UTF8,
                    "application/json")
            });
    }
}
