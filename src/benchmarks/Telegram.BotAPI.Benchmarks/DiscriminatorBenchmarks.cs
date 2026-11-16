using BenchmarkDotNet.Attributes;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;

[MemoryDiagnoser]
public class DiscriminatorBenchmarks
{
    private const string Payload = """
        {
          "status": "member",
          "id": 123456789,
          "first_name": "Benchmark",
          "username": "benchmark_bot"
        }
        """;

    private readonly JsonSerializerOptions legacyOptions = new();
    private readonly JsonSerializerOptions generatedOptions = new()
    {
        TypeInfoResolver = BenchmarkJsonContext.Default,
    };

    [GlobalSetup]
    public void Setup()
    {
        // Options and generated metadata are intentionally created once and reused.
        this.legacyOptions.Converters.Add(new LegacyMemberConverter());
        this.generatedOptions.Converters.Add(new GeneratedMemberConverter());

        // Fail before timing if either implementation selects a different subtype or payload.
        var legacy = JsonSerializer.Deserialize<LegacyMember>(Payload, this.legacyOptions);
        var generated = JsonSerializer.Deserialize<GeneratedMember>(Payload, this.generatedOptions);
        if (legacy is not LegacyMemberBasic legacyBasic ||
            generated is not GeneratedMemberBasic generatedBasic ||
            legacyBasic.Id != generatedBasic.Id ||
            legacyBasic.FirstName != generatedBasic.FirstName ||
            legacyBasic.Username != generatedBasic.Username)
        {
            throw new InvalidOperationException("The implementations produced different results.");
        }
    }

    [Benchmark(Baseline = true, Description = "Current pattern: DOM + raw JSON + reflection")]
    public LegacyMember? CurrentReflectionConverter() =>
        JsonSerializer.Deserialize<LegacyMember>(Payload, this.legacyOptions);

    [Benchmark(Description = "Prototype: reader probe + generated type metadata")]
    public GeneratedMember? GeneratedMetadataConverter() =>
        JsonSerializer.Deserialize<GeneratedMember>(Payload, this.generatedOptions);
}

[JsonConverter(typeof(LegacyMemberConverter))]
public abstract class LegacyMember
{
    [JsonPropertyName("status")]
    public abstract string Status { get; }
}

public sealed class LegacyMemberBasic : LegacyMember
{
    [JsonPropertyName("status")]
    public override string Status => "member";

    [JsonPropertyName("id")]
    public long Id { get; set; }

    [JsonPropertyName("first_name")]
    public string? FirstName { get; set; }

    [JsonPropertyName("username")]
    public string? Username { get; set; }
}

public sealed class LegacyMemberWithRole : LegacyMember
{
    [JsonPropertyName("status")]
    public override string Status => "member";

    [JsonPropertyName("id")]
    public long Id { get; set; }

    [JsonPropertyName("role")]
    public string? Role { get; set; }
}

/// <summary>
/// Benchmark-only reproduction of the repository's current discriminator strategy:
/// materialize JsonDocument, allocate raw JSON text, then deserialize a second time.
/// </summary>
public sealed class LegacyMemberConverter : JsonConverter<LegacyMember>
{
    private static readonly Lazy<IReadOnlyDictionary<string, Type[]>> Mapping =
        new(LoadMapping, LazyThreadSafetyMode.ExecutionAndPublication);

    public override LegacyMember? Read(
        ref Utf8JsonReader reader,
        Type typeToConvert,
        JsonSerializerOptions options)
    {
        using var document = JsonDocument.ParseValue(ref reader);
        var root = document.RootElement;
        var status = root.GetProperty("status").GetString()
            ?? throw new JsonException("status must be a string.");
        var rawJson = root.GetRawText();

        if (!Mapping.Value.TryGetValue(status, out var candidates))
            throw new JsonException($"Unknown status '{status}'.");

        var selected = candidates.Length == 1
            ? candidates[0]
            : candidates
                .OrderByDescending(type => type.GetProperties()
                    .Where(property => property.GetCustomAttribute<JsonPropertyNameAttribute>() is not null)
                    .Count(property =>
                    {
                        var jsonName = property.GetCustomAttribute<JsonPropertyNameAttribute>()!.Name;
                        return root.TryGetProperty(jsonName, out _);
                    }))
                .First();

        return (LegacyMember?)JsonSerializer.Deserialize(rawJson, selected, options);
    }

