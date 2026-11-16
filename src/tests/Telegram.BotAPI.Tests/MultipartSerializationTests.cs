using System;
using System.Collections.Generic;
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
        Dictionary<string, string> fields = new();
        Dictionary<string, string> filenames = new();
        string? mediaType = null;

        using HttpClient httpClient = CreateHttpClient(async request =>
        {
            mediaType = request.Content?.Headers.ContentType?.MediaType;
            foreach (HttpContent part in request.Content!)
            {
                string? name = part.Headers.ContentDisposition?.Name?.Trim('"');
                if (name is null)
                {
                    continue;
                }

                string? filename = part.Headers.ContentDisposition?.FileName?.Trim('"');
                if (filename is not null)
                {
                    filenames[name] = filename;
                }
                else
                {
                    fields[name] = await part.ReadAsStringAsync();
                }
            }
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
        Assert.Equal("123", fields["chat_id"]);
        Assert.Equal("hello", fields["caption"]);
        Assert.Equal("sample.bin", filenames["document"]);
    }

    [Fact]
    public async Task DictionaryArguments_IncludeInputFile()
    {
        Dictionary<string, string> fields = new();
        Dictionary<string, string> filenames = new();

        using HttpClient httpClient = CreateHttpClient(async request =>
        {
            foreach (HttpContent part in request.Content!)
            {
                string? name = part.Headers.ContentDisposition?.Name?.Trim('"');
                if (name is null)
                {
                    continue;
                }

                string? filename = part.Headers.ContentDisposition?.FileName?.Trim('"');
                if (filename is not null)
                {
                    filenames[name] = filename;
                }
                else
                {
                    fields[name] = await part.ReadAsStringAsync();
                }
            }
        });

        TelegramBotClient client = new(new TelegramBotClientOptions("test-token", httpClient));
        Dictionary<string, object> args = new()
        {
            ["chat_id"] = 456,
            ["document"] = new InputFile(new byte[] { 4, 5, 6 }, "dict.bin"),
        };

        BotResponse<bool> response = await client.CallMethodDirectAsync<bool>("sendDocument", args);

        Assert.True(response.Ok);
        Assert.Equal("456", fields["chat_id"]);
        Assert.Equal("dict.bin", filenames["document"]);
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
