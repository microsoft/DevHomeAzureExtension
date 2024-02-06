// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using DevHome.Logging;
using Jeffijoe.MessageFormat;

namespace DevHomeAzureExtension.Helpers;

internal class TimeSpanHelper
{
    public static string TimeSpanToDisplayString(TimeSpan timeSpan, Logger? log = null)
    {
        if (timeSpan.TotalSeconds < 1)
        {
            return Resources.GetResource("Widget_Template_Now", log);
        }

        if (timeSpan.TotalMinutes < 1)
        {
            return MessageFormatter.Format(Resources.GetResource("Widget_Template_SecondsAgo", log), new { seconds = timeSpan.Seconds });
        }

        if (timeSpan.TotalHours < 1)
        {
            return MessageFormatter.Format(Resources.GetResource("Widget_Template_MinutesAgo", log), new { minutes = timeSpan.Minutes });
        }

        if (timeSpan.TotalDays < 1)
        {
            return MessageFormatter.Format(Resources.GetResource("Widget_Template_HoursAgo", log), new { hours = timeSpan.Hours });
        }

        return MessageFormatter.Format(Resources.GetResource("Widget_Template_DaysAgo", log), new { days = timeSpan.Days });
    }

    internal static string DateTimeOffsetToDisplayString(DateTimeOffset? dateTime, Logger? log)
    {
        if (dateTime == null)
        {
            return Resources.GetResource("Widget_Template_UnknownTime", log);
        }

        return TimeSpanToDisplayString(DateTime.UtcNow - dateTime.Value.DateTime, log);
    }
}
