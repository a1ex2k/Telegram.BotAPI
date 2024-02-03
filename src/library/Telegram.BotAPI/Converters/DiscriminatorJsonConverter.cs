// Copyright (c) 2024 Quetzal Rivera.
// Licensed under the MIT License, See LICENCE in the project root for license information.

using System.Collections.ObjectModel;
using System.Linq;
using System.Reflection;
using System.Runtime.Serialization;
using System.Text.Json;

namespace Telegram.BotAPI.Converters;

/// <summary>
/// Custom JSON converter that provides a way to retrieve the type of a class from a discriminator value.
/// </summary>
/// <typeparam name="T"></typeparam>
public abstract class DiscriminatorJsonConverter<T> : JsonConverter<T>
	where T : class
{
	/// <summary>
	/// Gets the name of the property that contains the discriminator value.
	/// </summary>
	public abstract string PropertyName { get; }

	private static ReadOnlyDictionary<string, Type>? discriminatorMapping;

	/// <inheritdoc />
	public override T? Read(
		ref Utf8JsonReader reader,
		Type typeToConvert,
		JsonSerializerOptions options
	)
	{
		var jsonDoc = JsonSerializer.Deserialize<JsonDocument>(ref reader, options)!;
		var prop = jsonDoc.RootElement.GetProperty(this.PropertyName);
		var rawText = jsonDoc.RootElement.GetRawText();
		var discriminatorValue =
			prop.GetString()
			?? throw new JsonException($"Property type of {this.PropertyName} must be a String.");

		// Load mapping if it hasn't been loaded yet
		discriminatorMapping ??= LoadDiscriminatorMapping(this.PropertyName);

		if (discriminatorMapping.TryGetValue(discriminatorValue, out var type))
		{
			var @object =
				JsonSerializer.Deserialize(rawText, type, options)
				?? throw new JsonException($"Json object is not a valid {type.Name}.");
			return (T)@object;
		}

		throw new InvalidOperationException(
			$"Cannot find type for discriminator value {discriminatorValue}"
		);
	}

	private static ReadOnlyDictionary<string, Type> LoadDiscriminatorMapping(string propertyName)
	{
		// Get all classes that inherit from T
		var classes = typeof(T).Assembly.GetTypes().Where(t => t.IsSubclassOf(typeof(T)));
		// Create a dictionary of the discriminator values and the corresponding type
		var mapping = new Dictionary<string, Type>();
		foreach (var c in classes)
		{
			// Creates a new instance of the class without calling the constructor
			var instance = FormatterServices.GetUninitializedObject(c);
			// Try to get the discriminator value
			var discriminator = c.GetProperties()
				.FirstOrDefault(p =>
					p.GetCustomAttribute<JsonPropertyNameAttribute>()?.Name == propertyName
				)
				?.GetValue(instance);
			if (discriminator is string discriminatorValue)
			{
				mapping.Add(discriminatorValue, c);
				continue;
			}

			throw new InvalidOperationException($"Cannot get discriminator value from {c.Name}");
		}

		return new ReadOnlyDictionary<string, Type>(mapping);
	}
}
