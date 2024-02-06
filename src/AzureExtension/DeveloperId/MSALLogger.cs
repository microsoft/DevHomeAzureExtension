// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Microsoft.IdentityModel.Abstractions;

namespace DevHomeAzureExtension.DeveloperId;

// The below class is used in MSAL.NET logging for better diagnosability of issues (see more here: https://learn.microsoft.com/en-us/entra/msal/dotnet/advanced/exceptions/msal-logging)
public class MSALLogger : IIdentityLogger
{
    private readonly EventLogLevel _minLogLevel = EventLogLevel.LogAlways;

    public MSALLogger(EventLogLevel minLogLevel = EventLogLevel.LogAlways)
    {
        _minLogLevel = minLogLevel;
    }

    public bool IsEnabled(EventLogLevel eventLogLevel)
    {
        return eventLogLevel >= _minLogLevel;
    }

    public void Log(LogEntry entry)
    {
        DevHomeAzureExtension.DeveloperId.Log.Logger()?.ReportDebug($"MSAL: EventLogLevel: {entry.EventLogLevel}, Message: {entry.Message} ");
    }
}
