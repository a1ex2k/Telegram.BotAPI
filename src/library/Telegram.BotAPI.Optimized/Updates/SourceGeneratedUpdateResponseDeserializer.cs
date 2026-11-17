using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;
using Telegram.BotAPI.GettingUpdates;

namespace Telegram.BotAPI;

/// <summary>
/// Update response deserializer using System.Text.Json source-generated metadata.
/// </summary>
public sealed class SourceGeneratedUpdateResponseDeserializer : IUpdateResponseDeserializer
{
    public async ValueTask<BotResponse<IEnumerable<Update>>> DeserializeAsync(
        Stream utf8Json,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(utf8Json);

        return await JsonSerializer.DeserializeAsync(
            utf8Json,
            UpdateJsonSerializerContext.Default.UpdateResponse,
            cancellationToken).ConfigureAwait(false)
            ?? throw new JsonException("Cannot deserialize the update response.");
    }
}

[JsonSourceGenerationOptions(DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull)]
[JsonSerializable(typeof(BotResponse<IEnumerable<Update>>), TypeInfoPropertyName = "UpdateResponse")]
internal partial class UpdateJsonSerializerContext : JsonSerializerContext;
