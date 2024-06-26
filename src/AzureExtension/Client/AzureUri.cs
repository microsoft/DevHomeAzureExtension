// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Serilog;

namespace DevHomeAzureExtension.Client;

// This class is using lazy initialization to extract data from the Uri exactly once and only
// if needed. It uses Lazy<T> with the function initializer, which is also thread-safe.
// All public properties are non-nullable and it handles null inputs in the constructor safely.
// The key property to always check is "IsValid" - if this is true you can trust the core
// data, like the organization and the connection. If it is false you cannot trust the connection.
// This is done so we can avoid having nullable types that you always need to check null on before
// using. Instead you just check if it's valid and can then use the object and trust the result.
public class AzureUri
{
    private const string ValidUriString = "https://www.microsoft.com/";

    private readonly bool _validUri;

    private readonly Lazy<AzureHostType> _hostType;

    private readonly Lazy<bool> _isValidFormat;

    private readonly Lazy<bool> _isValid;

    private readonly Lazy<string> _organization;

    private readonly Lazy<string> _project;

    private readonly Lazy<int> _apiSegmentIndex;

    private readonly Lazy<string> _apiSegment;

    private readonly Lazy<bool> _isRepository;

    private readonly Lazy<string> _repository;

    private readonly Lazy<bool> _isQuery;

    private readonly Lazy<string> _query;

    private readonly Lazy<Uri> _connection;

    /// <summary>
    /// ADO APIs need a specific URI for getting information about an organization.
    /// </summary>
    private readonly Lazy<Uri> _organizationLink;

    private static readonly Lazy<ILogger> _logger = new(() => Serilog.Log.ForContext("SourceContext", nameof(AzureUri)));

    private static readonly ILogger _log = _logger.Value;

    // Original input string, similar to Uri, but in cases of an invalid Uri they will be different.
    public string OriginalString { get; } = string.Empty;

    // Most public properties are to be non-nullable for ease of use.
    public Uri Uri { get; } = new(ValidUriString);

    // This is if the Uri format is valid, not if it is a valid Azure DevOps Uri.
    // This is private to avoid confusion.
    private bool IsValidFormat => _isValidFormat.Value;

    public bool IsValid => _isValid.Value;

    public AzureHostType HostType => _hostType.Value;

    public bool IsHosted => HostType switch
    {
        AzureHostType.Modern => true,
        AzureHostType.Legacy => true,
        _ => false,
    };

    public string Organization => _organization.Value;

    public string Project => _project.Value;

    public string APISegment => _apiSegment.Value;

    private int APISegmentIndex => _apiSegmentIndex.Value;

    public bool HasAPISegment => APISegmentIndex != -1;

    public bool IsRepository => _isRepository.Value;

    public string Repository => _repository.Value;

    public bool IsQuery => _isQuery.Value;

    public string Query => _query.Value;

    public Uri Connection => _connection.Value;

    public Uri OrganizationLink => _organizationLink.Value;

    // If an invalid input or Uri was passed in (null, empty string, etc), the object
    // is still valid, but the Uri will be DefaultUriString, not the original input,
    // so in the event of an invalid input ToString() will show the original input or
    // string.Empty in the case of null or invalid Uri.
    public override string ToString() => _validUri ? Uri.ToString() : OriginalString;

    public AzureUri(Uri? uri)
        : this()
    {
        if (uri == null)
        {
            OriginalString = string.Empty;
        }
        else
        {
            Uri = uri;
            OriginalString = uri.OriginalString;
            _validUri = true;
        }
    }

    public AzureUri(string? uriString)
        : this()
    {
        if (uriString == null)
        {
            OriginalString = string.Empty;
            return;
        }

        OriginalString = uriString;
        Uri? uri;
        var success = Uri.TryCreate(uriString, UriKind.Absolute, out uri);
        if (success)
        {
            Uri = uri!;
            _validUri = true;
        }
    }

