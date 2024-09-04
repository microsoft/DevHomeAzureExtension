// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Runtime.InteropServices.WindowsRuntime;
using System.Text;
using System.Text.Json;
using DevHomeAzureExtension.Contracts;
using DevHomeAzureExtension.Helpers;
using Microsoft.Windows.DevHome.SDK;
using Serilog;
using Windows.Foundation;
using Windows.Storage;

namespace DevHomeAzureExtension.QuickStartPlayground;

public sealed class DevContainerGenerationOperation : IQuickStartProjectGenerationOperation
{
    private static readonly ILogger _log = Log.ForContext("SourceContext", nameof(DevContainerGenerationOperation));

    private readonly string _prompt;
    private readonly StorageFolder _outputFolder;
    private readonly IAzureOpenAIService _azureOpenAIService;
    private readonly IInstalledAppsService _installedAppsService;
    private IExtensionAdaptiveCardSession2? _adaptiveCardSession;

    public DevContainerGenerationOperation(
        string prompt,
        StorageFolder outputFolder,
        IAzureOpenAIService azureOpenAIService,
        IInstalledAppsService installedAppsService)
    {
        _prompt = prompt;
        _outputFolder = outputFolder;
        _azureOpenAIService = azureOpenAIService;
        _installedAppsService = installedAppsService;
        _adaptiveCardSession = null;
    }

    public IExtensionAdaptiveCardSession2 AdaptiveCardSession => _adaptiveCardSession!;

