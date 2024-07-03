// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Text.Json;
using System.Text.Json.Serialization;
using DevHomeAzureExtension.DevBox.Models;

namespace DevHomeAzureExtension.DevBox.Helpers;

/// <summary>
/// Custom JSON converter for <see cref="DevCenterOperationStatus"/>.
/// This is added directly to the <see cref="JsonSerializerOptions"/> to handle the conversion of the enum to and from JSON.
/// <see cref="Constants.JsonOptions"/>
/// </summary>
public class DevCenterOperationStatusConverter : JsonConverter<DevCenterOperationStatus>
{
    public override DevCenterOperationStatus Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (Enum.TryParse(reader.GetString(), out DevCenterOperationStatus status))
        {
            return status;
        }

        return DevCenterOperationStatus.Failed; // Set a default value if parsing fails
    }

    public override void Write(Utf8JsonWriter writer, DevCenterOperationStatus value, JsonSerializerOptions options)
    {
        writer.WriteStringValue(value.ToString());
    }
}
