// Copyright (c) Microsoft Corporation and Contributors
// Licensed under the MIT license.

using AzureExtension.Providers;
using Microsoft.TeamFoundation.SourceControl.WebApi;

namespace DevHomeAzureExtension.Providers;

public class DevHomeRepository : Microsoft.Windows.DevHome.SDK.IRepository
{
    private readonly string name;

    private readonly Uri cloneUrl;

    private readonly bool _isPrivate;

    private readonly DateTimeOffset _lastUpdated;

    string Microsoft.Windows.DevHome.SDK.IRepository.DisplayName => name;

    public string OwningAccountName => _owningAccountName;

    private readonly string _owningAccountName = string.Empty;

    public bool IsPrivate => _isPrivate;

    public DateTimeOffset LastUpdated => _lastUpdated;

    public Uri RepoUri => cloneUrl;

    /// <summary>
    /// Initializes a new instance of the <see cref="DevHomeRepository"/> class.
    /// </summary>
    /// <remarks>
    /// This can throw.
    /// </remarks>
    public DevHomeRepository(GitRepository gitRepository)
    {
        name = gitRepository.Name;

        Uri? localUrl;
        if (!Uri.TryCreate(gitRepository.WebUrl, UriKind.RelativeOrAbsolute, out localUrl))
        {
            throw new ArgumentException("URl is null");
        }

        var repoInformation = new RepositoryInformation(localUrl);
        _owningAccountName = Path.Join(repoInformation.Organization, repoInformation.RepoName);

        cloneUrl = localUrl;

        _isPrivate = gitRepository.ProjectReference.Visibility == Microsoft.TeamFoundation.Core.WebApi.ProjectVisibility.Private;
        _lastUpdated = DateTimeOffset.UtcNow;
    }
}
