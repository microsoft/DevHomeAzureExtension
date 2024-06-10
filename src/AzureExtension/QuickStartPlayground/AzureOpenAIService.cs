// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Diagnostics;
using System.Reflection;
using Azure;
using Azure.AI.OpenAI;
using AzureExtension.Contracts;
using Microsoft.ML.Tokenizers;
using Newtonsoft.Json;
using Serilog;

namespace AzureExtension.QuickStartPlayground;

public sealed class AzureOpenAIService : IAzureOpenAIService
{
    private readonly ILogger _log = Serilog.Log.ForContext("SourceContext", nameof(AzureOpenAIService));

    private const string AzureOpenAIEmbeddingFile = "ms-appx:///AzureExtension/Assets/QuickStartPlayground/docsEmbeddings-AzureOpenAI.json";
    private const string OpenAIEmbeddingFile = "ms-appx:///AzureExtension/Assets/QuickStartPlayground/docsEmbeddings-OpenAI.json";

    private readonly OpenAIEndpoint _endpoint;

    // We use a Microsoft-published library that implements OpenAI's TikToken algorithm to figure out how much space to
    // allocate for the output.
    private readonly Tokenizer _gpt3Tokenizer = Tokenizer.CreateTiktokenForModel("gpt-35-turbo-instruct");

    // This is the publicly documented gpt-35-turbo-instruct context window length (shared between the input and output)
    private readonly int _contextWindowMaxLength = 4096;

    // This is a fudge factor in case there is a discrepancy between the tokenizer's output and what the
    // model will internally calculate.
    private readonly int _contextWindowPadding = 100;

    public AzureOpenAIService(IAICredentialService aiCredentialService, OpenAIEndpoint endpoint)
    {
        AICredentialService = aiCredentialService;
        _endpoint = endpoint;
        InitializeAIClient();
    }

    public IAICredentialService AICredentialService { get; }

    public void InitializeAIClient()
    {
        if (_endpoint == OpenAIEndpoint.AzureOpenAI)
        {
            InitializeAzureOpenAIClient();
        }
        else
        {
            InitializeOpenAIClient();
        }
    }

    private void InitializeOpenAIClient()
    {
        const string openAILoginId = "OpenAI";

        CompletionDeploymentName = "gpt-3.5-turbo-instruct";
        EmbeddingDeploymentName = "text-embedding-3-small";

        var secureOpenAIAccessToken = AICredentialService.GetCredentials(openAILoginId, openAILoginId);
        if (secureOpenAIAccessToken is not null)
        {
            var openAIAccessToken = new System.Net.NetworkCredential(string.Empty, secureOpenAIAccessToken).Password;
            OpenAIClient = new OpenAIClient(openAIAccessToken);
        }
    }

    private void InitializeAzureOpenAIClient()
    {
#if DEBUG
        Dictionary<string, string>? config;
        var binaryPath = Assembly.GetExecutingAssembly().Location;
        var binaryDirectory = Path.GetDirectoryName(binaryPath) ?? throw new DirectoryNotFoundException($"Couldn't get directory name for {binaryPath}");
        var filePath = Path.Combine(binaryDirectory, "config.json");

        if (File.Exists(filePath))
        {
            // Read the contents of the file
            config = JsonConvert.DeserializeObject<Dictionary<string, string>>(File.ReadAllText(filePath));
        }
        else
        {
            Debug.WriteLine("Couldn't find config.json next to binary, trying environment variables.");
            config = new Dictionary<string, string>
            {
                // Try reading values from environment variables
                ["embedAPIKey"] = Environment.GetEnvironmentVariable("DEVCONTAINER_EMBED_KEY")!,
                ["embed_base_url"] = Environment.GetEnvironmentVariable("DEVCONTAINER_EMBED_URL")!,
                ["embeddingDeploymentName"] = Environment.GetEnvironmentVariable("DEVCONTAINER_EMBED_DEPLOYMENTNAME")!,
                ["apiKey"] = Environment.GetEnvironmentVariable("DEVCONTAINER_API_KEY")!,
                ["deploymentName"] = Environment.GetEnvironmentVariable("DEVCONTAINER_API_DEPLOYMENTNAME")!,
                ["base_url"] = Environment.GetEnvironmentVariable("DEVCONTAINER_API_URL")!,
            };
        }

        if (config == null)
        {
            throw new InvalidOperationException("Couldn't find config.json or environment variables, please double-check that at least one of these is available");
        }

        // Validate that no keys in the dictionary are null or empty
        foreach (var key in config.Keys)
        {
            if (string.IsNullOrEmpty(config[key]))
            {
                throw new ArgumentNullException($"{key} is not set.");
            }
        }

        CompletionDeploymentName = config["deploymentName"];
        EmbeddingDeploymentName = config["embeddingDeploymentName"];
        OpenAIClient = new OpenAIClient(new Uri(config["base_url"]), new AzureKeyCredential(config["apiKey"]));
#endif
    }

    public Uri GetEmbeddingsFile()
    {
        return new Uri(_endpoint == OpenAIEndpoint.AzureOpenAI ? AzureOpenAIEmbeddingFile : OpenAIEmbeddingFile);
    }

    private string? CompletionDeploymentName { get; set; }

    private string? EmbeddingDeploymentName { get; set; }

    private OpenAIClient? OpenAIClient { get; set; }

    public ReadOnlyMemory<float> GetEmbedding(string inputText)
    {
        var openAIClient = OpenAIClient;
        ArgumentNullException.ThrowIfNull(openAIClient);

        var response = openAIClient.GetEmbeddings(new EmbeddingsOptions(EmbeddingDeploymentName, [inputText]));

        return response.Value.Data[0].Embedding;
    }

    public async Task<string> GetAICompletionAsync(string systemInstructions, string userMessage)
    {
        var openAIClient = OpenAIClient;
        ArgumentNullException.ThrowIfNull(openAIClient);

        var prompts = systemInstructions + "\n\n" + userMessage;
        var maxTokens = ComputeMaxTokens(prompts);

        var response = await openAIClient.GetCompletionsAsync(
            new CompletionsOptions()
            {
                DeploymentName = CompletionDeploymentName,
                Prompts =
                {
                    prompts,
                },
                Temperature = 0.01F,
                MaxTokens = maxTokens,
            });

        if (response.Value.Choices[0].FinishReason == CompletionsFinishReason.TokenLimitReached)
        {
            throw new InvalidDataException("Token limit reached while generating response");
        }

        return response.Value.Choices[0].Text;
    }

    private int ComputeMaxTokens(string inputPrompt)
    {
        var promptTokens = _gpt3Tokenizer.CountTokens(inputPrompt);
        var maxTokensForResponse = _contextWindowMaxLength - _contextWindowPadding - promptTokens;

        if (maxTokensForResponse < 0)
        {
            throw new InvalidDataException("Input prompt has taken up the entire context window.");
        }

        _log.Information("Input Prompt:\n{inputPrompt}", inputPrompt);
        _log.Information("Tokens used: {promptTokens}", promptTokens);
        _log.Information("Max tokens for response: {maxTokensForResponse}", maxTokensForResponse);

        return maxTokensForResponse;
    }
}