    public AzureUri()
    {
        _isValidFormat = new Lazy<bool>(InitializeIsValidFormat);
        _isValid = new Lazy<bool>(InitializeIsValid);
        _hostType = new Lazy<AzureHostType>(InitializeAzureHostType);
        _organization = new Lazy<string>(InitializeOrganization);
        _apiSegmentIndex = new Lazy<int>(InitializeAPISegmentIndex);
        _apiSegment = new Lazy<string>(InitializeAPISegment);
        _project = new Lazy<string>(InitializeProject);
        _isRepository = new Lazy<bool>(InitializeIsRepository);
        _repository = new Lazy<string>(InitializeRepository);
        _isQuery = new Lazy<bool>(InitializeIsQuery);
        _query = new Lazy<string>(InitializeQuery);
        _connection = new Lazy<Uri>(InitializeConnection);
        _organizationLink = new Lazy<Uri>(InitializeOrganizationLink);
    }

    private AzureHostType InitializeAzureHostType()
    {
        if (!IsValidFormat)
        {
            return AzureHostType.NotHosted;
        }
        else if (Uri.Host.EndsWith("dev.azure.com", StringComparison.OrdinalIgnoreCase))
        {
            return AzureHostType.Modern;
        }
        else if (Uri.Host.EndsWith(".visualstudio.com", StringComparison.OrdinalIgnoreCase))
        {
            return AzureHostType.Legacy;
        }
        else
        {
            return AzureHostType.NotHosted;
        }
    }

    private bool InitializeIsValidFormat()
    {
        if (Uri.OriginalString == ValidUriString)
        {
            return false;
        }

        if (!Uri.IsAbsoluteUri)
        {
            return false;
        }

        // Hosted supports https, but on-prem can support http.
        if (Uri.Scheme != Uri.UriSchemeHttps && Uri.Scheme != Uri.UriSchemeHttp)
        {
            return false;
        }

        return true;
    }

    private bool InitializeIsValid()
    {
        if (!IsValidFormat)
        {
            return false;
        }

        switch (HostType)
        {
            case AzureHostType.Legacy:
                return true;

            case AzureHostType.Modern:
                if (Uri.Segments.Length >= 2)
                {
                    return true;
                }

                return false;

            case AzureHostType.NotHosted:
                return true;

            default:
                return false;
        }
    }

    private string InitializeOrganization()
    {
        if (!IsValid)
        {
            return string.Empty;
        }

        try
        {
            switch (HostType)
            {
                case AzureHostType.Modern:
                    return Uri.Segments[1].Replace("/", string.Empty);

                case AzureHostType.Legacy:
                    // Legacy format can have "vssps" in the uri, which we need to ignore for
                    // extracting the url
                    if (Uri.Host.EndsWith(".vssps.visualstudio.com", StringComparison.OrdinalIgnoreCase))
                    {
                        return Uri.Host.Replace(".vssps.visualstudio.com", string.Empty, StringComparison.OrdinalIgnoreCase);
                    }
                    else
                    {
                        return Uri.Host.Replace(".visualstudio.com", string.Empty, StringComparison.OrdinalIgnoreCase);
                    }

                case AzureHostType.NotHosted:
                    // Not hosted (i.e. On-prem server) can be anything, we assume it is the hostname.
                    return Uri.Host;

                default:
                    return string.Empty;
            }
        }
        catch (Exception e)
        {
            _log.Error(e, $"InitializeOrganization failed for Uri: {Uri}");
            return string.Empty;
        }
    }

    private int InitializeAPISegmentIndex()
    {
        // Find the first segment which starts with an underscore.
        for (var i = 1; i < Uri.Segments.Length; ++i)
        {
            if (Uri.Segments[i].StartsWith('_'))
            {
                return i;
            }
        }

        // -1 is not found.
        return -1;
    }

    private string InitializeAPISegment()
    {
        if (HasAPISegment)
        {
            return Uri.Segments[APISegmentIndex].Replace("/", string.Empty);
        }

        return string.Empty;
    }

