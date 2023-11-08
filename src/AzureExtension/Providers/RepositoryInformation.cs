// Copyright (c) Microsoft Corporation and Contributors
// Licensed under the MIT license.

namespace AzureExtension.Providers;

public class RepositoryInformation
{
    private enum UriForm
    {
        New,
        Old,
    }

    public string Organization
    {
        get;
    }

    public string Project
    {
        get;
    }

    public string RepoName
    {
        get;
    }

    public Uri OrganizationLink
    {
        get;
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="RepositoryInformation"/> class.
    /// Parses uri into org, project, repo name and org link.
    /// </summary>
    /// <param name="repoUri">The uri to the repo.</param>
    /// <exception cref="ArgumentException">If _git isn't in the uri</exception>
    public RepositoryInformation(Uri repoUri)
    {
        // legacy
        // https://organization.visualstudio.com/DefaultCollection/project/_git/repository <- from clone window
        // https://organization.visualstudio.com/project/_git/repository <- from URL window

        // New
        // https://organization@dev.azure.com/organization/project/_git/repository <- from clone window
        // https://dev.azure.com/organization/project/_git/repository <- from repo url window
        var segments = repoUri.Segments;
        var locationOfGit = -1;
        for (var index = 0; index < segments.Length; index++)
        {
            if (segments[index].StartsWith("_git", StringComparison.OrdinalIgnoreCase))
            {
                locationOfGit = index;
                break;
            }
        }

        if (locationOfGit == -1)
        {
            throw new ArgumentException("Could not find _git");
        }

        var uriForm = WhatForm(repoUri);
        Organization = GetOrganization(repoUri, uriForm);
        Project = GetProject(repoUri, locationOfGit);
        RepoName = GetRepoName(repoUri, locationOfGit);
        OrganizationLink = GetHttpsOrganizationLink(repoUri, uriForm, locationOfGit);
    }

    private UriForm WhatForm(Uri repoUri)
    {
        var uriHost = repoUri.Host;
        if (uriHost.Contains("dev.azure.com", StringComparison.OrdinalIgnoreCase))
        {
            return UriForm.New;
        }
        else if (uriHost.Contains("visualstudio.com", StringComparison.OrdinalIgnoreCase))
        {
            return UriForm.Old;
        }
        else
        {
            throw new ArgumentException("Uri is neither the old or new form");
        }
    }

    private string GetOrganization(Uri repoUri, UriForm uriForm)
    {
        if (uriForm == UriForm.New)
        {
            // https://organization@dev.azure.com/organization/project/_git/repository <- from clone window
            // https://dev.azure.com/organization/project/_git/repository <- from repo url window
            return repoUri.Segments[1].Replace("/", string.Empty);
        }
        else if (uriForm == UriForm.Old)
        {
            // https://organization.visualstudio.com/DefaultCollection/project/_git/repository <- from clone window
            // https://organization.visualstudio.com/project/_git/repository <- from URL window
            return repoUri.Host.Substring(0, repoUri.Host.IndexOf('.'));
        }
        else
        {
            throw new ArgumentException("Uri is not old or new form");
        }
    }

    private string GetProject(Uri repoUri, int locationOfGit)
    {
        // New
        // https://organization@dev.azure.com/organization/project/_git/repository <- from clone window
        // https://dev.azure.com/organization/project/_git/repository <- from repo url window

        // Legacy
        // https://organization.visualstudio.com/DefaultCollection/project/_git/repository <- from clone window
        // https://organization.visualstudio.com/project/_git/repository <- from URL window
        return repoUri.Segments[locationOfGit - 1].Replace("/", string.Empty);
    }

    private string GetRepoName(Uri repoUri, int locationOfGit)
    {
        // New
        // https://organization@dev.azure.com/organization/project/_git/repository <- from clone window
        // https://dev.azure.com/organization/project/_git/repository <- from repo url window

        // Legacy
        // https://organization.visualstudio.com/DefaultCollection/project/_git/repository <- from clone window
        // https://organization.visualstudio.com/project/_git/repository <- from URL window
        return repoUri.Segments[locationOfGit + 1];
    }

    private Uri GetHttpsOrganizationLink(Uri repoUri, UriForm uriForm, int locationOfGit)
    {
        if (uriForm == UriForm.New)
        {
            // New
            // https://organization@dev.azure.com/organization/project/_git/repository <- from clone window
            // https://dev.azure.com/organization/project/_git/repository <- from repo url window
            var orgUriAsString = repoUri.Scheme + "://" + repoUri.Host + "/" + this.Organization;
            Uri? orgUri;
            if (!Uri.TryCreate(orgUriAsString, UriKind.Absolute, out orgUri))
            {
                throw new ArgumentException("Could not make Org Uri");
            }

            return orgUri;
        }
        else if (uriForm == UriForm.Old)
        {
            // Legacy
            // https://organization@dev.azure.com/organization/project/_git/repository <- from clone window
            // https://dev.azure.com/organization/project/_git/repository <- from repo url window
            var orgUriAsString = repoUri.Scheme + "://" + repoUri.Host;
            Uri? orgUri;
            if (!Uri.TryCreate(orgUriAsString, UriKind.Absolute, out orgUri))
            {
                throw new ArgumentException("Could not make Org Uri");
            }

            return orgUri;
        }
        else
        {
            throw new ArgumentException("Uri is not old or new form");
        }
    }
}
