// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Diagnostics;
using System.Text.Json;
using System.Text.Json.Serialization;
using AzureExtension.Contracts;
using DevHomeAzureExtension.Helpers;
using Microsoft.Windows.DevHome.SDK;
using Windows.Foundation;

namespace AzureExtension.QuickStartPlayground;

public sealed partial class DependencyUIController : IExtensionAdaptiveCardSession2
{
    private static readonly Dictionary<string, Tuple<string, string>> _dependencies = new()
    {
        { "Docker Desktop", new("Docker Desktop", "https://docs.docker.com/desktop/install/windows-install") },
        { "Microsoft Visual Studio Code", new("Visual Studio Code", "https://code.visualstudio.com/Download") },
        { "ms-vscode-remote.remote-containers", new("Visual Studio Code Remote Containers Extension", "https://marketplace.visualstudio.com/items?itemName=ms-vscode-remote.remote-containers") },
    };

    private static readonly Dictionary<string, List<string>> _dockerAndVSCodePathEntries = new()
    {
        { "Docker Desktop", new() { "docker.exe" } },
        { "Microsoft Visual Studio Code", new() { "code.cmd", "code-insiders.cmd" } },
    };

    private static readonly HashSet<string> _dockerAndVSCode =
    [
        "Docker Desktop",
        "Microsoft Visual Studio Code",
    ];

    private static readonly HashSet<string> _devContainerExtension =
    [
        "ms-vscode-remote.remote-containers",
    ];

    private readonly IInstalledAppsService _installedAppsService;
    private HashSet<string> _missingDependencies;
    private IExtensionAdaptiveCard? _extensionUI;

    public event TypedEventHandler<IExtensionAdaptiveCardSession2, ExtensionAdaptiveCardSessionStoppedEventArgs>? Stopped;

    public static QuickStartProjectAdaptiveCardResult GetAdaptiveCard(IInstalledAppsService installedAppsService)
    {
        var missingDependencies = installedAppsService.CheckForMissingDependencies([], _dockerAndVSCode, _devContainerExtension, _dockerAndVSCodePathEntries).Result;
        if (missingDependencies.Count == 0)
        {
            return null!;
        }

        return new QuickStartProjectAdaptiveCardResult(new DependencyUIController(missingDependencies, installedAppsService));
    }

    public DependencyUIController(HashSet<string> missingDependencies, IInstalledAppsService installedAppsService)
    {
        _missingDependencies = missingDependencies;
        _installedAppsService = installedAppsService;
    }

    public ProviderOperationResult Initialize(IExtensionAdaptiveCard extensionUI)
    {
        _extensionUI = extensionUI;
        var missingDependenciesJson = CreateMissingDependenciesJson(_missingDependencies, _dependencies);

        return _extensionUI.Update(GetTemplate(), missingDependenciesJson, string.Empty);
    }

    private string CreateMissingDependenciesJson(HashSet<string> dependencies, Dictionary<string, Tuple<string, string>> metadata)
    {
        var missingDependenciesDisplayNames = dependencies.Select(s => metadata[s]);

        var output = new
        {
            cardTitle = Resources.GetResource(@"QuickstartPlayground_Dependency_CardName"),
            instructions = Resources.GetResource(@"QuickstartPlayground_Dependency_CardInstructions"),
            appNameHeader = Resources.GetResource(@"QuickstartPlayground_Dependency_AppNameHeader"),
            downloadHeader = Resources.GetResource(@"QuickstartPlayground_Dependency_DownloadHeader"),
            refreshStatusAction = Resources.GetResource(@"QuickstartPlayground_Dependency_RefreshStatusAction"),
            cancelAction = Resources.GetResource(@"QuickstartPlayground_Dependency_CancelAction"),
            rows = missingDependenciesDisplayNames.Select(s => new { appName = s.Item1, url = s.Item2 }),
        };

        return JsonSerializer.Serialize(output);
    }

