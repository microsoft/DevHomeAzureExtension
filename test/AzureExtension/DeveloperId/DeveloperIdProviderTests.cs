// Copyright (c) Microsoft Corporation and Contributors
// Licensed under the MIT license.

using DevHomeAzureExtension.DeveloperId;
using DevHomeAzureExtension.Test.DeveloperId.Mocks;
using Microsoft.Windows.DevHome.SDK;

namespace DevHomeAzureExtension.Test;

public partial class DeveloperIdTests
{
    [TestMethod]
    [TestCategory("Unit")]
    public void DeveloperIdProvider_SingletonTest()
    {
        // Ensure that the DeveloperIdProvider is a singleton
        var developerIdProvider1 = DeveloperIdProvider.GetInstance(new MockAuthenticationHelper());
        Assert.IsNotNull(developerIdProvider1);
        Assert.IsInstanceOfType(developerIdProvider1, typeof(DeveloperIdProvider));

        var developerIdProvider2 = DeveloperIdProvider.GetInstance(new MockAuthenticationHelper());
        Assert.IsNotNull(developerIdProvider2);
        Assert.IsInstanceOfType(developerIdProvider2, typeof(DeveloperIdProvider));

        Assert.AreSame(developerIdProvider1, developerIdProvider2);
    }

    [TestMethod]
    [TestCategory("Unit")]
    public void DeveloperIdProvider_AuthenticationExperienceKind()
    {
        // dependency injection
        var developerIdProvider = DeveloperIdProvider.GetInstance(new MockAuthenticationHelper());
        Assert.IsNotNull(developerIdProvider);
        Assert.AreEqual(AuthenticationExperienceKind.CustomProvider, developerIdProvider.GetAuthenticationExperienceKind());
    }

    [TestMethod]
    [TestCategory("Unit")]
    public void DeveloperIdProvider_GetDeveloperIds_Empty()
    {
        Task.Run(() =>
        {
            var mockAuthenticationHelper = new MockAuthenticationHelper();
            var developerIdProvider = DeveloperIdProvider.GetInstance(mockAuthenticationHelper);

            Assert.IsNotNull(developerIdProvider);

            var result = developerIdProvider.GetLoggedInDeveloperIds();
            Assert.IsNotNull(result);
            Assert.AreEqual(ProviderOperationStatus.Success, result.Result.Status);
            Assert.AreEqual(0, result.DeveloperIds.Count());

            developerIdProvider.Dispose();
        });
    }

    [TestMethod]
    [TestCategory("Unit")]
    public void DeveloperIdProvider_GetDeveloperIds_NotEmpty()
    {
        Task.Run(() =>
        {
            var mockAuthenticationHelper = new MockAuthenticationHelper();
            mockAuthenticationHelper.PopulateCache();
            var developerIdProvider = DeveloperIdProvider.GetInstance(mockAuthenticationHelper);
            Assert.IsNotNull(developerIdProvider);

            var result = developerIdProvider.GetLoggedInDeveloperIds();
            Assert.IsNotNull(result);
            Assert.AreEqual(ProviderOperationStatus.Success, result.Result.Status);
            Assert.AreEqual(1, result.DeveloperIds.Count());
            Assert.AreEqual("testAccount", result.DeveloperIds.First().LoginId);
            developerIdProvider.Dispose();
        });
    }

    [TestMethod]
    [TestCategory("Unit")]
    public void DeveloperIdProvider_GetDeveloperIdState_LoggedIn()
    {
        Task.Run(() =>
        {
            var mockAuthenticationHelper = new MockAuthenticationHelper();
            mockAuthenticationHelper.PopulateCache();
            var developerIdProvider = DeveloperIdProvider.GetInstance(mockAuthenticationHelper);
            Assert.IsNotNull(developerIdProvider);

            var result = developerIdProvider.GetLoggedInDeveloperIds();
            Assert.IsNotNull(result);
            Assert.AreEqual(ProviderOperationStatus.Success, result.Result.Status);
            Assert.AreEqual(1, result.DeveloperIds.Count());
            Assert.AreEqual("testAccount", result.DeveloperIds.First().LoginId);

            var developerIdStateResult = developerIdProvider.GetDeveloperIdState(result.DeveloperIds.First());
            Assert.IsNotNull(result);
            Assert.AreEqual(AuthenticationState.LoggedIn, developerIdStateResult);
            developerIdProvider.Dispose();
        });
    }

