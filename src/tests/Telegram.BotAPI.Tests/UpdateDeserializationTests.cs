using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using System.Text;
using Microsoft.Extensions.DependencyInjection;
using Telegram.BotAPI.GettingUpdates;
using Xunit;

namespace Telegram.BotAPI.Tests;

public sealed class UpdateDeserializationTests
{
    [Fact]
    public async Task UpdateResponse_UsesSourceGeneratedDeserializer()
    {
        const string json = """
            {"ok":true,"result":[
              {"update_id":101,"message":{"message_id":7,"date":1700000000,
                "chat":{"id":42,"type":"private"},"text":"hello"}},
              {"update_id":102,"callback_query":{"id":"query-1","from":{"id":9,"is_bot":false,"first_name":"A"},"chat_instance":"chat","data":"ok"}}
            ]}
            """;
        using ServiceProvider provider = new ServiceCollection()
            .AddOptimizedTelegramBotClient("test-token")
            .ConfigurePrimaryHttpMessageHandler(() => new JsonHandler(json))
            .Services.BuildServiceProvider();

        ITelegramBotClient client = provider.GetRequiredService<ITelegramBotClient>();
        IEnumerable<Update> updates = await client.GetUpdatesAsync();

        Update[] result = updates.ToArray();
        Assert.Equal(2, result.Length);
        Assert.Equal(101, result[0].UpdateId);
        Assert.Equal("hello", result[0].Message?.Text);
        Assert.Equal("ok", result[1].CallbackQuery?.Data);
    }

    [Fact]
    public async Task CustomUpdateDeserializer_CanBeReplacedThroughDi()
    {
        CustomDeserializer custom = new();
        ServiceCollection services = new();
        services.AddSingleton<IUpdateResponseDeserializer>(custom);
        services.AddOptimizedTelegramBotClient("test-token")
            .ConfigurePrimaryHttpMessageHandler(() => new JsonHandler("{\"ok\":true,\"result\":[]}"));

        using ServiceProvider provider = services.BuildServiceProvider();
        ITelegramBotClient client = provider.GetRequiredService<ITelegramBotClient>();

        IEnumerable<Update> updates = await client.GetUpdatesAsync();

        Assert.Same(custom, provider.GetRequiredService<IUpdateResponseDeserializer>());
        Assert.Equal(777, Assert.Single(updates).UpdateId);
        Assert.True(custom.WasCalled);
    }

    private sealed class CustomDeserializer : IUpdateResponseDeserializer
    {
        public bool WasCalled { get; private set; }

        public ValueTask<BotResponse<IEnumerable<Update>>> DeserializeAsync(
            Stream utf8Json, CancellationToken cancellationToken = default)
        {
            WasCalled = true;
            return ValueTask.FromResult(new BotResponse<IEnumerable<Update>>
            {
                Ok = true,
                Result = new[] { new Update { UpdateId = 777 } }
            });
        }
    }

    private sealed class JsonHandler(string json) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken) =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(json, Encoding.UTF8, "application/json")
            });
    }
}
