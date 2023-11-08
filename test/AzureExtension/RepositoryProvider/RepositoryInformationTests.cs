// Copyright (c) Microsoft Corporation and Contributors
// Licensed under the MIT license.

using AzureExtension.Providers;

namespace DevHomeAzureExtension.Test;

[TestClass]
public class RepositoryInformationTests
{
    private const string OrganizationName = "myorganization";
    private const string ProjectName = "myproject";
    private const string RepositoryName = "myrepo";

    [TestMethod]
    [TestCategory("Unit")]
    public void ParseOldForm()
    {
        // From browser box
        var url = $"https://{OrganizationName}.visualstudio.com/{ProjectName}/_git/{RepositoryName}";
        var repoInformation = new RepositoryInformation(new Uri(url));

        Assert.AreEqual(OrganizationName, repoInformation.Organization);
        Assert.AreEqual(new Uri($"https://{OrganizationName}.visualstudio.com"), repoInformation.OrganizationLink);
        Assert.AreEqual(ProjectName, repoInformation.Project);
        Assert.AreEqual(RepositoryName, repoInformation.RepoName);

        // From clone button
        url = $"https://{OrganizationName}.visualstudio.com/DefaultCollection/{ProjectName}/_git/{RepositoryName}";
        repoInformation = new RepositoryInformation(new Uri(url));

        Assert.AreEqual(OrganizationName, repoInformation.Organization);
        Assert.AreEqual(new Uri($"https://{OrganizationName}.visualstudio.com"), repoInformation.OrganizationLink);
        Assert.AreEqual(ProjectName, repoInformation.Project);
        Assert.AreEqual(RepositoryName, repoInformation.RepoName);
    }

    [TestMethod]
    [TestCategory("Unit")]
    public void ParseNewForm()
    {
        // From browser box
        var url = $"https://dev.azure.com/{OrganizationName}/{ProjectName}/_git/{RepositoryName}";
        var repoInformation = new RepositoryInformation(new Uri(url));

        Assert.AreEqual(OrganizationName, repoInformation.Organization);
        Assert.AreEqual(new Uri($"https://dev.azure.com/{OrganizationName}"), repoInformation.OrganizationLink);
        Assert.AreEqual(ProjectName, repoInformation.Project);
        Assert.AreEqual(RepositoryName, repoInformation.RepoName);

        // From clone button
        url = $"https://{OrganizationName}@dev.azure.com/{OrganizationName}/{ProjectName}/_git/{RepositoryName}";
        repoInformation = new RepositoryInformation(new Uri(url));

        Assert.AreEqual(OrganizationName, repoInformation.Organization);
        Assert.AreEqual(new Uri($"https://dev.azure.com/{OrganizationName}"), repoInformation.OrganizationLink);
        Assert.AreEqual(ProjectName, repoInformation.Project);
        Assert.AreEqual(RepositoryName, repoInformation.RepoName);
    }
}
