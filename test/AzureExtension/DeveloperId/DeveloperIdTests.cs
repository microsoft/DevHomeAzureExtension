// Copyright (c) Microsoft Corporation and Contributors
// Licensed under the MIT license.

using DevHomeAzureExtension.DeveloperId;

namespace DevHomeAzureExtension.Test;

public partial class DeveloperIdTests
{
    [TestMethod]
    [TestCategory("Manual")]
    [Ignore("Comment out to run")]
    public void DevId_Manual_LoginLogoutEvents()
    {
        // Getting instance the first time will also do the login.
        var authProvider = DeveloperIdProvider.GetInstance();

        // Register for Login and Logout Events
        authProvider.Changed += AuthenticationEvent;

        // Start a new login flow. Control flows to browser.
        // Task.Run(async () => { await authProvider.LoginNewDeveloperIdAsync(); });

        // Get the list of DeveloperIds
        var devIds = authProvider.GetLoggedInDeveloperIdsInternal();
        Assert.IsNotNull(devIds);
        Assert.AreEqual(devIds.Count(), 1);

        // Logout
        authProvider.LogoutDeveloperId(devIds.First());

        // Wait 1 sec for the Logout event.
        AuthenticationEventTriggered.WaitOne(1000);

        // Get the list of DeveloperIds
        devIds = authProvider.GetLoggedInDeveloperIdsInternal();
        Assert.IsNotNull(devIds);
        Assert.AreEqual(devIds.Count(), 0);
    }
}
