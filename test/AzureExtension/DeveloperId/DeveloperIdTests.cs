// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

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

        // Start a new interactive login flow
        // var windowHandle = Application.Current.GetService<WindowEx>().GetWindowHandle();
        // var windowPtr = Win32Interop.GetWindowIdFromWindow(windowHandle);
        // Task.Run(async () => { await authProvider.ShowLogonSession(windowPtr); });

        // Get the list of DeveloperIds
        var devIds = authProvider.GetLoggedInDeveloperIdsInternal();
        Assert.IsNotNull(devIds);
        Assert.AreEqual(devIds.Count(), 1);

        // Logout
        authProvider.LogoutDeveloperId(devIds.First());

        // Wait 1 sec for the Logout event.
        _authenticationEventTriggered.WaitOne(1000);

        // Get the list of DeveloperIds
        devIds = authProvider.GetLoggedInDeveloperIdsInternal();
        Assert.IsNotNull(devIds);
        Assert.AreEqual(devIds.Count(), 0);
    }
}
