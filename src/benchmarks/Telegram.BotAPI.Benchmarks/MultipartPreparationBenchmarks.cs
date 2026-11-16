using System.Collections.Concurrent;
using System.Net.Http;
using System.Reflection;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using BenchmarkDotNet.Attributes;
using Telegram.BotAPI.AvailableTypes;

[MemoryDiagnoser]
public class MultipartPreparationBenchmarks
{
    private static readonly byte[] FileBytes = new byte[4096];
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    private static readonly ConcurrentDictionary<Type, PropertyPlan[]> PropertyPlans = new();
    private static readonly string[] ExpectedParts = ["name", "document", "benchmark.bin"];

    [GlobalSetup]
    public async Task Setup()
    {
        _ = PropertyPlans.GetOrAdd(typeof(UploadArgs), CreatePropertyPlan);

        string baseline = await SerializeToStringAsync(CreateUncachedForm(CreateArgs()));
        string cached = await SerializeToStringAsync(CreateCachedForm(CreateArgs()));

        foreach (string expected in ExpectedParts)
        {
            if (!baseline.Contains(expected, StringComparison.Ordinal)
                || !cached.Contains(expected, StringComparison.Ordinal))
            {
                throw new InvalidOperationException("Multipart implementations produced different form fields.");
            }
        }

        if (baseline.Length != cached.Length)
        {
            throw new InvalidOperationException("Multipart implementations produced different payload lengths.");
        }
    }

    [Benchmark(Baseline = true, Description = "Uncached reflection + multipart serialization")]
    public Task<int> UncachedReflection() =>
        SerializeAndDisposeAsync(CreateUncachedForm(CreateArgs()));

    [Benchmark(Description = "Cached property metadata + multipart serialization")]
    public Task<int> CachedPropertyMetadata() =>
        SerializeAndDisposeAsync(CreateCachedForm(CreateArgs()));

    private static UploadArgs CreateArgs() => new()
    {
        ChatId = 123456789,
        Name = "benchmark document",
        Document = new InputFile(FileBytes, "benchmark.bin"),
    };

    // Mirrors the current library path: reflection is performed once to detect files
    // and again to construct the multipart form, including attribute lookup.
    private static MultipartFormDataContent CreateUncachedForm(object args)
    {
        if (!ArgsHaveFilesUncached(args))
        {
            throw new InvalidOperationException("Expected a file-bearing argument object.");
        }

        MultipartFormDataContent content = new(Guid.NewGuid().ToString() + DateTime.UtcNow.Ticks);
        foreach (PropertyInfo property in args.GetType().GetProperties())
        {
            object? value = property.GetValue(args);
            if (value is null)
            {
                continue;
            }

            JsonPropertyNameAttribute? attribute = property.GetCustomAttribute<JsonPropertyNameAttribute>();
            if (attribute is not null)
            {
                if (value.GetType().IsPrimitive || value is string)
                {
                    content.Add(new StringContent(value.ToString()!), attribute.Name);
                }
                else if (value is InputFile file)
                {
                    content.Add(file.Content, attribute.Name, file.Filename);
                }
                else
                {
                    string json = JsonSerializer.Serialize(value, SerializerOptions);
                    content.Add(new StringContent(json, Encoding.UTF8), attribute.Name);
                }
            }
            else if (value is IDictionary<string, InputFile> files)
            {
                foreach (KeyValuePair<string, InputFile> file in files)
                {
                    content.Add(file.Value.Content, file.Key, file.Value.Filename);
                }
            }
        }

        return content;
    }

    private static bool ArgsHaveFilesUncached(object args)
    {
        foreach (PropertyInfo property in args.GetType().GetProperties())
        {
            object? value = property.GetValue(args);
            if (value is null || value.GetType().IsPrimitive || value is string)
            {
                continue;
            }

            switch (value)
            {
                case InputFile:
                    return true;
                case IDictionary<string, InputFile> files when files.Any():
                    return true;
            }
        }

        return false;
    }

    // Experimental path: cache property descriptors and JSON field names per argument type.
    private static MultipartFormDataContent CreateCachedForm(object args)
    {
        PropertyPlan[] plan = PropertyPlans.GetOrAdd(args.GetType(), CreatePropertyPlan);
        if (!ArgsHaveFilesCached(args, plan))
        {
            throw new InvalidOperationException("Expected a file-bearing argument object.");
        }

        MultipartFormDataContent content = new(Guid.NewGuid().ToString() + DateTime.UtcNow.Ticks);
        foreach (PropertyPlan item in plan)
        {
            object? value = item.Property.GetValue(args);
            if (value is null)
            {
                continue;
            }

            if (item.JsonName is not null)
            {
                if (value.GetType().IsPrimitive || value is string)
                {
                    content.Add(new StringContent(value.ToString()!), item.JsonName);
                }
                else if (value is InputFile file)
                {
                    content.Add(file.Content, item.JsonName, file.Filename);
                }
                else
                {
                    string json = JsonSerializer.Serialize(value, SerializerOptions);
                    content.Add(new StringContent(json, Encoding.UTF8), item.JsonName);
                }
            }
            else if (item.IsFileDictionary && value is IDictionary<string, InputFile> files)
            {
                foreach (KeyValuePair<string, InputFile> file in files)
                {
                    content.Add(file.Value.Content, file.Key, file.Value.Filename);
                }
            }
        }

        return content;
    }

    private static bool ArgsHaveFilesCached(object args, PropertyPlan[] plan)
    {
        foreach (PropertyPlan item in plan)
        {
            object? value = item.Property.GetValue(args);
            if (value is null || value.GetType().IsPrimitive || value is string)
            {
                continue;
            }

            switch (value)
            {
                case InputFile:
                    return true;
                case IDictionary<string, InputFile> files when files.Any():
                    return true;
            }
        }

        return false;
    }

    private static PropertyPlan[] CreatePropertyPlan(Type type) =>
        type.GetProperties()
            .Select(property => new PropertyPlan(
                property,
                property.GetCustomAttribute<JsonPropertyNameAttribute>()?.Name,
                typeof(IDictionary<string, InputFile>).IsAssignableFrom(property.PropertyType)))
            .ToArray();

    private static async Task<int> SerializeAndDisposeAsync(MultipartFormDataContent content)
    {
        using (content)
        {
            using MemoryStream output = new();
            await content.CopyToAsync(output).ConfigureAwait(false);
            return checked((int)output.Length);
        }
    }

    private static async Task<string> SerializeToStringAsync(MultipartFormDataContent content)
    {
        using (content)
        using (MemoryStream output = new())
        {
            await content.CopyToAsync(output).ConfigureAwait(false);
            return Encoding.UTF8.GetString(output.ToArray());
        }
    }

    private sealed record PropertyPlan(PropertyInfo Property, string? JsonName, bool IsFileDictionary);

    private sealed class UploadArgs
    {
        [JsonPropertyName("chat_id")]
        public long ChatId { get; init; }

        [JsonPropertyName("name")]
        public string Name { get; init; } = string.Empty;

        [JsonPropertyName("document")]
        public InputFile Document { get; init; } = null!;
    }
}
