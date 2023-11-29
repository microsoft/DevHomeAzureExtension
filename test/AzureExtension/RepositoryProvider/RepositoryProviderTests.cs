// Copyright (c) Microsoft Corporation and Contributors
// Licensed under the MIT license.

using Microsoft.Extensions.Hosting;
using Microsoft.Windows.DevHome.SDK;

namespace DevHomeAzureExtension.Test;

public partial class RepositoryProviderTests
{
    [TestMethod]
    [TestCategory("Unit")]
    public void ValidateCanGetProvider()
    {
        var manualResetEvent = new ManualResetEvent(false);
        var host = Microsoft.Extensions.Hosting.Host.CreateDefaultBuilder().Build();
        var azureExtension = new AzureExtension(manualResetEvent, host);
        var repositoryProvider = azureExtension.GetProvider(ProviderType.Repository);
        Assert.IsNotNull(repositoryProvider);
        Assert.IsNotNull(repositoryProvider as IRepositoryProvider);
    }

    [TestMethod]
    [TestCategory("Unit")]
    public void CanParseGoodURL()
    {
        // TO DO: Implement for Azure Extension
    }

    [TestMethod]
    [TestCategory("Unit")]
    public void CanClone()
    {
        var manualResetEvent = new ManualResetEvent(false);
        var host = Microsoft.Extensions.Hosting.Host.CreateDefaultBuilder().Build();
        var azureExtension = new AzureExtension(manualResetEvent, host);
        var repositoryObject = azureExtension.GetProvider(ProviderType.Repository);
        Assert.IsNotNull(repositoryObject);

        var repositoryProvider = repositoryObject as IRepositoryProvider;
        Assert.IsNotNull(repositoryProvider);

        // Not supported yet, verify the call to parse URI returns false.
        var result = repositoryProvider.IsUriSupportedAsync(new Uri(WASDK_URL)).AsTask().Result;
        Assert.IsFalse(result.IsSupported);
    }

    [TestMethod]
    [TestCategory("Unit")]
    public void CloneViaMakingRepoObject()
    {
        IRepository mockRepo = new Mocks.MockRepository();
        Assert.IsNotNull(mockRepo);
    }
}
