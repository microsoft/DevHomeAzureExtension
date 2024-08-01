// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Text.Json.Nodes;
using DevHomeAzureExtension.Client;
using DevHomeAzureExtension.DataManager;
using DevHomeAzureExtension.DataModel;
using DevHomeAzureExtension.DeveloperId;
using DevHomeAzureExtension.Helpers;
using Microsoft.Windows.Widgets.Providers;
using Newtonsoft.Json;

namespace DevHomeAzureExtension.Widgets;

internal abstract class AzurePullRequestsBaseWidget : AzureWidget
{
    private readonly string _sampleIconData = IconLoader.GetIconAsBase64("screenshot.png");

    // Widget Data
    protected string WidgetTitle { get; set; } = string.Empty;

    protected string? Message { get; set; }

    // Creation and destruction methods
    public AzurePullRequestsBaseWidget()
    : base()
    {
    }

    public override void CreateWidget(WidgetContext widgetContext, string state)
    {
        if (state.Length != 0)
        {
            ResetDataFromState(state);
        }

        base.CreateWidget(widgetContext, state);
    }

    public override void DeleteWidget(string widgetId, string customState)
    {
        base.DeleteWidget(widgetId, customState);
    }

    protected string GetIconForPullRequestStatus(string? prStatus)
    {
        prStatus ??= string.Empty;
        if (Enum.TryParse<PolicyStatus>(prStatus, false, out var policyStatus))
        {
            return policyStatus switch
            {
                PolicyStatus.Approved => IconLoader.GetIconAsBase64("PullRequestApproved.png"),
                PolicyStatus.Running => IconLoader.GetIconAsBase64("PullRequestWaiting.png"),
                PolicyStatus.Queued => IconLoader.GetIconAsBase64("PullRequestWaiting.png"),
                PolicyStatus.Rejected => IconLoader.GetIconAsBase64("PullRequestRejected.png"),
                PolicyStatus.Broken => IconLoader.GetIconAsBase64("PullRequestRejected.png"),
                _ => IconLoader.GetIconAsBase64("PullRequestReviewNotStarted.png"),
            };
        }

        return string.Empty;
    }

    protected override bool ValidateConfiguration(WidgetActionInvokedArgs args)
    {
        return true;
    }

    // Increase precision of SetDefaultDeveloperLoginId by matching the selectedRepositoryUrl's org
    // with the first matching DeveloperId that contains that org.
    protected override void SetDefaultDeveloperLoginId()
    {
        base.SetDefaultDeveloperLoginId();
    }

    // Data loading methods
    public override void HandleDataManagerUpdate(object? source, DataManagerUpdateEventArgs e)
    {
        return;
    }

    public override void RequestContentData()
    {
        return;
    }

    protected override void ResetDataFromState(string data)
    {
        return;
    }

    public override string GetConfiguration(string data)
    {
        return EmptyJson;
    }

    public override void LoadContentData()
    {
        return;
    }

    // Overriding methods from Widget base
    public override string GetTemplatePath(WidgetPageState page)
    {
        return page switch
        {
            WidgetPageState.SignIn => @"Widgets\Templates\AzureSignInTemplate.json",
            WidgetPageState.Configure => @"Widgets\Templates\AzurePullRequestsConfigurationTemplate.json",
            WidgetPageState.Content => @"Widgets\Templates\AzurePullRequestsTemplate.json",
            WidgetPageState.Loading => @"Widgets\Templates\AzureLoadingTemplate.json",
            _ => throw new NotImplementedException(Page.GetType().Name),
        };
    }

    public override string GetData(WidgetPageState page)
    {
        return page switch
        {
            WidgetPageState.SignIn => GetSignIn(),
            WidgetPageState.Configure => GetConfiguration(string.Empty),
            WidgetPageState.Content => ContentData,
            WidgetPageState.Loading => GetLoadingMessage(),
            _ => throw new NotImplementedException(Page.GetType().Name),
        };
    }
}
