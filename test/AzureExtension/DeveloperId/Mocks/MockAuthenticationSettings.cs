// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using DevHomeAzureExtension.DeveloperId;

namespace AzureExtension.Test.DeveloperId.Mocks;

public class MockAuthenticationSettings : IAuthenticationSettings
{
    public void InitializeSettings()
    {
        // no settings to initalize for testing
    }
}
