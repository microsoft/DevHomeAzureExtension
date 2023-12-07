// Copyright (c) Microsoft Corporation and Contributors
// Licensed under the MIT license.

using AzureExtension.Test.DeveloperId.Mocks;
using DevHomeAzureExtension.DeveloperId;
using Microsoft.Windows.DevHome.SDK;

namespace DevHomeAzureExtension.Test;

public partial class DeveloperIdTests
{
    [TestMethod]
    [TestCategory("Unit")]
    public void DeveloperIdProvider_AuthenticationExperienceKind()
    {
        // dependency injection
        var devIdProvider = DeveloperIdProvider.GetInstance(new MockAuthenticationHelper());
        Assert.IsNotNull(devIdProvider);
        Assert.AreEqual(AuthenticationExperienceKind.CustomProvider, devIdProvider.GetAuthenticationExperienceKind());
    }
}