    private string InitializeProject()
    {
        if (!IsValid)
        {
            return string.Empty;
        }

        // Project will be the segment preceding the API segment, if one exists.
        // If one does not exist, it will be the last segment.
        var targetSegment = APISegmentIndex > 1 ? APISegmentIndex - 1 : 1;
        var hostTypeOffset = 0;
        if (HostType == AzureHostType.Legacy || HostType == AzureHostType.NotHosted)
        {
            hostTypeOffset = -1;
        }

        if (!HasAPISegment)
        {
            // If there is no API segment, we must assume project is the last
            // segment, provided it is either segment 2 or segment 3.
            // Length of segments includes zero index which is always a '/' if it exists.
            if (Uri!.Segments.Length < (3 + hostTypeOffset) || Uri!.Segments.Length > (4 + hostTypeOffset))
            {
                return string.Empty;
            }

            targetSegment = Uri!.Segments.Length - 1;
        }
        else
        {
            // There is a special situation with Repository URLs where the project name is omitted
            // if the repository is the same name as the project. In this situation the segment
            // immediately preceding the _git segment must be the organization segment.
            if ((APISegmentIndex == (2 + hostTypeOffset))
                && (Uri!.Segments.Length > (APISegmentIndex + 1))
                && APISegment.Equals("_git", StringComparison.OrdinalIgnoreCase))
            {
                // The target segment is the repository name.
                targetSegment = APISegmentIndex + 1;
            }
            else if (targetSegment < (2 + hostTypeOffset) || targetSegment > (3 + hostTypeOffset))
            {
                // Project must be the preceding segment, as long as it is either segment 2
                // or segment 3.
                return string.Empty;
            }
        }

        try
        {
            return Uri.UnescapeDataString(Uri!.Segments[targetSegment].Replace("/", string.Empty));
        }
        catch (Exception e)
        {
            _log.Error(e, $"InitializeProject failed for Uri: {Uri}");
            return string.Empty;
        }
    }

