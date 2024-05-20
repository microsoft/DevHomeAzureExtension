// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Diagnostics;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Windows.DevHome.SDK;
using Windows.Foundation;

namespace AzureExtension.QuickStartPlayground;

public sealed partial class DockerProgressUIController : IExtensionAdaptiveCardSession2
{
    public event TypedEventHandler<IExtensionAdaptiveCardSession2, ExtensionAdaptiveCardSessionStoppedEventArgs>? Stopped;

    public bool Success { get; private set; }

    private readonly DockerOutput _dockerOutput = new();
    private IExtensionAdaptiveCard? _extensionUI;
    private DateTime _lastUpdate;

    public ProviderOperationResult Initialize(IExtensionAdaptiveCard extensionUI)
    {
        _extensionUI = extensionUI;
        return _extensionUI.Update(GetTemplate(), _dockerOutput.GetAsJson(), string.Empty);
    }

    private static string GetTemplate()
    {
        var dockerProgressPage = @"
{
    ""type"": ""AdaptiveCard"",
    ""body"": [
        {
            ""type"": ""TextBlock"",
            ""size"": ""Medium"",
            ""text"": ""${Output}"",
            ""wrap"": true
        }
    ],
    ""$schema"": ""http://adaptivecards.io/schemas/adaptive-card.json"",
    ""version"": ""1.5"",
    ""verticalContentAlignment"": ""Top"",
    ""rtl"": false
}";
        return dockerProgressPage;
    }

    public void OnDockerOutputReceived(object sender, DataReceivedEventArgs args)
    {
        var newData = args.Data;
        if (!string.IsNullOrEmpty(newData))
        {
            _dockerOutput.Update(newData);

            if (_extensionUI != null)
            {
                lock (this)
                {
                    // We don't want to constantly update the UI, so do it at a certain interval.
                    var now = DateTime.Now;
                    if (_lastUpdate == default || now - _lastUpdate >= TimeSpan.FromMilliseconds(500))
                    {
                        _extensionUI.Update(GetTemplate(), _dockerOutput.GetAsJson(), string.Empty);
                        _lastUpdate = now;
                    }
                }
            }
        }
    }

    public void DockerValidationComplete(bool success)
    {
        Success = success;

        // Update the adaptive card in case we didn't meet the time period to update.
        if (!success && _extensionUI != null)
        {
            var dockerOutputJson = _dockerOutput.GetAsJson();
            if (dockerOutputJson != string.Empty)
            {
                _extensionUI.Update(GetTemplate(), dockerOutputJson, string.Empty);
            }
        }

        var result = new ProviderOperationResult(success ? ProviderOperationStatus.Success : ProviderOperationStatus.Failure, null, string.Empty, string.Empty);
        Stopped?.Invoke(this, new(result, string.Empty));
    }

    public static QuickStartProjectAdaptiveCardResult GetAdaptiveCard()
    {
        return new QuickStartProjectAdaptiveCardResult(new DockerProgressUIController());
    }

    public IAsyncOperation<ProviderOperationResult> OnAction(string action, string inputs)
    {
        return null!;
    }

    public void Dispose()
    {
    }

    private sealed class DockerOutput
    {
        private readonly StringBuilder _output = new();

        public string Output
        {
            get
            {
                lock (_output)
                {
                    return _output.ToString();
                }
            }
        }

        public DockerOutput()
        {
        }

        public void Update(string newData)
        {
            lock (_output)
            {
                _output.AppendLine(newData);
            }
        }

        public string GetAsJson()
        {
            return JsonSerializer.Serialize(this, DockerOutputGenerationContext.Default.DockerOutput);
        }
    }

    [JsonSerializable(typeof(DockerOutput))]
    private sealed partial class DockerOutputGenerationContext : JsonSerializerContext
    {
    }
}