    public IAsyncOperationWithProgress<QuickStartProjectResult, QuickStartProjectProgress> GenerateAsync()
    {
        return AsyncInfo.Run<QuickStartProjectResult, QuickStartProjectProgress>(async (cancellationToken, progress) =>
        {
            var currentStep = string.Empty;
            var currentProgress = 0u;

            try
            {
                ReportProgress(Resources.GetResource(@"QuickstartPlayground_Progress_GetRecommendedLanguage"), 0);
                var recommendedLanguage = await Completions.GetRecommendedLanguageAsync(_azureOpenAIService, _prompt);

                if (recommendedLanguage.Contains("Rejected", StringComparison.OrdinalIgnoreCase))
                {
                    return new QuickStartProjectResult(
                        new InvalidDataException(recommendedLanguage),
                        recommendedLanguage,
                        $"Requested rejected: '{recommendedLanguage}'");
                }

                ReportProgress(Resources.GetResource(@"QuickstartPlayground_Progress_GetEnrichedPrompt"), 8);
                var enrichedPrompt = await Completions.GetEnrichedPromptAsync(_azureOpenAIService, _prompt, recommendedLanguage);

                ReportProgress(Resources.GetResource(@"QuickstartPlayground_Progress_GetSamples"), 10);
                var samples = await GetSamplesAsync();

                ReportProgress(Resources.GetResource(@"QuickstartPlayground_Progress_GetEmbeddings"), 30);
                var promptEmbedding = _azureOpenAIService.GetEmbedding(_prompt);
                var cosineSortedSamples = EmbeddingsCalc.GetCosineSimilaritySamples(promptEmbedding, samples);
                var favoredLanguageCosineSortedSamples = EmbeddingsCalc.SortByLanguageThenCosine(cosineSortedSamples, recommendedLanguage);
                var topDoc = cosineSortedSamples.First().Sample;
                var topDocFavoredLanguage = favoredLanguageCosineSortedSamples.First().Sample;
                if (topDocFavoredLanguage != null && topDocFavoredLanguage.Name != null)
                {
                    ReportProgress(Resources.GetResource(@"QuickstartPlayground_Progress_GetContainerFiles"), 50);
                    var codeSpacesCompletion = await Completions.GetDevContainerFilesAsync(_azureOpenAIService, enrichedPrompt, topDocFavoredLanguage);

                    ReportProgress(Resources.GetResource(@"QuickstartPlayground_Progress_GetStarterCode"), 70);
                    var codeCompletion = await Completions.GetStarterCodeAsync(_azureOpenAIService, enrichedPrompt, codeSpacesCompletion, topDocFavoredLanguage);

                    ReportProgress(Resources.GetResource(@"QuickstartPlayground_Progress_GetReadme"), 80);
                    var readmeCompletion = await Completions.GetProjectReadmeAsync(_azureOpenAIService, enrichedPrompt, codeSpacesCompletion, codeCompletion);
                    var codeSpaceDefinitionString = codeSpacesCompletion + "\r\n" + codeCompletion + "\r\n" + readmeCompletion;

                    ReportProgress(Resources.GetResource(@"QuickstartPlayground_Progress_WriteOutput"), 85);
                    CreateFilesInOutputFolder(codeSpaceDefinitionString, _outputFolder);

                    ValidateJsonFilesInOutputFolder(_outputFolder);

                    // Docker validation is only done on debug builds for our internal validation purposes.
                    // For customer scenarios, we leave docker validation to the IDE.
                    DockerProgressUIController? dockerProgressUIController = null;
#if DEBUG
                    if (DockerValidation.IsDockerRunning())
                    {
                        dockerProgressUIController = new DockerProgressUIController();

                        ReportProgress(Resources.GetResource(@"QuickstartPlayground_Progress_DockerValidation"), 90, dockerProgressUIController);
                        var buildSuccess = await DockerValidation.BuildDockerContainerAsync(
                            _outputFolder,
                            dockerProgressUIController.OnDockerOutputReceived);
                        dockerProgressUIController.DockerValidationComplete(buildSuccess);

                        // Don't reset the adaptive card if we failed so that we keep showing the failure.
                        // Otherwise set it to null, so that it is no longer shown.
                        if (buildSuccess)
                        {
                            dockerProgressUIController = null;
                        }
                    }
#endif

                    ReportProgress(Resources.GetResource(@"QuickstartPlayground_Progress_Done"), 100, dockerProgressUIController);

                    var sampleDirectory = Path.Combine(AppContext.BaseDirectory, "AzureExtension\\Assets\\QuickstartPlayground\\Samples", topDocFavoredLanguage.Name);
                    var sampleUri = new Uri(sampleDirectory);
                    _log.Information($"Using {sampleUri.AbsoluteUri} as sample reference.");

                    return QuickStartProjectResult.CreateWithFeedbackHandler(
                        DevContainerProjectHost.GetProjectHosts(_outputFolder.Path, await IsVisualStudioCodeInsidersInstalled()),
                        [sampleUri],
                        new DevContainerProjectFeedback(_prompt));
                }
                else
                {
                    return new QuickStartProjectResult(
                        new InvalidDataException(nameof(topDocFavoredLanguage)),
                        Resources.GetResource(@"QuickstartPlayground_SampleFailureDuringProjectGeneration", recommendedLanguage),
                        $"Failed to find similar sample for '{recommendedLanguage}'");
                }
            }
            catch (Azure.RequestFailedException ex) when (string.Equals(ex.ErrorCode, "invalid_api_key", StringComparison.Ordinal))
            {
                return new QuickStartProjectResult(
                    new UnauthorizedAccessException(),
                    Resources.GetResource(@"QuickstartPlayground_APIKeyFailureDuringProjectGeneration", currentStep),
                    $"Failure to authenticate with API Key when '{currentStep}'");
            }
            catch (Exception ex)
            {
                return new QuickStartProjectResult(ex, Resources.GetResource(@"QuickstartPlayground_FailureDuringProjectGeneration", currentStep, ex.Message), ex.Message);
            }

            void ReportProgress(string step, uint progressValue, IExtensionAdaptiveCardSession2? dockerProgressAdaptiveCardSession = null)
            {
                _adaptiveCardSession = dockerProgressAdaptiveCardSession;
                progress.Report(new QuickStartProjectProgress(step, progressValue / 100.0));
                currentStep = step;
                currentProgress = progressValue;
            }
        });
    }

