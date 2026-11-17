using Telegram.BotAPI.GettingUpdates;

namespace Telegram.BotAPI;

/// <summary>
/// Deserializes Telegram update response envelopes. Replace this service through DI
/// to use a custom serializer or update model pipeline.
/// </summary>
public interface IUpdateResponseDeserializer
{
    /// <summary>Reads a getUpdates response from the supplied stream.</summary>
    ValueTask<BotResponse<IEnumerable<Update>>> DeserializeAsync(
        Stream utf8Json,
        CancellationToken cancellationToken = default);
}
