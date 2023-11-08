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
        Unknown,
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

            Assert.IsTrue(azureUri.IsHosted);
            var org = azureUri.Organization;
            var project = azureUri.Project;
            var repository = azureUri.Repository;
            var query = azureUri.Query;
            TestContext?.WriteLine($"Org: {org}  Project: {project}  Repository: {repository}  Query: {query}");
            Assert.AreEqual("organization", org);
            Assert.AreEqual("project", project);

            // Validate the constructor is identical if it is created from a Uri instead of a string.
            if (uriTuple.Item2 != AzureUriType.Garbage)
            {
                uri = new Uri(uriTuple.Item1);
                azureUri = new AzureUri(uri);
                Assert.AreEqual(azureUri.ToString(), uri.ToString());
                Assert.AreEqual(azureUri.OriginalString, uri.OriginalString);
                Assert.AreEqual(azureUri.Uri, uri);
            }

            // Verify Query Uris have expected values.
            if (uriTuple.Item2 == AzureUriType.Query)
            {
                Assert.IsTrue(azureUri.IsQuery);
                Assert.AreEqual("12345678-1234-1234-1234-1234567890ab", query);
            }
            else
            {
                Assert.IsFalse(azureUri.IsQuery);
                Assert.AreEqual(string.Empty, query);
            }

            // Verify repository Uris have expected values.
            if (uriTuple.Item2 == AzureUriType.Repository)
            {
                Assert.IsTrue(azureUri.IsRepository);
                Assert.AreEqual("repository", repository);
            }
            else
            {
                Assert.IsFalse(azureUri.IsRepository);
                Assert.AreEqual(string.Empty, repository);
            }

            var connectionUri = azureUri.Connection;
            Assert.IsNotNull(connectionUri);
            TestContext?.WriteLine($"Connection: {connectionUri}");

            // Verify connection URI remains in the original format. Legacy URIs should use legacy connections.
            if (azureUri.HostType == AzureHostType.Legacy)
            {
                Assert.AreEqual("https://organization.visualstudio.com/", connectionUri.ToString());
            }
            else
            {
                Assert.AreEqual("https://dev.azure.com/organization/", connectionUri.ToString());
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
