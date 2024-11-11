// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace DevHomeAzureExtension.Providers;

internal sealed class SettingsCardData
{
    public string NotificationsEnabled { get; set; } = string.Empty;

    public string CacheLastUpdated { get; set; } = string.Empty;

    public string UpdateAzureData { get; set; } = string.Empty;
}
