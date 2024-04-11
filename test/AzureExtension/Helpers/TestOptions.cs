// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using DevHome.Logging;
using DevHomeAzureExtension.DataModel;

namespace DevHomeAzureExtension.Test;

public partial class TestOptions
{
    public string LogFileFolderRoot { get; set; } = string.Empty;

    public string LogFileFolderName { get; set; } = "{now}";

    public string LogFileName { get; set; } = string.Empty;

    public string LogFileFolderPath => Path.Combine(LogFileFolderRoot, LogFileFolderName);

    public DataStoreOptions DataStoreOptions { get; set; }

    public TestOptions()
    {
        DataStoreOptions = new DataStoreOptions();
    }
}
