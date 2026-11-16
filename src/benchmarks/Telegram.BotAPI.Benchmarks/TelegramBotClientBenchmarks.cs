using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.Json.Serialization;
using BenchmarkDotNet.Attributes;
using Telegram.BotAPI;
using Telegram.BotAPI.AvailableTypes;

[MemoryDiagnoser]
public class TelegramBotClientBenchmarks
{
    private static readonly byte[] SuccessResponse = Encoding.UTF8.GetBytes("""{"ok":true,"result":true}""");
    private HttpClient httpClient = null!;
    private TelegramBotClient client = null!;

    [GlobalSetup]
    public void Setup()
    {
        this.httpClient = new HttpClient(new BenchmarkHttpMessageHandler())
        {
            BaseAddress = new Uri(TelegramBotClientOptions.BaseServerAddress),
        };

        TelegramBotClientOptions options = new("benchmark-token", this.httpClient);
        this.client = new TelegramBotClient(options);

        // Fail setup if the in-memory transport does not produce a valid Bot API response.
        BotResponse<bool> response = this.client
            .CallMethodDirectAsync<bool>("getMe")
            .GetAwaiter()
            .GetResult();
        if (!response.Ok || response.Result != true)
        {
            throw new InvalidOperationException("The benchmark HTTP handler returned an invalid response.");
        }
    }

    [GlobalCleanup]
    public void Cleanup() => this.httpClient.Dispose();

    [Benchmark(Baseline = true, Description = "GET request + response")]
    public Task<BotResponse<bool>> GetRequestAndResponse() =>
        this.client.CallMethodDirectAsync<bool>("getMe");

    [Benchmark(Description = "JSON POST request + response")]
    public Task<BotResponse<bool>> JsonRequestAndResponse() =>
        this.client.CallMethodDirectAsync<bool>(
            "sendMessage",
            new SendMessageBenchmarkArgs { ChatId = 123456789, Text = "benchmark payload" });

    [Benchmark(Description = "Multipart upload request + response (4 KiB)")]
    public Task<BotResponse<bool>> MultipartUploadAndResponse() =>
        this.client.CallMethodDirectAsync<bool>(
            "sendDocument",
            new UploadBenchmarkArgs
            {
                ChatId = 123456789,
                Document = new InputFile(new byte[4096], "benchmark.bin"),
            });

    private sealed class BenchmarkHttpMessageHandler : HttpMessageHandler
    {
        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            // Consume the body so serialization and multipart encoding are included in the measurement.
            if (request.Content is not null)
            {
                await request.Content.CopyToAsync(Stream.Null, cancellationToken).ConfigureAwait(false);
            }

            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new ByteArrayContent(SuccessResponse),
            };
        }
    }

    private sealed class SendMessageBenchmarkArgs
    {
        [JsonPropertyName("chat_id")]
        public long ChatId { get; init; }

        [JsonPropertyName("text")]
        public string Text { get; init; } = string.Empty;
    }

    private sealed class UploadBenchmarkArgs
    {
        [JsonPropertyName("chat_id")]
        public long ChatId { get; init; }

        [JsonPropertyName("document")]
        public InputFile Document { get; init; } = null!;
    }
}
