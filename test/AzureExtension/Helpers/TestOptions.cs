﻿// Copyright (c) Microsoft Corporation and Contributors
// Licensed under the MIT license.

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