    private static string GetTemplate()
    {
        var dependencyPage = @"{
    ""type"": ""AdaptiveCard"",
    ""body"": [
        {
            ""type"": ""TextBlock"",
            ""size"": ""ExtraLarge"",
            ""weight"": ""Bolder"",
            ""text"": ""${cardTitle}""
        },
        {
            ""type"": ""TextBlock"",
            ""size"": ""Medium"",
            ""text"": ""${instructions}"",
            ""wrap"": true
        },
        {
            ""type"": ""Table"",
            ""firstRowAsHeaders"": true,
            ""showGridlines"": true,
            ""columns"": [
                {
                    ""width"": 1
                },
                {
                    ""width"": 1
                }
            ],
            ""rows"": [
                {
                    ""type"": ""TableRow"",
                    ""style"": ""accent"",
                    ""cells"": [
                        {
                            ""type"": ""TableCell"",
                            ""items"": [
                                {
                                    ""type"": ""TextBlock"",
                                    ""text"": ""${appNameHeader}"",
                                    ""wrap"": true,
                                    ""weight"": ""Bolder"",
                                    ""size"": ""medium""
                                }
                            ]
                        },
                        {
                            ""type"": ""TableCell"",
                            ""items"": [
                                {
                                    ""type"": ""TextBlock"",
                                    ""text"": ""${downloadHeader}"",
                                    ""wrap"": true,
                                    ""weight"": ""Bolder"",
                                    ""size"": ""medium""
                                }
                            ]
                        }
                    ]
                },
                {
                    ""type"": ""TableRow"",
                    ""$data"": ""${rows}"",
                    ""cells"": [
                        {
                            ""type"": ""TableCell"",
                            ""items"": [
                                {
                                    ""type"": ""TextBlock"",
                                    ""text"": ""${appName}"",
                                    ""wrap"": true
                                }
                            ]
                        },
                        {
                            ""type"": ""TableCell"",
                            ""items"": [
                                {
                                    ""type"": ""ActionSet"",
                                    ""actions"": [
                                        {
                                            ""type"": ""Action.OpenUrl"",
                                            ""title"": ""Go to download"",
                                            ""url"": ""${url}"",
                                            ""id"": ""openUrlAction_${url}""
                                        }
                                    ]
                                }
                            ]
                        }
                    ]
                }
            ]
        }
    ],
    ""actions"": [
        {
            ""type"": ""Action.Execute"",
            ""title"": ""${refreshStatusAction}"",
            ""id"": ""refreshAction""
        },
        {
            ""type"": ""Action.Execute"",
            ""title"": ""${cancelAction}"",
            ""id"": ""cancelAction""
        }
    ],
    ""$schema"": ""http://adaptivecards.io/schemas/adaptive-card.json"",
    ""version"": ""1.5"",
    ""verticalContentAlignment"": ""Top"",
    ""rtl"": false
}";
        return dependencyPage;
    }

    public IAsyncOperation<ProviderOperationResult> OnAction(string action, string inputs)
    {
        return Task.Run(() =>
        {
            var adaptiveCardPayload = JsonSerializer.Deserialize(action, DependencyAdaptiveCardPayloadSourceGeneration.Default.DependencyAdaptiveCardPayload);
            if (adaptiveCardPayload is not null)
            {
                if (adaptiveCardPayload.IsCancelAction)
                {
                    // Inform Dev Home using the stopped event that the user canceled,
                    // but the result of OnAction is we successfully handled the user clicking cancel.
                    Stopped?.Invoke(this, new(new ProviderOperationResult(ProviderOperationStatus.Failure, new OperationCanceledException(), string.Empty, string.Empty), string.Empty));
                    return new ProviderOperationResult(ProviderOperationStatus.Success, null, string.Empty, string.Empty);
                }
                else if (adaptiveCardPayload.IsRefreshAction)
                {
                    // Re-query the installed apps service to see what dependencies are missing (if any) and update the UI.
                    if (_installedAppsService is not null)
                    {
                        _missingDependencies = _installedAppsService.CheckForMissingDependencies([], _dockerAndVSCode, _devContainerExtension, _dockerAndVSCodePathEntries).Result;
                        if (_missingDependencies.Count == 0)
                        {
                            var result = new ProviderOperationResult(ProviderOperationStatus.Success, null, "All dependencies are installed", string.Empty);
                            Stopped?.Invoke(this, new(result, string.Empty));

                            return result;
                        }

                        var missingDependenciesJson = CreateMissingDependenciesJson(_missingDependencies, _dependencies);

                        _extensionUI?.Update(GetTemplate(), missingDependenciesJson, string.Empty);
                    }

                    return new ProviderOperationResult(ProviderOperationStatus.Failure, null, "OnAction() failed during Refresh", "InstalledApps service is null");
                }
                else if (adaptiveCardPayload.IsOpenUrlAction)
                {
                    if (adaptiveCardPayload.Url is null)
                    {
                        return new ProviderOperationResult(ProviderOperationStatus.Failure, null, "OnAction() for opening URL failed", "url is null");
                    }

                    // Open the URL in the default browser.
                    var browser = Process.Start(new ProcessStartInfo(adaptiveCardPayload.Url) { UseShellExecute = true });
                    if (browser is null)
                    {
                        return new ProviderOperationResult(ProviderOperationStatus.Failure, null, "OnAction() for opening URL failed", "Process.Start returned null");
                    }

                    return new ProviderOperationResult(ProviderOperationStatus.Success, null, "Opening web browser, please download and install the application.", string.Empty);
                }
                else
                {
                    return new ProviderOperationResult(ProviderOperationStatus.Failure, null, "OnAction() called with invalid action", "Id is null");
                }
            }
            else
            {
                return new ProviderOperationResult(ProviderOperationStatus.Failure, null, "OnAction() called with invalid action", "adaptiveCardPayload is null");
            }
        }).AsAsyncOperation();
    }

    public void Dispose()
    {
    }

    internal sealed class DependencyAdaptiveCardPayload
    {
        public string? Id
        {
            get; set;
        }

        public string? Url
        {
            get; set;
        }

        public bool IsCancelAction => Id == "cancelAction";

        public bool IsRefreshAction => Id == "refreshAction";

        // Adaptive cards requires that the id for each action is unique, but the 'open URL' action is effectively the same
        // (i.e., opening the browser to a particular URL). Ideally we want those all to funnel into the same implementation
        // in OnAction. So the way we do this is to have the adaptive card create an id out of a prefix and the URL (this
        // satisfies the uniqueness requirement). And then we map these back to a common action.
        public bool IsOpenUrlAction => Id is not null && Id.StartsWith("openUrlAction_", StringComparison.InvariantCulture);
    }

    [JsonSourceGenerationOptions(PropertyNameCaseInsensitive = true)]
    [JsonSerializable(typeof(DependencyAdaptiveCardPayload))]
    internal sealed partial class DependencyAdaptiveCardPayloadSourceGeneration : JsonSerializerContext
    {
    }
}
