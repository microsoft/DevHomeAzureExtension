// Copyright (c) Microsoft Corporation and Contributors
// Licensed under the MIT license.

using DevHome.Logging;
using Windows.Storage;

namespace DevHomeAzureExtension.Client;

[System.Diagnostics.CodeAnalysis.SuppressMessage("StyleCop.CSharp.DocumentationRules", "SA1649:File name should match first type name", Justification = "Logging project is shared")]
public class Log
{
    private static readonly string _loggerName = "Client";

    private static Logger? _logger;

    public static Logger? Logger()
    {
        try
        {
            _logger ??= new Logger(_loggerName, GetLoggingOptions());
        }
        catch
        {
            // Do nothing if logger fails.
        }

        return _logger;
    }

    public static void Attach(Logger logger)
    {
        if (logger is not null)
        {
            _logger?.Dispose();
            _logger = logger;
        }
    }

    public static Options GetLoggingOptions()
    {
        return new Options
        {
            LogFileFolderRoot = ApplicationData.Current.TemporaryFolder.Path,
            LogFileName = $"{_loggerName}_{{now}}.log",
            LogFileFolderName = _loggerName,
            DebugListenerEnabled = true,
#if DEBUG
            LogStdoutEnabled = true,
            LogStdoutFilter = SeverityLevel.Debug,
            LogFileFilter = SeverityLevel.Debug,
#else
            LogStdoutEnabled = false,
            LogStdoutFilter = SeverityLevel.Info,
            LogFileFilter = SeverityLevel.Info,
#endif
            FailFastSeverity = FailFastSeverityLevel.Critical,
        };
    }
}