    private bool InitializeIsRepository()
    {
        if (!IsValid)
        {
            return false;
        }

        if (!APISegment.Equals("_git", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        if (Uri.Segments.Length <= APISegmentIndex + 1)
        {
            return false;
        }

        if (string.IsNullOrEmpty(Project))
        {
            return false;
        }

        return true;
    }

    private string InitializeRepository()
    {
        if (!IsRepository)
        {
            return string.Empty;
        }

        try
        {
            var targetSegment = APISegmentIndex + 1;

            // We've verified it is a repository Uri, therefore we know the repository name is the
            // next segment and that such a segment exists.
            return Uri.UnescapeDataString(Uri.Segments[targetSegment].Replace("/", string.Empty));
        }
        catch (Exception e)
        {
            _log.Error(e, $"InitializeRepository failed for Uri: {Uri}");
            return string.Empty;
        }
    }

    private bool InitializeIsQuery()
    {
        if (!IsValid)
        {
            return false;
        }

        if (!APISegment.Equals("_queries", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        if (string.IsNullOrEmpty(Project))
        {
            return false;
        }

        if (Uri.Segments.Length <= APISegmentIndex + 2)
        {
            return false;
        }

        // The query id should be a Guid. If it is not a Guid, it is not a valid query.
        var queryId = Uri.Segments[APISegmentIndex + 2].Replace("/", string.Empty);
        if (!Guid.TryParse(queryId, out _))
        {
            return false;
        }

        var apiSubtype = Uri.Segments[APISegmentIndex + 1].Replace("/", string.Empty);
        if (apiSubtype.Equals("query", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }
        else if (apiSubtype.Equals("query-edit", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        return false;
    }

    private string InitializeQuery()
    {
        if (!IsQuery)
        {
            return string.Empty;
        }

        // We've verified it is a query Uri, therefore we know the queryId is two segments
        // after the underscore segment and that such a segment exists.
        try
        {
            var targetSegment = APISegmentIndex + 2;

            // We've verified it is a repository Uri, therefore we know the repository name is the
            // next segment and that such a segment exists.
            return Uri.Segments[targetSegment].Replace("/", string.Empty);
        }
        catch (Exception e)
        {
            _log.Error(e, $"InitializeQuery failed for Uri: {Uri}");
            return string.Empty;
        }
    }

    private Uri InitializeConnection()
    {
        Uri? newUri = null;
        switch (HostType)
        {
            case AzureHostType.Legacy:

                // Legacy format is just the authority, as the organization is in the subdomain.
                // Note that Authority will not contain the port number unless the port
                // number differs from the default port. So if Port 443 is specified, Authority will
                // not list it as that is the default port for the https scheme.
                var legacyUriString = Uri.Scheme + "://" + Uri.Authority;
                legacyUriString = legacyUriString.TrimEnd('/') + '/';
                if (!Uri.TryCreate(legacyUriString, UriKind.Absolute, out newUri))
                {
                    _log.Error($"Failed creating legacy Uri: {Uri}   UriString: {legacyUriString}");
                }

                break;

            case AzureHostType.Modern:

                // Modern format is authority + first segment, which is the organization.
                var modernUriString = Uri.Scheme + "://" + Uri.Authority + Uri.Segments[0] + Uri.Segments[1];
                modernUriString = modernUriString.TrimEnd('/') + '/';
                if (!Uri.TryCreate(modernUriString, UriKind.Absolute, out newUri))
                {
                    _log.Error($"Failed creating modern Uri: {Uri}   UriString: {modernUriString}");
                }

                break;

            case AzureHostType.NotHosted:
                // OnPrem includes the collection.
                var onpremUriString = Uri.Scheme + "://" + Uri.Authority;
                onpremUriString = onpremUriString.TrimEnd('/') + '/';
                if (!Uri.TryCreate(onpremUriString, UriKind.Absolute, out newUri))
                {
                    _log.Error($"Failed creating On-Prem Uri: {Uri}   UriString: {onpremUriString}");
                }

                break;

            default:
                break;
        }

        // Always return a valid Uri object.
        // Callers should verify it is valid using IsValid property.
        if (newUri is null)
        {
            return new(ValidUriString);
        }
        else
        {
            return newUri;
        }
    }

    private Uri InitializeOrganizationLink()
    {
        Uri? orgUri = null;
        switch (HostType)
        {
            case AzureHostType.Legacy:
                // https://organization.visualstudio.com/DefaultCollection/project/_git/repository <- from clone window
                // https://organization.visualstudio.com/project/_git/repository <- from repo url window
                // https://organization.visualstudio.com/_git/project from browser when clicking on "Repos" when the repo name is the same as the project name
                var legacyOrgUri = Uri.Scheme + "://" + Uri.Host;
                if (!Uri.TryCreate(legacyOrgUri, UriKind.Absolute, out orgUri))
                {
                    _log.Error("Could not make Org Uri");
                }

                break;

            case AzureHostType.Modern:
                // https://organization@dev.azure.com/organization/project/_git/repository from clone window
                // https://dev.azure.com/organization/project/_git/repository from repo url window
                // https://dev.azure.com/organization/project from browser when clicking on "Repos" when the repo name is the same as the project name
                var modernOrgUri = Uri.Scheme + "://" + Uri.Host + "/" + Organization;
                if (!Uri.TryCreate(modernOrgUri, UriKind.Absolute, out orgUri))
                {
                    _log.Error("Could not make Org Uri");
                }

                break;

            case AzureHostType.NotHosted:
                var onpremOrgUri = Uri.Scheme + "://" + Uri.Host;
                if (!Uri.TryCreate(onpremOrgUri, UriKind.Absolute, out orgUri))
                {
                    _log.Error("Could not make Org Uri");
                }

                break;

            default:
                break;
        }

        // Always return a valid Uri object.
        // Callers should verify it is valid using IsValid property.
        if (orgUri is null)
        {
            return new(ValidUriString);
        }
        else
        {
            return orgUri;
        }
    }
}
