// Copyright (c) Microsoft Corporation and Contributors
// Licensed under the MIT license.

using Microsoft.Extensions.Hosting;
using Microsoft.Windows.DevHome.SDK;
using static DevHomeAzureExtension.Test.WidgetTests;

namespace DevHomeAzureExtension.Test;

public partial class RepositoryProviderTests
{
    private const string LegacyOrganizationLink = "https://organization.visualstudio.com";
    private const string ModernOrganizationLink = "https://dev.azure.com/organization";
    private const string OrganizationName = "organization";
    private const string ProjectName = "project";
    private const string RepositoryName = "repository";

    private List<Tuple<string, bool>> _validUrls = new();

    private List<Tuple<string, bool>> GetValidUrls()
    {
        if (_validUrls.Any())
        {
            return _validUrls;
        }

        List<Tuple<string, bool>> azureUris = new();
        azureUris.Add(new Tuple<string, bool>("https://organization@dev.azure.com/organization/project/_git/repository", false));
        azureUris.Add(new Tuple<string, bool>("https://organization@dev.azure.com/organization/project/_git/repository/", false));
        azureUris.Add(new Tuple<string, bool>("https://dev.azure.com/organization/project/_git/repository", false));
        azureUris.Add(new Tuple<string, bool>("https://dev.azure.com/organization/project/_git/repository/", false));
        azureUris.Add(new Tuple<string, bool>("https://dev.azure.com/organization/project/_git/repository/some/other/stuff", false));
        azureUris.Add(new Tuple<string, bool>("https://dev.azure.com/organization/collection/project/_git/repository", false));
        azureUris.Add(new Tuple<string, bool>("https://dev.azure.com/organization/collection/project/_git/repository/", false));
        azureUris.Add(new Tuple<string, bool>("https://dev.azure.com/organization/collection/project/_git/repository/some/other/stuff", false));
        azureUris.Add(new Tuple<string, bool>("https://organization.visualstudio.com/project/_git/repository", false));
        azureUris.Add(new Tuple<string, bool>("https://organization.visualstudio.com/project/_git/repository/", false));
        azureUris.Add(new Tuple<string, bool>("https://organization.visualstudio.com/project/_git/repository/some/other/stuff", false));
        azureUris.Add(new Tuple<string, bool>("https://organization.visualstudio.com/collection/project/_git/repository", false));
        azureUris.Add(new Tuple<string, bool>("https://organization.visualstudio.com/collection/project/_git/repository/", false));
        azureUris.Add(new Tuple<string, bool>("https://organization.visualstudio.com/collection/project/_git/repository/some/other/stuff", false));

        // Repositories where repository name = project name.
        azureUris.Add(new Tuple<string, bool>("https://dev.azure.com/organization/_git/project", true));
        azureUris.Add(new Tuple<string, bool>("https://dev.azure.com/organization/_git/project/", true));
        azureUris.Add(new Tuple<string, bool>("https://dev.azure.com/organization/_git/project/some/other/stuff", true));
        azureUris.Add(new Tuple<string, bool>("https://organization.visualstudio.com/_git/project", true));
        azureUris.Add(new Tuple<string, bool>("https://organization.visualstudio.com/_git/project/", true));
        azureUris.Add(new Tuple<string, bool>("https://organization.visualstudio.com/_git/project/some/other/stuff", true));

        _validUrls = azureUris;

        return _validUrls;
    }

    private List<string> _invalidUrls = new();

    private List<string> GetInvalidUrls()
    {
        if (_invalidUrls.Any())
        {
            return _invalidUrls;
        }

        _invalidUrls.Add("https://www.microsoft.com");
        _invalidUrls.Add("https://github.com/owner/repo/pulls?q=is%3Aopen+mentions%3A%40me");
        return _invalidUrls;
    }

    private List<string> _validNotRepoUrls = new();

