// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Text.Json.Serialization;
using DevHomeAzureExtension.DevBox.Models;

namespace DevHomeAzureExtension.DevBox.DevBoxJsonToCsClasses;

/// <summary>
/// Used to generate the source code for the classes that we deserialize Json to objects for the DevBox feature.
/// .Net 8 requires a JsonSerializerContext to be used with the JsonSerializerOptions when
/// deserializing JSON into objects.
/// See : https://learn.microsoft.com/dotnet/standard/serialization/system-text-json/source-generation?pivots=dotnet-8-0
/// for more information
/// </summary>
[JsonSerializable(typeof(DevBoxImageReference))]
[JsonSerializable(typeof(DevBoxHardwareProfile))]
[JsonSerializable(typeof(DevBoxLongRunningOperation))]
[JsonSerializable(typeof(DevBoxMachines))]
[JsonSerializable(typeof(DevBoxMachineState))]
[JsonSerializable(typeof(DevBoxOperation))]
[JsonSerializable(typeof(DevBoxOperationList))]
[JsonSerializable(typeof(DevBoxOperationResult))]
[JsonSerializable(typeof(DevBoxOsDisk))]
[JsonSerializable(typeof(DevBoxPool))]
[JsonSerializable(typeof(DevBoxPoolRoot))]
[JsonSerializable(typeof(DevBoxPoolStopOnDisconnect))]
[JsonSerializable(typeof(DevBoxProject))]
[JsonSerializable(typeof(DevBoxProjectProperties))]
[JsonSerializable(typeof(DevBoxProjects))]
[JsonSerializable(typeof(DevBoxRemoteConnectionData))]
[JsonSerializable(typeof(DevBoxStorageProfile))]
[JsonSerializable(typeof(DevBoxCreationParameters))]
[JsonSerializable(typeof(DevCenterOperationBase))]
[JsonSerializable(typeof(DevBoxCreationPoolName))]
public partial class JsonSourceGenerationContext : JsonSerializerContext
{
}
