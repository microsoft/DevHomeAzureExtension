﻿// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Diagnostics;
using System.Globalization;
using System.Text;
using Microsoft.Windows.AppNotifications;
using Serilog;

namespace DevHomeAzureExtension.Notifications;

public class NotificationHandler
{
    private static readonly Lazy<ILogger> _logger = new(() => Serilog.Log.ForContext("SourceContext", nameof(NotificationHandler)));

    private static readonly ILogger _log = _logger.Value;

#pragma warning disable IDE0060 // Remove unused parameter
    public static void OnNotificationInvoked(object sender, AppNotificationActivatedEventArgs args) => NotificationActivation(args);
#pragma warning restore IDE0060 // Remove unused parameter

    public static void NotificationActivation(AppNotificationActivatedEventArgs args)
    {
        _log.Information($"Notification Activated with args: {NotificationArgsToString(args)}");

        if (args.Arguments.TryGetValue("htmlurl", out var urlString))
        {
            try
            {
                // Do not assume this string is a safe URL and blindly execute it; verify that it is
                // in fact a valid Azure URL.
                // TODO: Validate Azure URL
                _log.Information($"Launching Uri: {urlString}");
                var processStartInfo = new ProcessStartInfo
                {
                    UseShellExecute = true,
                    FileName = urlString,
                };

                Process.Start(processStartInfo);
            }
            catch (Exception ex)
            {
                _log.Error(ex, $"Failed launching Uri for {args.Arguments["htmlurl"]}");
            }

            return;
        }
    }

    public static string NotificationArgsToString(AppNotificationActivatedEventArgs args)
    {
        var sb = new StringBuilder();
        foreach (var arg in args.Arguments)
        {
            sb.Append(CultureInfo.InvariantCulture, $"{arg.Key}={arg.Value} ");
        }

        return sb.ToString();
    }
}
