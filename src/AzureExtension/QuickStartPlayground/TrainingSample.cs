// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Text.Json.Serialization;

namespace DevHomeAzureExtension.QuickStartPlayground;

public sealed class TrainingSample
{
    public required string Name
    {
        get; set;
    }

    public required string Codespaces
    {
        get; set;
    }

    public required string Prompt
    {
        get; set;
    }

    public required string Code
    {
        get; set;
    }

    public required string Readme
    {
        get; set;
    }

    public required ReadOnlyMemory<float> Embedding
    {
        get; set;
    }

    public required string Language
    {
        get; set;
    }
}

#pragma warning disable SA1402 // File may only contain a single type
[JsonSourceGenerationOptions(PropertyNameCaseInsensitive = true)]
[JsonSerializable(typeof(IReadOnlyList<TrainingSample>))]
internal sealed partial class SourceGenerationContext : JsonSerializerContext
{
}
#pragma warning restore SA1402 // File may only contain a single type