    private List<string> GetValidNotRepoUrls()
    {
        if (_validNotRepoUrls.Any())
        {
            return _validNotRepoUrls;
        }

        _validNotRepoUrls.Add("https://dev.azure.com/organization/project/_workitems/recentlyupdated/");
        return _validNotRepoUrls;
    }

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
    public void CanParseURL()
    {
        foreach (var azureUriTuple in GetValidUrls())
        {
            TestContext?.WriteLine($"Testing {azureUriTuple.Item1}");
            var myAzureUri = new AzureUri(azureUriTuple.Item1);
            Assert.IsTrue(myAzureUri.IsValid);
            Assert.IsTrue(myAzureUri.IsRepository);

            Assert.AreEqual(myAzureUri.Organization, OrganizationName);
            Assert.AreEqual(myAzureUri.Project, ProjectName);

            // The project is the same name as the repo
            if (azureUriTuple.Item2)
            {
                Assert.AreEqual(myAzureUri.Repository, ProjectName);
            }
            else
            {
                Assert.AreEqual(myAzureUri.Repository, RepositoryName);
            }

            if (myAzureUri.HostType == AzureHostType.Legacy)
            {
                Assert.AreEqual(myAzureUri.OrganizationLink.OriginalString, LegacyOrganizationLink);
            }
            else if (myAzureUri.HostType == AzureHostType.Modern)
            {
                Assert.AreEqual(myAzureUri.OrganizationLink.OriginalString, ModernOrganizationLink);
            }
            else
            {
                Assert.Fail();
            }
        }

        foreach (var invalidUrl in GetInvalidUrls())
        {
            TestContext?.WriteLine($"Testing {invalidUrl}");
            var myAzureUri = new AzureUri(invalidUrl);
            Assert.IsFalse(myAzureUri.IsValid);
            Assert.IsFalse(myAzureUri.IsRepository);
        }

        foreach (var validNotRepositoryUrl in GetValidNotRepoUrls())
        {
            TestContext?.WriteLine($"Testing {validNotRepositoryUrl}");
            var myAzureUri = new AzureUri(validNotRepositoryUrl);
            Assert.IsTrue(myAzureUri.IsValid);
            Assert.IsFalse(myAzureUri.IsRepository);
        }
    }

    [TestMethod]
    [TestCategory("Unit")]
    public void TestIsUriSupported()
    {
        var manualResetEvent = new ManualResetEvent(false);
        var host = Microsoft.Extensions.Hosting.Host.CreateDefaultBuilder().Build();
        var azureExtension = new AzureExtension(manualResetEvent, host);
        var repositoryObject = azureExtension.GetProvider(ProviderType.Repository);
        Assert.IsNotNull(repositoryObject);

        var repositoryProvider = repositoryObject as IRepositoryProvider;
        Assert.IsNotNull(repositoryProvider);

        foreach (var azureUriTuple in GetValidUrls())
        {
            TestContext?.WriteLine($"Testing {azureUriTuple.Item1}");
            var resultObject = repositoryProvider.IsUriSupportedAsync(new Uri(azureUriTuple.Item1)).AsTask().Result;
            Assert.IsTrue(resultObject.IsSupported);
        }

        foreach (var invalidUrl in GetInvalidUrls())
        {
            TestContext?.WriteLine($"Testing {invalidUrl}");
            var resultObject = repositoryProvider.IsUriSupportedAsync(new Uri(invalidUrl)).AsTask().Result;
            Assert.IsFalse(resultObject.IsSupported);
        }

        foreach (var validNotRepositoryUrl in GetValidNotRepoUrls())
        {
            TestContext?.WriteLine($"Testing {validNotRepositoryUrl}");
            var resultObject = repositoryProvider.IsUriSupportedAsync(new Uri(validNotRepositoryUrl)).AsTask().Result;
            Assert.IsFalse(resultObject.IsSupported);
        }
    }

    [TestMethod]
    [TestCategory("Unit")]
    public void CloneViaMakingRepoObject()
    {
        IRepository mockRepo = new Mocks.MockRepository();
        Assert.IsNotNull(mockRepo);
    }
}
