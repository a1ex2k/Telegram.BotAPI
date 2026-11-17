using System.Net;
using System.Text;
using Telegram.BotAPI;
using Xunit;

namespace Telegram.BotAPI.Tests;

public sealed class OptimizedTelegramBotClientTests
{
    [Fact]
    public async Task JsonArguments_ArePostedWithJsonContentType()
    {
        HttpRequestMessage? captured = null;
        using HttpClient httpClient = new(new StubHandler(request =>
        {
            captured = request;
            return JsonResponse("{\"ok\":true,\"result\":42}");
        }));
        TelegramBotClientOptions options = new("test-token", httpClient);
        OptimizedTelegramBotClient client = new(options, httpClient);

        int result = await client.CallMethodAsync<int>("getValue", new { ChatId = 123 });

        Assert.Equal(42, result);
        Assert.NotNull(captured);
        Assert.Equal(HttpMethod.Post, captured!.Method);
        Assert.Equal("application/json", captured.Content!.Headers.ContentType!.MediaType);
        string body = await captured.Content.ReadAsStringAsync();
        Assert.Contains("\"ChatId\":123", body);
    }

    [Fact]
    public async Task StreamArguments_AreSentAsJsonStream()
    {
        HttpRequestMessage? captured = null;
        using HttpClient httpClient = new(new StubHandler(request =>
        {
            captured = request;
            return JsonResponse("{\"ok\":true,\"result\":7}");
        }));
        TelegramBotClientOptions options = new("test-token", httpClient);
        OptimizedTelegramBotClient client = new(options, httpClient);
        using MemoryStream stream = new(Encoding.UTF8.GetBytes("{\"chat_id\":123}"));

        int result = await client.CallMethodAsync<int>("sendRaw", stream);

        Assert.Equal(7, result);
        Assert.NotNull(captured);
        Assert.Equal("application/json", captured!.Content!.Headers.ContentType!.MediaType);
        Assert.Equal("{\"chat_id\":123}", await captured.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task BotApiErrors_AreReturnedByDirectCall()
    {
        using HttpClient httpClient = new(new StubHandler(_ =>
            JsonResponse("{\"ok\":false,\"error_code\":400,\"description\":\"Bad Request\"}", HttpStatusCode.BadRequest)));
        TelegramBotClientOptions options = new("test-token", httpClient);
        OptimizedTelegramBotClient client = new(options, httpClient);

        BotResponse<int> response = await client.CallMethodDirectAsync<int>("getValue");

        Assert.False(response.Ok);
        Assert.Equal(400, response.ErrorCode);
    }

    private static HttpResponseMessage JsonResponse(string json, HttpStatusCode statusCode = HttpStatusCode.OK) =>
        new(statusCode)
        {
            Content = new StringContent(json, Encoding.UTF8, "application/json")
        };

    private sealed class StubHandler(Func<HttpRequestMessage, HttpResponseMessage> responseFactory)
        : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken) =>
            Task.FromResult(responseFactory(request));
    }
}
