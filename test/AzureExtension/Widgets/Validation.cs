// Copyright (c) Microsoft Corporation and Contributors
// Licensed under the MIT license.
using DevHome.Logging;
using DevHomeAzureExtension.Client;

namespace DevHomeAzureExtension.Test;

public partial class WidgetTests
{
    public enum AzureUriType
    {
        WorkItem,
        Query,
        Repository,
        RepositoryNoProject,
        NamesWithSpaces,
        Unknown,
        SignIn,
        Garbage,        // Anything that is expected to fail creation by a normal Uri.
    }

    [TestMethod]
    [TestCategory("Unit")]
    public void AzureUriValidation()
    {
        using var log = new Logger("TestLog", TestOptions.LogOptions);
        var testListener = new TestListener("TestListener", TestContext!);
        log.AddListener(testListener);
        Log.Attach(log);

        var testUrisValid = new List<Tuple<string, AzureUriType, bool>>
        {
            // Test Uris use legacy and new format, with and without collection, and
            // with and without trailing slash.

            // Stub link, just the Project
            Tuple.Create("https://dev.azure.com/organization/project", AzureUriType.Unknown, false),
            Tuple.Create("https://dev.azure.com/organization/project/", AzureUriType.Unknown, false),
            Tuple.Create("https://dev.azure.com/organization/collection/project", AzureUriType.Unknown, false),
            Tuple.Create("https://dev.azure.com/organization/collection/project/", AzureUriType.Unknown, false),
            Tuple.Create("https://organization.visualstudio.com/project", AzureUriType.Unknown, true),
            Tuple.Create("https://organization.visualstudio.com/project/", AzureUriType.Unknown, true),
            Tuple.Create("https://organization.visualstudio.com/collection/project", AzureUriType.Unknown, true),
            Tuple.Create("https://organization.visualstudio.com/collection/project/", AzureUriType.Unknown, true),

            // Work item link both current and legacy, which should be valid for all validity tests.
            Tuple.Create("https://dev.azure.com/organization/project/_workitems/edit/1234567", AzureUriType.WorkItem, false),
            Tuple.Create("https://dev.azure.com/organization/project/_workitems/edit/1234567/", AzureUriType.WorkItem, false),
            Tuple.Create("https://dev.azure.com/organization/collection/project/_workitems/edit/1234567", AzureUriType.WorkItem, false),
            Tuple.Create("https://dev.azure.com/organization/collection/project/_workitems/edit/1234567/", AzureUriType.WorkItem, false),
            Tuple.Create("https://organization.visualstudio.com/project/_workitems/edit/1234567", AzureUriType.WorkItem, true),
            Tuple.Create("https://organization.visualstudio.com/project/_workitems/edit/1234567/", AzureUriType.WorkItem, true),
            Tuple.Create("https://organization.visualstudio.com/collection/project/_workitems/edit/1234567", AzureUriType.WorkItem, true),
            Tuple.Create("https://organization.visualstudio.com/collection/project/_workitems/edit/1234567/", AzureUriType.WorkItem, true),

            // Queries
            Tuple.Create("https://dev.azure.com/organization/project/_queries/query/12345678-1234-1234-1234-1234567890ab", AzureUriType.Query, false),
            Tuple.Create("https://dev.azure.com/organization/project/_queries/query/12345678-1234-1234-1234-1234567890ab/", AzureUriType.Query, false),
            Tuple.Create("https://dev.azure.com/organization/collection/project/_queries/query/12345678-1234-1234-1234-1234567890ab", AzureUriType.Query, false),
            Tuple.Create("https://dev.azure.com/organization/collection/project/_queries/query/12345678-1234-1234-1234-1234567890ab/", AzureUriType.Query, false),
            Tuple.Create("https://organization.visualstudio.com/project/_queries/query/12345678-1234-1234-1234-1234567890ab", AzureUriType.Query, true),
            Tuple.Create("https://organization.visualstudio.com/project/_queries/query/12345678-1234-1234-1234-1234567890ab/", AzureUriType.Query, true),
            Tuple.Create("https://organization.visualstudio.com/collection/project/_queries/query/12345678-1234-1234-1234-1234567890ab", AzureUriType.Query, true),
            Tuple.Create("https://organization.visualstudio.com/collection/project/_queries/query/12345678-1234-1234-1234-1234567890ab/", AzureUriType.Query, true),

            Tuple.Create("https://dev.azure.com/organization/project/_queries/query-edit/12345678-1234-1234-1234-1234567890ab", AzureUriType.Query, false),
            Tuple.Create("https://dev.azure.com/organization/project/_queries/query-edit/12345678-1234-1234-1234-1234567890ab/", AzureUriType.Query, false),
            Tuple.Create("https://dev.azure.com/organization/collection/project/_queries/query-edit/12345678-1234-1234-1234-1234567890ab", AzureUriType.Query, false),
            Tuple.Create("https://dev.azure.com/organization/collection/project/_queries/query-edit/12345678-1234-1234-1234-1234567890ab/", AzureUriType.Query, false),
            Tuple.Create("https://organization.visualstudio.com/project/_queries/query-edit/12345678-1234-1234-1234-1234567890ab", AzureUriType.Query, true),
            Tuple.Create("https://organization.visualstudio.com/project/_queries/query-edit/12345678-1234-1234-1234-1234567890ab/", AzureUriType.Query, true),
            Tuple.Create("https://organization.visualstudio.com/collection/project/_queries/query-edit/12345678-1234-1234-1234-1234567890ab", AzureUriType.Query, true),
            Tuple.Create("https://organization.visualstudio.com/collection/project/_queries/query-edit/12345678-1234-1234-1234-1234567890ab/", AzureUriType.Query, true),

            // Queries that are malformed, which should be treated as non-queries but otherwise be treated as valid as a non-query.
            Tuple.Create("https://dev.azure.com/organization/project/_queries/query", AzureUriType.WorkItem, false),
            Tuple.Create("https://dev.azure.com/organization/project/_queries/query/", AzureUriType.WorkItem, false),
            Tuple.Create("https://dev.azure.com/organization/collection/project/_queries/query", AzureUriType.WorkItem, false),
            Tuple.Create("https://dev.azure.com/organization/collection/project/_queries/query/", AzureUriType.WorkItem, false),
            Tuple.Create("https://organization.visualstudio.com/project/_queries/query", AzureUriType.WorkItem, true),
            Tuple.Create("https://organization.visualstudio.com/project/_queries/query/", AzureUriType.WorkItem, true),
            Tuple.Create("https://organization.visualstudio.com/collection/project/_queries/query", AzureUriType.WorkItem, true),
            Tuple.Create("https://organization.visualstudio.com/collection/project/_queries/query/", AzureUriType.WorkItem, true),

            // Repositories
            Tuple.Create("https://dev.azure.com/organization/project/_git/repository", AzureUriType.Repository, false),
            Tuple.Create("https://dev.azure.com/organization/project/_git/repository/", AzureUriType.Repository, false),
            Tuple.Create("https://dev.azure.com/organization/project/_git/repository/some/other/stuff", AzureUriType.Repository, false),
            Tuple.Create("https://dev.azure.com/organization/collection/project/_git/repository", AzureUriType.Repository, false),
            Tuple.Create("https://dev.azure.com/organization/collection/project/_git/repository/", AzureUriType.Repository, false),
            Tuple.Create("https://dev.azure.com/organization/collection/project/_git/repository/some/other/stuff", AzureUriType.Repository, false),
            Tuple.Create("https://organization.visualstudio.com/project/_git/repository", AzureUriType.Repository, true),
            Tuple.Create("https://organization.visualstudio.com/project/_git/repository/", AzureUriType.Repository, true),
            Tuple.Create("https://organization.visualstudio.com/project/_git/repository/some/other/stuff", AzureUriType.Repository, true),
            Tuple.Create("https://organization.visualstudio.com/collection/project/_git/repository", AzureUriType.Repository, true),
            Tuple.Create("https://organization.visualstudio.com/collection/project/_git/repository/", AzureUriType.Repository, true),
            Tuple.Create("https://organization.visualstudio.com/collection/project/_git/repository/some/other/stuff", AzureUriType.Repository, true),

            // Names with spaces
            Tuple.Create("https://dev.azure.com/organization/project%20with%20spaces/_git/repository%20with%20spaces", AzureUriType.NamesWithSpaces, false),
            Tuple.Create("https://dev.azure.com/organization/project%20with%20spaces/_git/repository%20with%20spaces/", AzureUriType.NamesWithSpaces, false),
            Tuple.Create("https://dev.azure.com/organization/project%20with%20spaces/_git/repository%20with%20spaces/some/other/stuff", AzureUriType.NamesWithSpaces, false),
            Tuple.Create("https://dev.azure.com/organization/collection/project%20with%20spaces/_git/repository%20with%20spaces", AzureUriType.NamesWithSpaces, false),
            Tuple.Create("https://dev.azure.com/organization/collection/project%20with%20spaces/_git/repository%20with%20spaces/", AzureUriType.NamesWithSpaces, false),
            Tuple.Create("https://dev.azure.com/organization/collection/project%20with%20spaces/_git/repository%20with%20spaces/some/other/stuff", AzureUriType.NamesWithSpaces, false),
            Tuple.Create("https://organization.visualstudio.com/project%20with%20spaces/_git/repository%20with%20spaces", AzureUriType.NamesWithSpaces, true),
            Tuple.Create("https://organization.visualstudio.com/project%20with%20spaces/_git/repository%20with%20spaces/", AzureUriType.NamesWithSpaces, true),
            Tuple.Create("https://organization.visualstudio.com/project%20with%20spaces/_git/repository%20with%20spaces/some/other/stuff", AzureUriType.NamesWithSpaces, true),
            Tuple.Create("https://organization.visualstudio.com/collection/project%20with%20spaces/_git/repository%20with%20spaces", AzureUriType.NamesWithSpaces, true),
            Tuple.Create("https://organization.visualstudio.com/collection/project%20with%20spaces/_git/repository%20with%20spaces/", AzureUriType.NamesWithSpaces, true),
            Tuple.Create("https://organization.visualstudio.com/collection/project%20with%20spaces/_git/repository%20with%20spaces/some/other/stuff", AzureUriType.NamesWithSpaces, true),

            // Repositories where repository name = project name.
            Tuple.Create("https://dev.azure.com/organization/_git/repository", AzureUriType.RepositoryNoProject, false),
            Tuple.Create("https://dev.azure.com/organization/_git/repository/", AzureUriType.RepositoryNoProject, false),
            Tuple.Create("https://dev.azure.com/organization/_git/repository/some/other/stuff", AzureUriType.RepositoryNoProject, false),
            Tuple.Create("https://organization.visualstudio.com/_git/repository", AzureUriType.RepositoryNoProject, true),
            Tuple.Create("https://organization.visualstudio.com/_git/repository/", AzureUriType.RepositoryNoProject, true),
            Tuple.Create("https://organization.visualstudio.com/_git/repository/some/other/stuff", AzureUriType.RepositoryNoProject, true),

            // Azure DevOps services SignIn Uris
            Tuple.Create("https://app.vssps.visualstudio.com/", AzureUriType.SignIn, true),
            Tuple.Create("https://app.vssps.visualstudio.com/with/extra/stuff", AzureUriType.SignIn, true),
            Tuple.Create("https://app.vssps.visualstudio.com:443/", AzureUriType.SignIn, true),
            Tuple.Create("https://app.vssps.visualstudio.com:443/with/extra/stuff", AzureUriType.SignIn, true),
            Tuple.Create("https://organization.vssps.visualstudio.com/", AzureUriType.SignIn, true),
            Tuple.Create("https://organization.vssps.visualstudio.com/with/extra/stuff", AzureUriType.SignIn, true),
            Tuple.Create("https://organization.vssps.visualstudio.com:443/", AzureUriType.SignIn, true),
            Tuple.Create("https://organization.vssps.visualstudio.com:443/with/extra/stuff", AzureUriType.SignIn, true),

            // Azure DevOps services SignIn Uris, modern format.
            // Note port 443 is the default port for https, so Uri objects remove it.
            Tuple.Create("https://vssps.dev.azure.com/organization", AzureUriType.SignIn, false),
            Tuple.Create("https://vssps.dev.azure.com/organization/", AzureUriType.SignIn, false),
            Tuple.Create("https://vssps.dev.azure.com:443/organization", AzureUriType.SignIn, false),
            Tuple.Create("https://vssps.dev.azure.com:443/organization/", AzureUriType.SignIn, false),
            Tuple.Create("https://vssps.dev.azure.com/organization/with/extra/stuff", AzureUriType.SignIn, false),
            Tuple.Create("https://vssps.dev.azure.com/organization/with/extra/stuff/", AzureUriType.SignIn, false),
            Tuple.Create("https://vssps.dev.azure.com:443/organization/with/extra/stuff", AzureUriType.SignIn, false),
            Tuple.Create("https://vssps.dev.azure.com:443/organization/with/extra/stuff/", AzureUriType.SignIn, false),

            // Azure Devops SignIn Uris, legacy format.
            // Note port 443 is the default port for https, so Uri objects remove it.
            Tuple.Create("https://app.vssps.visualstudio.com/", AzureUriType.SignIn, false),
            Tuple.Create("https://app.vssps.visualstudio.com:443/", AzureUriType.SignIn, false),
            Tuple.Create("https://app.vssps.visualstudio.com:443/with/extra/stuff", AzureUriType.SignIn, false),
            Tuple.Create("https://organization.vssps.visualstudio.com/", AzureUriType.SignIn, false),
            Tuple.Create("https://organization.vssps.visualstudio.com/with/extra/stuff", AzureUriType.SignIn, false),
            Tuple.Create("https://organization.vssps.visualstudio.com:443/", AzureUriType.SignIn, false),
            Tuple.Create("https://organization.vssps.visualstudio.com:443/with/extra/stuff", AzureUriType.SignIn, false),
            Tuple.Create("https://app.vssps.visualstudio.com:443/", AzureUriType.SignIn, false),
            Tuple.Create("https://app.vssps.visualstudio.com:443/with/extra/stuff", AzureUriType.SignIn, false),
            Tuple.Create("https://organization.vssps.visualstudio.com/", AzureUriType.SignIn, false),
            Tuple.Create("https://organization.vssps.visualstudio.com/with/extra/stuff", AzureUriType.SignIn, false),
            Tuple.Create("https://organization.vssps.visualstudio.com:443/", AzureUriType.SignIn, false),
            Tuple.Create("https://organization.vssps.visualstudio.com:443/with/extra/stuff", AzureUriType.SignIn, false),
        };

        var testUrisInvalid = new List<Tuple<string, AzureUriType, bool>>
        {
            // Definitely bogus
            Tuple.Create("https://www.microsoft.com", AzureUriType.Unknown, false),
            Tuple.Create("foobar", AzureUriType.Garbage, false),
            Tuple.Create("DELETE * IN Repository", AzureUriType.Garbage, false),
            Tuple.Create("https://github.com/owner/repo/pulls?q=is%3Aopen+mentions%3A%40me", AzureUriType.Unknown, false),
            Tuple.Create(string.Empty, AzureUriType.Garbage, false),
        };

        foreach (var uriTuple in testUrisValid)
        {
            TestContext?.WriteLine($"Uri: {uriTuple.Item1}   Type: {uriTuple.Item2}  IsLegacy: {uriTuple.Item3}");

            // Validate the different inputs are identical regardless of which constructor was used.
            var azureUri = new AzureUri(uriTuple.Item1);

            Uri? uri = null;
            if (uriTuple.Item2 != AzureUriType.Garbage)
            {
                // Type garbage Uris will fail Uri constructor, that's fine for AzureUris, but we
                // cannot validate Uri parity with something that fails the Uri constructor.
                uri = new Uri(uriTuple.Item1);
                Assert.AreEqual(azureUri.ToString(), uri.ToString());
                Assert.AreEqual(azureUri.OriginalString, uri.OriginalString);
                Assert.AreEqual(azureUri.Uri, uri);
            }

            // All Valid inputs should be valid.
            Assert.IsTrue(azureUri.IsValid);

            // We only support hosted for now.
            Assert.IsTrue(azureUri.IsHosted);
            TestContext?.WriteLine($"Org: {azureUri.Organization}  Project: {azureUri.Project}  Repository: {azureUri.Repository}  Query: {azureUri.Query}");

            if (uriTuple.Item2 != AzureUriType.SignIn && uriTuple.Item2 != AzureUriType.RepositoryNoProject && uriTuple.Item2 != AzureUriType.NamesWithSpaces)
            {
                // Signin Uris may not have project.
                // Some repository URIs will have a different project.
                // Signin Uris can also have "app" as the organization.
                // Organization will be validated as part of that type.
                Assert.AreEqual("organization", azureUri.Organization);
                Assert.AreEqual("project", azureUri.Project);
            }

            if (uriTuple.Item2 == AzureUriType.RepositoryNoProject)
            {
                // No project Uris have the repository name as the project.
                Assert.AreEqual("organization", azureUri.Organization);
                Assert.AreEqual("repository", azureUri.Project);
            }

            if (uriTuple.Item2 == AzureUriType.NamesWithSpaces)
            {
                // Names with spaces have actual spaces in the names.
                Assert.AreEqual("organization", azureUri.Organization);
                Assert.AreEqual("project with spaces", azureUri.Project);
            }

            // Validate the constructor is identical if it is created from a Uri instead of a string.
            if (uriTuple.Item2 != AzureUriType.Garbage)
            {
                uri = new Uri(uriTuple.Item1);
                azureUri = new AzureUri(uri);
                Assert.AreEqual(azureUri.ToString(), uri.ToString());
                Assert.AreEqual(azureUri.OriginalString, uri.OriginalString);
                Assert.AreEqual(azureUri.Uri, uri);

                var connectionUri = azureUri.Connection;
                Assert.IsNotNull(connectionUri);
                TestContext?.WriteLine($"Connection: {connectionUri}");

                // All Https Uris have port 443 by default.
                // We require https uri, therefore we must have port 443.
                Assert.IsTrue(connectionUri.Port == 443);

                // A properly constructed connectionUri will be contained within the full Uri.
                // We use a separate standard Uri to compare so we get expected Uri behavior w/r/t
                // the Authority, which will not contain the port if it is the scheme's default.

                // The only modification is to enforce a trailing / which may or may not be there,
                // but the trailing slash is part of the Authority and we normalize to that.
                // We are testing for functional equivalence.
                var normalizedOriginal = uri.ToString().Trim('/') + '/';
                Assert.IsTrue(normalizedOriginal.StartsWith(connectionUri.ToString(), StringComparison.OrdinalIgnoreCase));
            }

            // Verify Query Uris have expected values.
            if (uriTuple.Item2 == AzureUriType.Query)
            {
                Assert.IsTrue(azureUri.IsQuery);
                Assert.AreEqual("12345678-1234-1234-1234-1234567890ab", azureUri.Query);
            }
            else
            {
                Assert.IsFalse(azureUri.IsQuery);
                Assert.AreEqual(string.Empty, azureUri.Query);
            }

            // Verify repository Uris have expected values.
            if (uriTuple.Item2 == AzureUriType.Repository || uriTuple.Item2 == AzureUriType.RepositoryNoProject)
            {
                Assert.IsTrue(azureUri.IsRepository);
                Assert.AreEqual("repository", azureUri.Repository);
            }
            else if (uriTuple.Item2 == AzureUriType.NamesWithSpaces)
            {
                Assert.IsTrue(azureUri.IsRepository);
                Assert.AreEqual("repository with spaces", azureUri.Repository);
            }
            else
            {
                Assert.IsFalse(azureUri.IsRepository);
                Assert.AreEqual(string.Empty, azureUri.Repository);
            }

            if (uriTuple.Item2 == AzureUriType.SignIn)
            {
                if (azureUri.Organization.Equals("app", StringComparison.OrdinalIgnoreCase))
                {
                    // App is valid if it is parsed as an organization, but only
                    // on legacy Uris. This directly validates that
                    // app.vssps.visualstudio.com is a valid input uri.
                    Assert.IsTrue(azureUri.HostType == AzureHostType.Legacy);
                }
                else
                {
                    // All other matches are assumed to be organization names.
                    // This ensures we do not treat the "vssps" part as being part of the org name.
                    Assert.AreEqual("organization", azureUri.Organization);
                }

                // Test for equivalence of Connection Uris when the base Uri and the Connection
                // should be equivalent.
                if (azureUri.HostType == AzureHostType.Modern)
                {
                    // Modern hosts have bare minimum Org requirement.
                    if (azureUri.Uri.Segments.Length == 2)
                    {
                        // Exact equivalence may not be true depending on trailing slash in the
                        // original Uri, but these are still functionally equivalent.
                        var normalizedOriginal = azureUri.Uri.ToString().Trim('/') + '/';
                        Assert.IsTrue(normalizedOriginal.Equals(azureUri.Connection.ToString(), StringComparison.OrdinalIgnoreCase));
                    }
                }
                else
                {
                    if (azureUri.Uri.Segments.Length < 2)
                    {
                        var normalizedOriginal = azureUri.Uri.ToString().Trim('/') + '/';
                        Assert.IsTrue(normalizedOriginal.Equals(azureUri.Connection.ToString(), StringComparison.OrdinalIgnoreCase));
                    }
                }
            }

            TestContext?.WriteLine($"Valid: {uriTuple.Item1}");
        }

        // Verify bad strings are recognized as bad.
        foreach (var uriTuple in testUrisInvalid)
        {
            TestContext?.WriteLine($"Uri: {uriTuple.Item1}   Type: {uriTuple.Item2}  IsLegacy: {uriTuple.Item3}");
            var azureUri = new AzureUri(uriTuple.Item1);
            Assert.IsFalse(azureUri.IsValid);
            Assert.IsFalse(azureUri.IsHosted);
            Assert.AreEqual(uriTuple.Item1, azureUri.OriginalString);
            Assert.AreEqual(string.Empty, azureUri.Organization);
            Assert.AreEqual(string.Empty, azureUri.Project);
            Assert.IsFalse(azureUri.IsRepository);
            Assert.AreEqual(string.Empty, azureUri.Repository);
            Assert.IsFalse(azureUri.IsQuery);
            Assert.AreEqual(string.Empty, azureUri.Query);
            TestContext?.WriteLine($"Invalid: {uriTuple.Item1}");

            if (uriTuple.Item2 != AzureUriType.Garbage)
            {
                // Verify a Uri object created from the same original input has matching ToString()
                // and matching OriginalString values, and the Uris therein are identical.
                var uri = new Uri(uriTuple.Item1);
                Assert.AreEqual(azureUri.ToString(), uri.ToString());
                Assert.AreEqual(azureUri.OriginalString, uri.OriginalString);
                Assert.AreEqual(azureUri.Uri, uri);

                // Create the AzureUri from the Uri, and ensure everything is still the same,
                // including hte ToString() and OriginalString values.
                azureUri = new AzureUri(uri);
                Assert.AreEqual(azureUri.ToString(), uri.ToString());
                Assert.AreEqual(azureUri.OriginalString, uri.OriginalString);
                Assert.AreEqual(azureUri.Uri, uri);

                // Re-validate all of the properties are the same using this constructor.
                Assert.IsFalse(azureUri.IsValid);
                Assert.IsFalse(azureUri.IsHosted);
                Assert.AreEqual(uriTuple.Item1, azureUri.OriginalString);
                Assert.AreEqual(string.Empty, azureUri.Organization);
                Assert.AreEqual(string.Empty, azureUri.Project);
                Assert.IsFalse(azureUri.IsRepository);
                Assert.AreEqual(string.Empty, azureUri.Repository);
                Assert.IsFalse(azureUri.IsQuery);
                Assert.AreEqual(string.Empty, azureUri.Query);
            }
        }

        // Null string input validation
        string? nullstr = null;
        var nullTest = new AzureUri(nullstr);
        Assert.IsNotNull(nullTest);
        Assert.AreEqual(string.Empty, nullTest.ToString());
        Assert.IsFalse(nullTest.IsValid);

        // Empty string input validation
        nullTest = new AzureUri(string.Empty);
        Assert.IsNotNull(nullTest);
        Assert.AreEqual(string.Empty, nullTest.ToString());
        Assert.IsFalse(nullTest.IsValid);

        // Null Uri input validation
        Uri? nulluri = null;
        nullTest = new AzureUri(nulluri);
        Assert.IsNotNull(nullTest);
        Assert.AreEqual(string.Empty, nullTest.ToString());
        Assert.IsFalse(nullTest.IsValid);

        // No-parameter constructor validation.
        var emptyAzureUri = new AzureUri();
        Assert.IsNotNull(emptyAzureUri);
        Assert.AreEqual(string.Empty, emptyAzureUri.ToString());
        Assert.IsFalse(emptyAzureUri.IsValid);
        Assert.IsFalse(emptyAzureUri.IsHosted);
        Assert.AreEqual(string.Empty, emptyAzureUri.Organization);
        Assert.AreEqual(string.Empty, emptyAzureUri.Project);
        Assert.IsFalse(emptyAzureUri.IsRepository);
        Assert.AreEqual(string.Empty, emptyAzureUri.Repository);
        Assert.IsFalse(emptyAzureUri.IsQuery);
        Assert.AreEqual(string.Empty, emptyAzureUri.Query);
        Assert.AreEqual(AzureHostType.NotHosted, emptyAzureUri.HostType);

        testListener.PrintEventCounts();
        Assert.AreEqual(false, testListener.FoundErrors());
    }
}
