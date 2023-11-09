// Copyright (c) Microsoft Corporation and Contributors
// Licensed under the MIT license.

using DevHome.Logging;
using DevHomeAzureExtension.Client;
using Microsoft.Windows.ApplicationModel.Resources;

namespace DevHomeAzureExtension.Helpers;

public static class Resources
{
    private static ResourceLoader? _resourceLoader;

    public static string GetResource(string identifier, Logger? log = null)
    {
        try
        {
            if (_resourceLoader == null)
            {
                _resourceLoader = new ResourceLoader(ResourceLoader.GetDefaultResourceFilePath(), "AzureExtension/Resources");
            }

            return _resourceLoader.GetString(identifier);
        }
        catch (Exception ex)
        {
            log?.ReportError($"Failed loading resource: {identifier}", ex);

            // If we fail, load the original identifier so it is obvious which resource is missing.
            return identifier;
        }
    }

    // Replaces all identifiers in the provided list in the target string. Assumes all identifiers
    // are wrapped with '%' to prevent sub-string replacement errors. This is intended for strings
    // such as a JSON string with resource identifiers embedded.
    public static string ReplaceIdentifiers(string str, string[] resourceIdentifiers, Logger? log = null)
    {
        var start = DateTime.Now;
        foreach (var identifier in resourceIdentifiers)
        {
            // What is faster, String.Replace, RegEx, or StringBuilder.Replace? It is String.Replace().
            // https://learn.microsoft.com/archive/blogs/debuggingtoolbox/comparing-regex-replace-string-replace-and-stringbuilder-replace-which-has-better-performance
            var resourceString = GetResource(identifier, log);
            str = str.Replace($"%{identifier}%", resourceString);
        }

        var elapsed = DateTime.Now - start;
        log?.ReportDebug($"Replaced identifiers in {elapsed.TotalMilliseconds}ms");
        return str;
    }

    // These are all the string identifiers that appear in widgets.
    public static string[] GetWidgetResourceIdentifiers()
    {
        return new string[]
        {
            "Extension_Name/Azure",
            "Widget_Template/Loading",
            "Widget_Template/Updated",
            "Widget_Template_Tooltip/Submit",
            "Widget_Template_Button/Submit",
            "Widget_Template_Button/SignIn",
            "Widget_Template_Tooltip/SignIn",
            "Widget_Template/SignInRequired",
            "Widget_Template_Button/Save",
            "Widget_Template_Button/Cancel",
            "Widget_Template_Tooltip/Save",
            "Widget_Template_Tooltip/Cancel",
            "Widget_Template/PRCreated",
            "Widget_Template/PRInto",
            "Widget_Template/ChooseAccountPlaceholder",
            "Widget_Template_Tooltip/ClickWorkItem",
            "Widget_Template_Tooltip/ClickPullRequest",
            "Widget_Template/RepositoryURLLabel",
            "Widget_Template/EnterURLPlaceholder",
            "Widget_Template_ErrorMessage/RepositoryURL",
            "Widget_Template/PRMine",
            "Widget_Template/PRAssigned",
            "Widget_Template/PRAll",
            "Widget_Template/PRDefaultView",
            "Widget_Template_ErrorMessage/QueryURL",
            "Widget_Template/QueryURLLabel",
            "Widget_Template/QueryTitlePlaceholder",
            "Widget_Template/QueryTitleLabel",
            "Widget_Template/NumberOfTiles",
            "Widget_Template_Button/AddTile",
            "Widget_Template_Button/RemoveTile",
            "Widget_Template/CanBePinned",
            "Widget_Template/WorkItems",
            "Widget_Template_Tooltip/ClickTile",
            "Widget_Template/EmptyWorkItems",
            "Widget_Template/NotShownItems",
        };
    }
}
