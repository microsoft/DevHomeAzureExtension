// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace DevHomeAzureExtension.Contracts;

public enum OpenAIEndpoint
{
    OpenAI,
    AzureOpenAI,
}

public delegate IAzureOpenAIService AzureOpenAIServiceFactory(OpenAIEndpoint endpoint);

public interface IAzureOpenAIService
{
    public IAICredentialService AICredentialService { get; }

    public void InitializeAIClient();

    public Uri GetEmbeddingsFile();

    public ReadOnlyMemory<float> GetEmbedding(string inputText);

    public Task<string> GetAICompletionAsync(string systemInstructions, string userMessage);
}
