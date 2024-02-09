// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using DevHome.Logging;
using DevHomeAzureExtension.DataModel;

namespace DevHomeAzureExtension.Test;

public partial class TestOptions
{
    public Options LogOptions { get; set; }

    public DataStoreOptions DataStoreOptions { get; set; }

    public TestOptions()
    {
        LogOptions = new Options();
        DataStoreOptions = new DataStoreOptions();
    }
}
