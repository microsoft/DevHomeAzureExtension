// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using AzureExtension.DevBox.Models;

namespace AzureExtension.DevBox.Helpers;

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
