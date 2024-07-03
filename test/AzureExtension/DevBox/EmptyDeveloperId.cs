// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Microsoft.Windows.DevHome.SDK;

namespace DevHomeAzureExtension.Test.DevBox;

public class EmptyDeveloperId : IDeveloperId
{
    public string LoginId => string.Empty;

    public string Url => string.Empty;
}