    public override void Write(Utf8JsonWriter writer, LegacyMember value, JsonSerializerOptions options) =>
        JsonSerializer.Serialize(writer, value, value.GetType(), options);

    private static IReadOnlyDictionary<string, Type[]> LoadMapping()
    {
        var map = new Dictionary<string, List<Type>>(StringComparer.Ordinal);
        foreach (var type in typeof(LegacyMember).Assembly.GetTypes()
                     .Where(type => type.IsSubclassOf(typeof(LegacyMember))))
        {
            var instance = RuntimeHelpers.GetUninitializedObject(type);
            var statusProperty = type.GetProperties().First(property =>
                property.GetCustomAttribute<JsonPropertyNameAttribute>()?.Name == "status");
            var status = (string)statusProperty.GetValue(instance)!;
            if (!map.TryGetValue(status, out var list))
                map.Add(status, list = []);
            list.Add(type);
        }

        return map.ToDictionary(pair => pair.Key, pair => pair.Value.ToArray(), StringComparer.Ordinal);
    }
}

[JsonConverter(typeof(GeneratedMemberConverter))]
public abstract class GeneratedMember
{
    [JsonPropertyName("status")]
    public abstract string Status { get; }
}

public sealed class GeneratedMemberBasic : GeneratedMember
{
    [JsonPropertyName("status")]
    public override string Status => "member";

    [JsonPropertyName("id")]
    public long Id { get; set; }

    [JsonPropertyName("first_name")]
    public string? FirstName { get; set; }

    [JsonPropertyName("username")]
    public string? Username { get; set; }
}

public sealed class GeneratedMemberWithRole : GeneratedMember
{
    [JsonPropertyName("status")]
    public override string Status => "member";

    [JsonPropertyName("id")]
    public long Id { get; set; }

    [JsonPropertyName("role")]
    public string? Role { get; set; }
}

/// <summary>
/// Prototype fast path: inspect a copy of Utf8JsonReader, select generated metadata,
/// then deserialize the original reader directly without DOM or intermediate JSON text.
/// </summary>
public sealed class GeneratedMemberConverter : JsonConverter<GeneratedMember>
{
    public override GeneratedMember? Read(
        ref Utf8JsonReader reader,
        Type typeToConvert,
        JsonSerializerOptions options)
    {
        var probe = reader;
        string? status = null;
        var hasFirstName = false;

        while (probe.Read())
        {
            if (probe.TokenType != JsonTokenType.PropertyName)
                continue;

            var propertyName = probe.GetString();
            if (!probe.Read())
                throw new JsonException("Unexpected end of JSON.");

            if (propertyName == "status")
                status = probe.GetString();
            else if (propertyName == "first_name")
                hasFirstName = true;

            probe.Skip();
        }

        JsonTypeInfo selectedType = status switch
        {
            "member" when hasFirstName => BenchmarkJsonContext.Default.GeneratedMemberBasic,
            "member" => BenchmarkJsonContext.Default.GeneratedMemberWithRole,
            _ => throw new JsonException($"Unknown status '{status}'."),
        };

        return (GeneratedMember?)JsonSerializer.Deserialize(ref reader, selectedType);
    }

    public override void Write(Utf8JsonWriter writer, GeneratedMember value, JsonSerializerOptions options) =>
        JsonSerializer.Serialize(writer, value, value.GetType(), options);
}

[JsonSourceGenerationOptions(GenerationMode = JsonSourceGenerationMode.Metadata)]
[JsonSerializable(typeof(GeneratedMember))]
[JsonSerializable(typeof(GeneratedMemberBasic))]
[JsonSerializable(typeof(GeneratedMemberWithRole))]
internal partial class BenchmarkJsonContext : JsonSerializerContext;