    public void CreateFilesInOutputFolder(string inputString, StorageFolder outputFolder)
    {
        // The input string is a set of concatenated strings; each string is the output
        // of a particular completion call to the language model, and each string represents
        // a file in the generated project (e.g., a devcontainer file, a README, etc.). Lines
        // starting with "=== ./" indicate the start of a new relative file, with the body of the
        // file appearing after that.

        // Trim whitespace at the beginning and get line entries.
        var lines = inputString.TrimStart().Replace("\r", string.Empty).Split('\n');
        string? filename = null;

        foreach (var line in lines)
        {
            if (line.StartsWith("=== ./", StringComparison.Ordinal))
            {
                filename = line.Trim('=', ' ', '.');

                // Remove "./" or ".//" from the path and combine with the output folder
                filename = filename.StartsWith("//", StringComparison.Ordinal) ? filename[2..] : filename[1..];
                filename = Path.Combine(outputFolder.Path, filename);

                var dirPath = Path.GetDirectoryName(filename) ?? throw new DirectoryNotFoundException("Unable to retrieve directory name");

                if (!Directory.Exists(dirPath))
                {
                    _log.Information("Creating directory: {dirPath}", dirPath);
                    Directory.CreateDirectory(dirPath);
                }

                // There is a scenario where the model can get confused and accidentally include the devcontainer.json
                // and the Dockerfile as part of the starter code definition- which means that the files are showing
                // up twice in the inputString since they are already included in the devcontainer section.
                // Explicit instructions have been added to protect against this, but it's not foolproof.
                //
                // We do not want to block the user from seeing the project files, so we will overwrite the file.
                // In the case where we've seen this happen, the files are identical.
                if (File.Exists(filename))
                {
                    _log.Error($"File already exists: {filename}");
                }

                _log.Information("Creating file: {filename}", filename);
                File.WriteAllText(filename, string.Empty);
            }
            else
            {
                if (string.IsNullOrEmpty(filename))
                {
                    _log.Information("Error: no filename set.");
                    return;
                }

                File.AppendAllText(filename, line + Environment.NewLine);
            }
        }
    }

    public void ValidateJsonFilesInOutputFolder(StorageFolder outputFolder)
    {
        var jsonFiles = Directory.GetFiles(outputFolder.Path, "*.json", SearchOption.AllDirectories);

        foreach (var jsonFile in jsonFiles)
        {
            try
            {
                // The model will create a devcontainer.json file, which may have // comments inside of it.
                // JsonDocument will reject those, even though they are legal under the devcontainer schema.
                // So, filter out any of those lines (logging them for debugging purposes).
                if (Path.GetFileName(jsonFile).Equals("devcontainer.json", StringComparison.OrdinalIgnoreCase))
                {
                    var filteredJsonText = new StringBuilder();
                    var lines = File.ReadLines(jsonFile);
                    foreach (var line in lines)
                    {
                        if (line.TrimStart().StartsWith("//", StringComparison.Ordinal))
                        {
                            _log.Information("Ignoring comment line: {line}", line);
                        }
                        else
                        {
                            filteredJsonText.AppendLine(line);
                        }
                    }

                    _ = JsonDocument.Parse(filteredJsonText.ToString());
                }
                else
                {
                    var jsonText = File.ReadAllText(jsonFile);
                    _ = JsonDocument.Parse(jsonText);
                }
            }
            catch (JsonException ex)
            {
                // This exception won't be reported back to the user. From a UX standpoint, we do not want to
                // block the user from at least seeing the project files, so we will not rethrow and instead
                // log (this will be a telemetry event once the telemetry extension story is in place).
                _log.Error(ex, "Error parsing JSON file: {jsonFile}", jsonFile);
            }
        }
    }

    public async Task<IReadOnlyList<TrainingSample>> GetSamplesAsync()
    {
        var embeddings = await StorageFile.GetFileFromApplicationUriAsync(_azureOpenAIService.GetEmbeddingsFile());
        var contents = await FileIO.ReadTextAsync(embeddings);
        return JsonSerializer.Deserialize(contents, SourceGenerationContext.Default.IReadOnlyListTrainingSample)!;
    }

    public async Task<bool> IsVisualStudioCodeInsidersInstalled()
    {
        var vsCodeInsider = new HashSet<string>
        {
            "Microsoft Visual Studio Code Insiders",
        };

        var vsCodeInsiderPathEntry = new Dictionary<string, List<string>>()
        {
            { "Microsoft Visual Studio Code Insiders", new() { "code-insiders.cmd" } },
        };

        var missingApps = await _installedAppsService.CheckForMissingDependencies([], vsCodeInsider, [], vsCodeInsiderPathEntry);
        return missingApps.Count == 0;
    }
}
