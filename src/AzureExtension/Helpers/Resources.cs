﻿// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Globalization;
using Microsoft.Windows.ApplicationModel.Resources;
using Serilog;
using Windows.Win32;
using Windows.Win32.Foundation;

namespace DevHomeAzureExtension.Helpers;

public static class Resources
{
    private const int MaxBufferLength = 1024;

    private static ResourceLoader? _resourceLoader;

    public static string GetResource(string identifier, ILogger? log = null)
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
            log?.Error(ex, $"Failed loading resource: {identifier}");

            // If we fail, load the original identifier so it is obvious which resource is missing.
            return identifier;
        }
    }

    /// <summary>
    /// Gets the localized string of a resource key.
    /// </summary>
    /// <param name="key">Resource key.</param>
    /// <param name="args">Placeholder arguments.</param>
    /// <returns>Localized value, or resource key if the value is empty or an exception occurred.</returns>
    public static string GetResource(string key, params object[] args)
    {
        string value;

        try
        {
            value = GetResource(key);
            value = string.Format(CultureInfo.CurrentCulture, value, args);
        }
        catch
        {
            value = string.Empty;
        }

        return string.IsNullOrEmpty(value) ? key : value;
    }

    // Replaces all identifiers in the provided list in the target string. Assumes all identifiers
    // are wrapped with '%' to prevent sub-string replacement errors. This is intended for strings
    // such as a JSON string with resource identifiers embedded.
    public static string ReplaceIdentifiers(string str, string[] resourceIdentifiers, ILogger? log = null)
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
        log?.Debug($"Replaced identifiers in {elapsed.TotalMilliseconds}ms");
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
            "Widget_Template/WidgetTitlePlaceholder",
            "Widget_Template/WidgetTitleLabel",
            "Widget_Template/NumberOfTiles",
            "Widget_Template_Button/AddTile",
            "Widget_Template_Button/RemoveTile",
            "Widget_Template/CanBePinned",
            "Widget_Template/WorkItems",
            "Widget_Template_Tooltip/ClickTile",
            "Widget_Template/EmptyWorkItems",
            "Widget_Template/NotShownItems",
            "Widget_Template/ContentLoading",
        };
    }

    /// <summary>
    /// Gets a string or the absolute file path of an asset location within a package.
    /// </summary>
    /// <param name="resource">the ms-resource:// path to a resource in an app package's pri file.</param>
    /// <param name="packageFullName">the package containing the resource.</param>
    /// <returns>The retrieved string represented by the resource key.</returns>
    public static unsafe string GetResourceFromPackage(string resource, string packageFullName)
    {
        var indirectPathToResource = "@{" + packageFullName + "?" + resource + "}";
        Span<char> outputBuffer = new char[MaxBufferLength];

        fixed (char* outBufferPointer = outputBuffer)
        {
            fixed (char* resourcePathPointer = indirectPathToResource)
            {
                var res = PInvoke.SHLoadIndirectString(resourcePathPointer, new PWSTR(outBufferPointer), (uint)outputBuffer.Length);
                res.ThrowOnFailure();
                return new string(outputBuffer.TrimEnd('\0'));
            }
        }
    }
}
