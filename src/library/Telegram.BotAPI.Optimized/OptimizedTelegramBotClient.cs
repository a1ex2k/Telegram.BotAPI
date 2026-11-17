using System.IO;
using System.Net;
using System.Net.Http.Headers;
using System.Reflection;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Telegram.BotAPI.AvailableTypes;

namespace Telegram.BotAPI;

/// <summary>
/// An independent, dependency-injection-friendly implementation of <see cref="ITelegramBotClient"/>.
/// </summary>
public sealed class OptimizedTelegramBotClient : ITelegramBotClient
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    private readonly TelegramBotClientOptions options;
    private readonly HttpClient httpClient;

    /// <summary>Creates a client using the supplied options and HTTP client.</summary>
    public OptimizedTelegramBotClient(TelegramBotClientOptions options, HttpClient httpClient)
    {
        if (options is null)
            throw new ArgumentNullException(nameof(options));
        if (httpClient is null)
            throw new ArgumentNullException(nameof(httpClient));
        this.options = options;
        this.httpClient = httpClient;
        this.options.HttpClient = httpClient;
    }

    TelegramBotClientOptions ITelegramBotClient.Options => options;

    /// <inheritdoc/>
    public TResult CallMethod<TResult>(string method, object? args = null) =>
        CallMethodAsync<TResult>(method, args).GetAwaiter().GetResult();

    /// <inheritdoc/>
    public async Task<TResult> CallMethodAsync<TResult>(
        string method, object? args = null, CancellationToken cancellationToken = default)
    {
        BotResponse<TResult> response;
        do
        {
            response = await SendAsync<TResult>(method, args, cancellationToken).ConfigureAwait(false);
            if (response.ErrorCode != 429 || !options.AutoRetryOnRateLimit)
                break;

            int retryAfter = response.Parameters?.RetryAfter ?? 1;
            await Task.Delay(TimeSpan.FromSeconds(retryAfter), cancellationToken).ConfigureAwait(false);
        }
        while (true);

        if (response.Ok)
            return response.Result!;

        if (options.IgnoreBotExceptions)
            return default!;

        if (response.ErrorCode is null || response.Description is null)
            throw new JsonException("Cannot deserialize the response error.");

        throw new BotRequestException(response.ErrorCode.Value, response.Description, response.Parameters);
    }

    /// <inheritdoc/>
    public BotResponse<TReturn> CallMethodDirect<TReturn>(string method, object? args = null) =>
        CallMethodDirectAsync<TReturn>(method, args).GetAwaiter().GetResult();

    /// <inheritdoc/>
    public Task<BotResponse<TReturn>> CallMethodDirectAsync<TReturn>(
        string method, object? args = null, CancellationToken cancellationToken = default) =>
        SendAsync<TReturn>(method, args, cancellationToken);

    private async Task<BotResponse<TReturn>> SendAsync<TReturn>(
        string method, object? args, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(method))
            throw new ArgumentException("The method name cannot be null or whitespace.", nameof(method));
        using HttpRequestMessage request = new(
            args is null ? HttpMethod.Get : HttpMethod.Post,
            BuildRequestUri(method));

        if (args is not null)
        {
            if (args.GetType().IsPrimitive || args is string)
                throw new ArgumentException("The method arguments must be an object.", nameof(args));

            request.Content = args is Stream stream
                ? CreateStreamContent(stream)
                : CreateContent(args);
        }

        using HttpResponseMessage response = await httpClient.SendAsync(
            request, HttpCompletionOption.ResponseHeadersRead, cancellationToken).ConfigureAwait(false);

        try
        {
            response.EnsureSuccessStatusCode();
        }
        catch (HttpRequestException) when (
            string.Equals(response.Content.Headers.ContentType?.MediaType, "application/json",
                StringComparison.OrdinalIgnoreCase))
        {
            // Telegram may return a Bot API error envelope with a non-success HTTP status.
        }

        using Stream responseStream = await response.Content
            .ReadAsStreamAsync().ConfigureAwait(false);

        return await JsonSerializer.DeserializeAsync<BotResponse<TReturn>>(
            responseStream, SerializerOptions, cancellationToken).ConfigureAwait(false)
            ?? throw new JsonException("Cannot deserialize the response.");
    }

    private Uri BuildRequestUri(string method)
    {
        string path = options.UseTestEnvironment
            ? $"bot{options.BotToken}/test/{method}"
            : $"bot{options.BotToken}/{method}";
        return new Uri($"{options.ServerAddress.TrimEnd('/')}/{path}", UriKind.Absolute);
    }

    private static HttpContent CreateStreamContent(Stream stream)
    {
        StreamContent content = new(stream);
        content.Headers.ContentType = new MediaTypeHeaderValue("application/json");
        return content;
    }

    private static HttpContent CreateContent(object args)
    {
        if (!ArgsHasFiles(args))
            return new JsonHttpContent(args);

        MultipartFormDataContent multipart = new();

        if (args is IDictionary<string, object?> dictionary)
        {
            foreach (KeyValuePair<string, object?> item in dictionary)
                AddMultipartValue(multipart, item.Key, item.Value);
        }
        else
        {
            PropertyInfo[] properties = args.GetType().GetProperties(BindingFlags.Instance | BindingFlags.Public);
            foreach (PropertyInfo property in properties)
            {
                object? value = property.GetValue(args);
                if (value is null)
                    continue;

                JsonPropertyNameAttribute? jsonName = property.GetCustomAttribute<JsonPropertyNameAttribute>();
                if (jsonName is not null)
                    AddMultipartValue(multipart, jsonName.Name, value);
                else if (value is IDictionary<string, InputFile> files)
                    AddFileDictionary(multipart, files);
            }
        }

        return multipart;
    }

    private static bool ArgsHasFiles(object args)
    {
        if (args is IDictionary<string, object?> dictionary)
        {
            foreach (KeyValuePair<string, object?> item in dictionary)
            {
                if (item.Value is InputFile ||
                    item.Value is IDictionary<string, InputFile> files && files.Count > 0)
                    return true;
            }

            return false;
        }

        PropertyInfo[] properties = args.GetType().GetProperties(BindingFlags.Instance | BindingFlags.Public);
        foreach (PropertyInfo property in properties)
        {
            object? value = property.GetValue(args);
            if (value is InputFile ||
                value is IDictionary<string, InputFile> files && files.Count > 0)
                return true;
        }

        return false;
    }

    private static bool AddMultipartValue(MultipartFormDataContent content, string name, object? value)
    {
        if (value is null)
            return false;

        if (value is InputFile inputFile)
        {
            content.Add(inputFile.Content, name, inputFile.Filename);
            return true;
        }

        if (value is IDictionary<string, InputFile> files)
            return AddFileDictionary(content, files);

        string fieldValue = value.GetType().IsPrimitive || value is string
            ? Convert.ToString(value, System.Globalization.CultureInfo.InvariantCulture) ?? string.Empty
            : JsonSerializer.Serialize(value, SerializerOptions);
        content.Add(new StringContent(fieldValue, Encoding.UTF8), name);
        return false;
    }

    private static bool AddFileDictionary(
        MultipartFormDataContent content, IDictionary<string, InputFile> files)
    {
        foreach (KeyValuePair<string, InputFile> file in files)
            content.Add(file.Value.Content, file.Key, file.Value.Filename);
        return files.Count > 0;
    }

    private sealed class JsonHttpContent : HttpContent
    {
        private readonly object value;

        public JsonHttpContent(object value)
        {
            this.value = value;
            Headers.ContentType = new MediaTypeHeaderValue("application/json");
        }

        protected override Task SerializeToStreamAsync(Stream stream, TransportContext? context) =>
            JsonSerializer.SerializeAsync(stream, value, value.GetType(), SerializerOptions);

        protected override bool TryComputeLength(out long length)
        {
            length = 0;
            return false;
        }

        protected override void Dispose(bool disposing)
        {
            base.Dispose(disposing);
        }
    }
}
