using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;
using Telegram.BotAPI;
using Telegram.BotAPI.AvailableTypes;

namespace Telegram.BotAPI.Tests;

public sealed class MultipartSerializationTests
{
    [Fact]
    public async Task PocoArguments_IncludeFileAndJsonNamedFields()
    {
        string? body = null;
        string? mediaType = null;
        using HttpClient httpClient = CreateHttpClient(async request =>
        {
            mediaType = request.Content?.Headers.ContentType?.MediaType;
            using MemoryStream stream = new();
            await request.Content!.CopyToAsync(stream);
            body = Encoding.UTF8.GetString(stream.ToArray());
        });

        TelegramBotClient client = new(new TelegramBotClientOptions("test-token", httpClient));
        UploadArgs args = new()
        {
            ChatId = 123,
            Caption = "hello",
            Document = new InputFile(new byte[] { 1, 2, 3 }, "sample.bin"),
        };

        BotResponse<bool> response = await client.CallMethodDirectAsync<bool>("sendDocument", args);

        Assert.True(response.Ok);
        Assert.Equal("multipart/form-data", mediaType);
        Assert.Contains("name=\"chat_id\"", body);
        Assert.Contains("123", body);
        Assert.Contains("name=\"caption\"", body);
        Assert.Contains("hello", body);
        Assert.Contains("filename=\"sample.bin\"", body);
    }

    [Fact]
    public async Task DictionaryArguments_IncludeInputFile()
    {
        string? body = null;
        using HttpClient httpClient = CreateHttpClient(async request =>
        {
            using MemoryStream stream = new();
            await request.Content!.CopyToAsync(stream);
            body = Encoding.UTF8.GetString(stream.ToArray());
        });

        TelegramBotClient client = new(new TelegramBotClientOptions("test-token", httpClient));
        Dictionary<string, object> args = new()
        {
            ["chat_id"] = 456,
            ["document"] = new InputFile(new byte[] { 4, 5, 6 }, "dict.bin"),
        };

        BotResponse<bool> response = await client.CallMethodDirectAsync<bool>("sendDocument", args);

        Assert.True(response.Ok);
        Assert.Contains("name=\"chat_id\"", body);
        Assert.Contains("456", body);
        Assert.Contains("filename=\"dict.bin\"", body);
    }

    private static HttpClient CreateHttpClient(Func<HttpRequestMessage, Task> inspect)
    {
        return new HttpClient(new InspectingHandler(inspect))
        {
            BaseAddress = new Uri(TelegramBotClientOptions.BaseServerAddress),
        };
    }

    private sealed class InspectingHandler(Func<HttpRequestMessage, Task> inspect) : HttpMessageHandler
    {
        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            await inspect(request);
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("{\"ok\":true,\"result\":true}", Encoding.UTF8, "application/json"),
            };
        }
    }

    private sealed class UploadArgs
    {
        [JsonPropertyName("chat_id")]
        public long ChatId { get; init; }

        [JsonPropertyName("caption")]
        public string Caption { get; init; } = string.Empty;

        [JsonPropertyName("document")]
        public InputFile Document { get; init; } = null!;
    }
}