    [TestMethod]
    [TestCategory("Unit")]
    public void DeveloperIdProvider_GetDeveloperIdState_LoggedOut()
    {
        Task.Run(() =>
        {
            var developerIdProvider = DeveloperIdProvider.GetInstance();
            Assert.IsNotNull(developerIdProvider);

            var result = developerIdProvider.GetDeveloperIdState(new DevHomeAzureExtension.DeveloperId.DeveloperId());
            Assert.IsNotNull(result);
            Assert.AreEqual(AuthenticationState.LoggedOut, result);
        });
    }

    [TestMethod]
    [TestCategory("Unit")]
    public void DeveloperIdProvider_ShowLogonSession()
    {
        Task.Run(async () =>
        {
            var developerIdProvider = DeveloperIdProvider.GetInstance(new MockAuthenticationHelper());
            Assert.IsNotNull(developerIdProvider);

            var result = await developerIdProvider.ShowLogonSession(default(Microsoft.UI.WindowId));
            Assert.IsNotNull(result);
            Assert.AreEqual(ProviderOperationStatus.Success, result.Result.Status);
        });
    }

    [TestMethod]
    [TestCategory("Unit")]
    public void DeveloperIdProvider_LogoutDeveloperId_Success()
    {
        Task.Run(() =>
        {
            var mockAuthenticationHelper = new MockAuthenticationHelper();
            mockAuthenticationHelper.PopulateCache();
            var developerIdProvider = DeveloperIdProvider.GetInstance(mockAuthenticationHelper);
            Assert.IsNotNull(developerIdProvider);

            var resultForGetLoggedInDeveloperIds = developerIdProvider.GetLoggedInDeveloperIds();
            Assert.IsNotNull(resultForGetLoggedInDeveloperIds);
            Assert.AreEqual(ProviderOperationStatus.Success, resultForGetLoggedInDeveloperIds.Result.Status);
            Assert.AreEqual(1, resultForGetLoggedInDeveloperIds.DeveloperIds.Count());
            var developerIdToLogout = resultForGetLoggedInDeveloperIds.DeveloperIds.First();
            Assert.IsNotNull(developerIdToLogout);

            var resultForLogoutDeveloperId = developerIdProvider.LogoutDeveloperId(developerIdToLogout);
            Assert.IsNotNull(resultForLogoutDeveloperId);
            Assert.AreEqual(ProviderOperationStatus.Success, resultForLogoutDeveloperId.Status);
            Assert.AreEqual(AuthenticationState.LoggedOut, developerIdProvider.GetDeveloperIdState(developerIdToLogout));

            developerIdProvider.Dispose();
        });
    }

    [TestMethod]
    [TestCategory("Unit")]
    public void DeveloperIdProvider_LogoutDeveloperId_InvalidDeveloperId()
    {
        Task.Run(() =>
        {
            var developerIdProvider = DeveloperIdProvider.GetInstance();
            Assert.IsNotNull(developerIdProvider);

            var result = developerIdProvider.LogoutDeveloperId(new DevHomeAzureExtension.DeveloperId.DeveloperId());
            Assert.IsNotNull(result);
            Assert.AreEqual(ProviderOperationStatus.Failure, result.Status);
        });
    }

    [TestMethod]
    [TestCategory("Unit")]
    public void DeveloperIdProvider_DeveloperIdProvider_GetLoginAdaptiveCardSession_NeverImplemented()
    {
        var developerIdProvider = DeveloperIdProvider.GetInstance(new MockAuthenticationHelper());
        Assert.IsNotNull(developerIdProvider);
        AdaptiveCardSessionResult? result = null;
        try
        {
            result = developerIdProvider.GetLoginAdaptiveCardSession();
        }
        catch (Exception e)
        {
            Assert.IsInstanceOfType(e, typeof(NotImplementedException));
        }

        Assert.IsNull(result);
    }
}
