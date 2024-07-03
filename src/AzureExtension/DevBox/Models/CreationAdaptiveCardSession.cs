// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using DevHomeAzureExtension.Helpers;
using Microsoft.Windows.DevHome.SDK;
using Serilog;
using Windows.Foundation;

namespace DevHomeAzureExtension.DevBox.Models;

public enum SessionState
{
    InitialCreationForm,
    ReviewForm,
}

/// <summary>
/// Represents an adaptive card session that is presented to the user in Dev Homes environment creation UI. It is in charge
/// of retrieving and updating the adaptive card based on user input and interaction.
/// </summary>
public class CreationAdaptiveCardSession : IExtensionAdaptiveCardSession2
{
    private readonly Serilog.ILogger _log = Log.ForContext("SourceContext", nameof(CreationAdaptiveCardSession));

    private readonly string _pathToCreationFormTemplate = Path.Combine(AppContext.BaseDirectory, @"AzureExtension\DevBox\Templates\", "CreationForm.json");

    private readonly string _pathToReviewFormTemplate = Path.Combine(AppContext.BaseDirectory, @"AzureExtension\DevBox\Templates\", "ReviewForm.json");

    private readonly string _adaptiveCardNextButtonId = "DevHomeMachineConfigurationNextButton";

    /// <summary>
    /// An object that contains projects and a list of pools for that project
    /// </summary>
    private readonly List<DevBoxProjectAndPoolContainer> _devBoxProjectAndPoolContainer;

    private readonly List<JsonArray> _listOfAllDevBoxPools = new();

    private readonly JsonArray _arrayOfProjects = new();

    private readonly Dictionary<string, List<string>> _invalidDevBoxNames = new();

    private int _currentSelectedProjectIndex;

    private int _currentSelectedPoolIndex;

    private string _currentTextBoxValue = string.Empty;

    private ProviderOperationResult _operationResult = new(ProviderOperationStatus.Success, null, string.Empty, string.Empty);

    private bool _shouldEndSession;

    /// <summary>
    /// Gets the Json string that represents the user input that was passed to the adaptive card session. We'll keep this so we can pass it back to Dev Home
    /// at the end of the session.
    /// </summary>
    private Dictionary<string, string> _originalUserInputJson = new();

    public event TypedEventHandler<IExtensionAdaptiveCardSession2, ExtensionAdaptiveCardSessionStoppedEventArgs>? Stopped;

    public void Dispose()
    {
    }

    public CreationAdaptiveCardSession(List<DevBoxProjectAndPoolContainer> containers, IEnumerable<IComputeSystem>? devBoxes)
    {
        _devBoxProjectAndPoolContainer = containers;
        BuildUsedDevBoxNamesPerProject(devBoxes);
        BuildAllProjectsAndPoolJsonObjects();
    }

    private IExtensionAdaptiveCard? _creationAdaptiveCard;

    public bool ShouldEndSession { get; private set; }

    public ProviderOperationResult Initialize(IExtensionAdaptiveCard extensionUI)
    {
        _creationAdaptiveCard = extensionUI;

        return GetCreationFormAdaptiveCard(string.Empty, _currentSelectedProjectIndex, _currentSelectedPoolIndex);
    }

    public IAsyncOperation<ProviderOperationResult> OnAction(string action, string inputs)
    {
        return Task.Run(() =>
        {
            try
            {
                var actionPayLoad = JsonSerializer.Deserialize<Dictionary<string, string>>(action);
                var inputPayLoad = JsonSerializer.Deserialize<Dictionary<string, string>>(inputs);

                switch (_creationAdaptiveCard?.State)
                {
                    case "initialCreationForm":
                        HandleActionWhenFormInInitialState(actionPayLoad!, inputPayLoad!);
                        break;
                    case "reviewForm":
                        HandleActionWhenFormInReviewState(actionPayLoad!);
                        break;
                    default:
                        _shouldEndSession = true;
                        _operationResult = new ProviderOperationResult(
                            ProviderOperationStatus.Failure,
                            new InvalidOperationException(nameof(action)),
                            Resources.GetResource("AdaptiveCardStateNotRecognizedError"),
                            $"Unexpected state:{_creationAdaptiveCard?.State}");
                        break;
                }
            }
            catch (Exception ex)
            {
                _log.Error(ex, "Unable to update the adaptive card session for creation");
                _shouldEndSession = true;
                _operationResult = new ProviderOperationResult(ProviderOperationStatus.Failure, ex, ex.Message, ex.Message);
            }

            if (_shouldEndSession)
            {
                // The session has now ended. We'll raise the Stopped event to notify anyone in Dev Home who was listening to this event,
                // that the session has ended.
                Stopped?.Invoke(
                    this,
                    new ExtensionAdaptiveCardSessionStoppedEventArgs(_operationResult, GetDataToCreateDevBox()));
            }

            return _operationResult;
        }).AsAsyncOperation();
    }

    /// <summary>
    /// Loads the adaptive card template based on the session state.
    /// </summary>
    /// <param name="state">State the adaptive card session</param>
    /// <returns>A Json string representing the adaptive card</returns>
    public string LoadTemplate(SessionState state)
    {
        var pathToTemplate = state switch
        {
            SessionState.InitialCreationForm => _pathToCreationFormTemplate,
            SessionState.ReviewForm => _pathToReviewFormTemplate,
            _ => _pathToCreationFormTemplate,
        };

        return File.ReadAllText(pathToTemplate, Encoding.Default);
    }

    /// <summary>
    /// Creates the initial form that will be displayed to the user.
    /// </summary>
    /// <remarks>
    /// This will initially show a textbox, a combo box with a list of projects and a button to refresh the users pools.
    /// When the user selects a project the pools are not shown by default. Once they click the view pools button it submits
    /// the adaptive card and we send back a new adaptive card that contains a new combo box with the list of pools.
    /// </remarks>
    /// <param name="textboxText">The text currently in the textbox. This is used so we can save input from the previous adaptive card when refreshing the adaptive card</param>
    /// <param name="selectedProjectIndex">The current index of the selected project in the projects combo box. This is used so we can save input from the previous adaptive card when refreshing the adaptive card</param>
    /// <param name="selectedPoolIndex">The current index of the selected pool in the pools combo box. This is used so we can save input from the previous adaptive card when refreshing the adaptive card</param>
    /// <returns>Result of the operation</returns>
    private ProviderOperationResult GetCreationFormAdaptiveCard(string textboxText, int selectedProjectIndex, int selectedPoolIndex)
    {
        try
        {
            _log.Information("Building creation card with the following parameters: +" +
                $"'{nameof(textboxText)}': {textboxText}, " +
                $"'{nameof(selectedProjectIndex)}': {selectedProjectIndex}, " +
                $"'{nameof(selectedPoolIndex)}': {selectedPoolIndex}");

            var primaryButtonForCreationFlowText = Resources.GetResource("DevBox_PrimaryButtonLabelForCreationFlow");
            var secondaryButtonForCreationFlowText = Resources.GetResource("DevBox_SecondaryButtonLabelForCreationFlow");
            var settingsCardLabel = Resources.GetResource("DevBox_SettingsCardLabel");
            var enterNewEnvironmentNameLabel = Resources.GetResource("DevBox_EnterNewEnvironmentTextBoxLabel");
            var projectComboBoxLabel = Resources.GetResource("DevBox_ProjectComboBoxLabel");
            var poolComboBoxLabel = Resources.GetResource("DevBox_PoolComboBoxLabel");
            var getPoolsButtonLabel = Resources.GetResource("DevBox_PoolsButtonLabel");
            var devBoxTextBoxErrorMessage = Resources.GetResource("DevBox_TextBoxErrorMessage");
            var devBoxProjectComboBoxErrorMessage = Resources.GetResource("DevBox_ProjectComboBoxErrorMessage");
            var devBoxPoolsComboBoxErrorMessage = Resources.GetResource("DevBox_PoolsComboBoxErrorMessage");
            var devBoxPoolComboBoxLabel = Resources.GetResource("DevBox_PoolComboBoxLabel");
            var devBoxPoolsPlaceHolder = Resources.GetResource("DevBox_PoolsPlaceHolder");

            _arrayOfProjects![_currentSelectedProjectIndex]!.AsObject().TryGetPropertyValue("title", out var projectName);

            var templateData =
                $"{{\"PrimaryButtonLabelForCreationFlow\" : \"{primaryButtonForCreationFlowText}\"," +
                $"\"SecondaryButtonLabelForCreationFlow\" : \"{secondaryButtonForCreationFlowText}\"," +
                $"\"EnterNewEnvironmentTextBoxLabel\": \"{enterNewEnvironmentNameLabel}\"," +
                $"\"ProjectComboBoxLabel\": \"{projectComboBoxLabel}\"," +
                $"\"PoolComboBoxLabel\": \"{poolComboBoxLabel}\"," +
                $"\"PoolsButtonLabel\": \"{getPoolsButtonLabel}\"," +
                $"\"DevBoxTextBoxErrorMessage\": \"{devBoxTextBoxErrorMessage}\"," +
                $"\"DevBoxProjectComboBoxErrorMessage\": \"{devBoxProjectComboBoxErrorMessage}\"," +
                $"\"DevBoxPoolsComboBoxErrorMessage\": \"{devBoxPoolsComboBoxErrorMessage}\"," +
                $"\"DevBoxPoolComboBoxLabel\": \"{devBoxPoolComboBoxLabel}\"," +
                $"\"TextBoxText\": \"{textboxText}\"," +
                $"\"DevBoxPoolsPlaceHolder\": \"{devBoxPoolsPlaceHolder}\"," +
                $"\"SelectedProjectIndex\": \"{selectedProjectIndex}\"," +
                $"\"SelectedPoolIndex\": \"{selectedPoolIndex}\"," +
                $"\"DevBoxNameRegex\": \"{Constants.NameRegexPattern}\"," +
                $"\"SelectionChangedDataForChildChoiceSet\": {JsonSerializer.Serialize(_listOfAllDevBoxPools)}," +
                $"\"ProjectList\" : {_arrayOfProjects.ToJsonString()}," +
                $"\"PoolsList\" : {_listOfAllDevBoxPools[selectedProjectIndex].ToJsonString()}" +
                $"}}";

            var template = LoadTemplate(SessionState.InitialCreationForm);

            return _creationAdaptiveCard!.Update(template, templateData, "initialCreationForm");
        }
        catch (Exception ex)
        {
            _log.Error(ex, "Unable to get get creation template and data");
            var creationFormGenerationError = Resources.GetResource("DevBox_InitialCreationFormGenerationFailedError");
            return new ProviderOperationResult(ProviderOperationStatus.Failure, ex, creationFormGenerationError, ex.Message);
        }
    }

    /// <summary>
    /// Creates the review form that will be displayed to the user. This will be an adaptive card that is displayed in Dev Homes
    /// setup flow review page.
    /// </summary>
    /// <returns>Result of the operation</returns>
    private ProviderOperationResult GetForReviewFormAdaptiveCardAsync()
    {
        try
        {
            _log.Information("Building adaptive card template and data for the review form");

            var container = _devBoxProjectAndPoolContainer[_currentSelectedProjectIndex];
            var pool = container.Pools!.Value![_currentSelectedPoolIndex];

            _log.Information($"New DevBox name: {_originalUserInputJson["NewEnvironmentName"]}" +
                $"Project name: {container.Project!.Name}, Project index '{_currentSelectedProjectIndex}'" +
                $"Pool Name: {pool.Name}, Pool index '{_currentSelectedPoolIndex}'");

            // Setup localized strings for review form
            var reviewPageNameLabel = Resources.GetResource("DevBox_ReviewPageNameLabel", ":");
            var reviewPageProjectNameLabel = Resources.GetResource("DevBox_ReviewPageProjectNameLabel", ":");
            var reviewPagePoolNameLabel = Resources.GetResource("DevBox_ReviewPagePoolNameLabel", ":");
            var primaryButtonForCreationFlowText = Resources.GetResource("PrimaryButtonLabelForCreationFlow");
            var secondaryButtonForCreationFlowText = Resources.GetResource("SecondaryButtonLabelForCreationFlow");
            var poolHardwareSpecs = Resources.GetResource("DevBox_PoolSubtitle", pool.HardwareProfile.VCPUs, pool.HardwareProfile.MemoryGB, pool.StorageProfile.OsDisk.DiskSizeGB);

            var reviewFormData = new JsonObject
            {
                { "NewEnvironmentName", _originalUserInputJson["NewEnvironmentName"] },
                { "SelectedProjectName", container.Project!.Name },
                { "SelectedPoolName", pool.Name },
                { "ReviewPageProjectNameLabel", reviewPageProjectNameLabel },
                { "ReviewPagePoolNameLabel", reviewPagePoolNameLabel },
                { "ReviewPageSelectedPoolSpecs", poolHardwareSpecs },
                { "ReviewPageNameLabel", reviewPageNameLabel },
                { "PrimaryButtonLabelForCreationFlow", primaryButtonForCreationFlowText },
                { "SecondaryButtonLabelForCreationFlow", secondaryButtonForCreationFlowText },
            };

            var template = LoadTemplate(SessionState.ReviewForm);

            return _creationAdaptiveCard!.Update(LoadTemplate(SessionState.ReviewForm), reviewFormData.ToJsonString(), "reviewForm");
        }
        catch (Exception ex)
        {
            _log.Error(ex, "Unable to get get review template and data");
            var reviewFormGenerationError = Resources.GetResource("DevBox_ReviewFormGenerationFailedError");
            return new ProviderOperationResult(ProviderOperationStatus.Failure, ex, reviewFormGenerationError, ex.Message);
        }
    }

    private void HandleActionWhenFormInInitialState(Dictionary<string, string> actionPayload, Dictionary<string, string> inputPayload)
    {
        if (inputPayload.TryGetValue("NewEnvironmentName", out var textBoxStr))
        {
            _currentTextBoxValue = textBoxStr;
        }

        // check if a project was selected and update the selected index if it was.
        if (inputPayload.TryGetValue("ProjectsComboBox", out var projectIndexStr) && int.TryParse(projectIndexStr, out var projectIndex))
        {
            _currentSelectedProjectIndex = projectIndex;

            // check if a pool was selected and update the selected index if it was.
            if (inputPayload.TryGetValue("PoolsComboBox", out var poolIndexStr) && int.TryParse(poolIndexStr, out var poolIndex))
            {
                // If the project index changes, when GetCreationFormAdaptiveCard is called the list of pools will change. So, the previous
                // index is no longer valid.
                _currentSelectedPoolIndex = poolIndex;
            }
        }

        _log.Information($"HandleActionWhenFormInInitialState retrieved inputPayload: +" +
            $"{nameof(_currentTextBoxValue)}: {_currentTextBoxValue}" +
            $"{nameof(_currentSelectedProjectIndex)}: {_currentSelectedProjectIndex}" +
            $"{nameof(_currentSelectedPoolIndex)}: {_currentSelectedPoolIndex}");

        actionPayload.TryGetValue("id", out var actionButtonId);
        if ((actionButtonId != null) && actionButtonId.Equals(_adaptiveCardNextButtonId, StringComparison.OrdinalIgnoreCase))
        {
            _log.Information("Dev Home is moving to the Review page. Sending the review form and saving the current user inputPayload");

            // if OnAction's state is initialCreationForm and the user clicks the next button in Dev Home then the form was validated and they were able to
            // enter a name for their DevBox, select a project, and select a ComboBox.
            _originalUserInputJson = inputPayload;
            _operationResult = GetForReviewFormAdaptiveCardAsync();
        }
    }

    private void HandleActionWhenFormInReviewState(Dictionary<string, string> actionPayload)
    {
        actionPayload.TryGetValue("id", out var actionButtonId);

        if ((actionButtonId != null) && actionButtonId.Equals(_adaptiveCardNextButtonId, StringComparison.OrdinalIgnoreCase))
        {
            _log.Information("Dev Home is moving to summary page.  Sending the review form");

            // if OnAction's state is reviewForm, then the user has reviewed the form and Dev Home has started the creation process.
            // we'll show the same form to the user in Dev Homes summary page.
            _shouldEndSession = true;
            _operationResult = GetForReviewFormAdaptiveCardAsync();
        }
        else
        {
            _log.Information("Dev Home is moving back to the configure environment creation page.  Sending the creatin form with");

            // User is going back to creation form by clicking the previous button in Dev Homes review page.
            _operationResult = GetCreationFormAdaptiveCard(_currentTextBoxValue, _currentSelectedProjectIndex, _currentSelectedPoolIndex);
        }
    }

    private void BuildAllProjectsAndPoolJsonObjects()
    {
        _log.Information("Building json object for projects and pools");

        for (var i = 0; i < _devBoxProjectAndPoolContainer.Count; i++)
        {
            var container = _devBoxProjectAndPoolContainer[i];
            if (container?.Pools?.Value == null)
            {
                continue;
            }

            // Add information for the specific project to the project array
            var projectInfo = new JsonObject
                {
                    { "title", container.Project!.Properties.DisplayName.Length > 0 ? container.Project!.Properties.DisplayName : container.Project!.Name },
                    { "value", $"{i}" },
                };

            _arrayOfProjects.Add(projectInfo);

            // Add all the pools for the project into a list that contains all pools.
            // The index of the list will be directly related to the index of the project
            // in the project array above.
            var poolsArray = new JsonArray();
            for (var k = 0; k < container.Pools.Value.Count; k++)
            {
                var pool = container.Pools.Value[k];
                var poolHardwareSpecs = Resources.GetResource("DevBox_PoolSubtitle", pool.HardwareProfile.VCPUs, pool.HardwareProfile.MemoryGB, pool.StorageProfile.OsDisk.DiskSizeGB);
                var poolInfo = new JsonObject
                    {
                        { "title", $"{pool.Name} ({pool.Location})" },
                        { "subtitle", $"{poolHardwareSpecs}" },
                        { "value", $"{k}" },
                    };

                poolsArray.Add(poolInfo);
            }

            _listOfAllDevBoxPools.Add(poolsArray);
        }
    }

    private void BuildUsedDevBoxNamesPerProject(IEnumerable<IComputeSystem>? devBoxes)
    {
        if (devBoxes == null)
        {
            return;
        }

        foreach (var devBox in devBoxes)
        {
            if (devBox is DevBoxInstance instance)
            {
                if (!_invalidDevBoxNames.TryGetValue(instance.DevBoxState.ProjectName, out var _))
                {
                    _invalidDevBoxNames[instance.DevBoxState.ProjectName] = new();
                }

                _invalidDevBoxNames[instance.DevBoxState.ProjectName].Add(instance.DevBoxState.Name);
            }
        }
    }

    private string GetDataToCreateDevBox()
    {
        var container = _devBoxProjectAndPoolContainer[_currentSelectedProjectIndex];
        var pool = container.Pools!.Value![_currentSelectedPoolIndex];

        var creationParameters = new DevBoxCreationParameters(container!.Project!.Name, pool.Name, _originalUserInputJson["NewEnvironmentName"], container.Project.Properties.DevCenterUri);
        return JsonSerializer.Serialize(creationParameters, Constants.JsonOptions);
    }
}
