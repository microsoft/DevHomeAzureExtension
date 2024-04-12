// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Text.Json;
using System.Text.Json.Serialization;
using Newtonsoft.Json;

namespace DevHomeAzureExtension.Helpers;

public static class Json
{
    public static async Task<T> ToObjectAsync<T>(string value)
    {
        if (typeof(T) == typeof(bool))
        {
            return (T)(object)bool.Parse(value);
        }

        return await Task.Run<T>(() =>
        {
            return JsonConvert.DeserializeObject<T>(value)!;
        });
    }

    public static async Task<string> StringifyAsync<T>(T value)
    {
        if (typeof(T) == typeof(bool))
        {
            return value!.ToString()!.ToLowerInvariant();
        }

        return await Task.Run<string>(() =>
        {
            return JsonConvert.SerializeObject(value);
        });
    }

    public static string Stringify<T>(T value)
    {
        if (typeof(T) == typeof(bool))
        {
            return value!.ToString()!.ToLowerInvariant();
        }

#pragma warning disable CA1869 // Cache and reuse 'JsonSerializerOptions' instances
        return System.Text.Json.JsonSerializer.Serialize(
            value,
            new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true,
                DefaultIgnoreCondition = JsonIgnoreCondition.Never,
                IncludeFields = true,
            });
#pragma warning restore CA1869 // Cache and reuse 'JsonSerializerOptions' instances
    }

    public static T? ToObject<T>(string json)
    {
        if (typeof(T) == typeof(bool))
        {
            return (T)(object)bool.Parse(json);
        }

#pragma warning disable CA1869 // Cache and reuse 'JsonSerializerOptions' instances
        return System.Text.Json.JsonSerializer.Deserialize<T>(json, new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true,
            DefaultIgnoreCondition = JsonIgnoreCondition.Never,
            IncludeFields = true,
        });
#pragma warning restore CA1869 // Cache and reuse 'JsonSerializerOptions' instances
    }
}
