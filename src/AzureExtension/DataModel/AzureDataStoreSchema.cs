// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace DevHomeAzureExtension.DataModel;

public class AzureDataStoreSchema : IDataStoreSchema
{
    public long SchemaVersion => SchemaVersionValue;

    public List<string> SchemaSqls => _schemaSqlsValue;

    public AzureDataStoreSchema()
    {
    }

    // Update this anytime incompatible changes happen with a released version.
    private const long SchemaVersionValue = 0x0007;

    private const string Metadata =
    @"CREATE TABLE Metadata (" +
        "Id INTEGER PRIMARY KEY NOT NULL," +
        "Key TEXT NOT NULL COLLATE NOCASE," +
        "Value TEXT NOT NULL" +
    ");" +
    "CREATE UNIQUE INDEX IDX_Metadata_Key ON Metadata (Key);";

    private const string Identity =
    @"CREATE TABLE Identity (" +
        "Id INTEGER PRIMARY KEY NOT NULL," +
        "Name TEXT NOT NULL COLLATE NOCASE," +
        "InternalId TEXT NOT NULL," +
        "Avatar TEXT NOT NULL COLLATE NOCASE," +
        "DeveloperLoginId TEXT," +
        "TimeUpdated INTEGER NOT NULL" +
    ");" +

    // InternalId is a Guid from the server, so by definition is unique.
    "CREATE UNIQUE INDEX IDX_Identity_InternalId ON Identity (InternalId);";

    private const string Project =
    @"CREATE TABLE Project (" +
        "Id INTEGER PRIMARY KEY NOT NULL," +
        "Name TEXT NOT NULL COLLATE NOCASE," +
        "InternalId TEXT NOT NULL," +
        "Description TEXT NOT NULL," +
        "OrganizationId INTEGER NOT NULL," +
        "TimeUpdated INTEGER NOT NULL" +
    ");" +

    // Project ID is a Guid, so by definition is unique.
    // Project Name can be renamed and reused per DevOps documentation, so it is not safe.
    "CREATE UNIQUE INDEX IDX_Project_InternalId ON Project (InternalId);";

    private const string ProjectReference =
    @"CREATE TABLE ProjectReference (" +
        "Id INTEGER PRIMARY KEY NOT NULL," +
        "ProjectId INTEGER NOT NULL," +
        "DeveloperId INTEGER NOT NULL," +
        "PullRequestCount INTEGER NOT NULL" +
    ");" +

    // Project references are unique by DeveloperId and ProjectId
    "CREATE UNIQUE INDEX IDX_ProjectReference_ProjectIdDeveloperId ON ProjectReference (ProjectId, DeveloperId);";

    private const string Organization =
    @"CREATE TABLE Organization (" +
        "Id INTEGER PRIMARY KEY NOT NULL," +
        "Name TEXT NOT NULL COLLATE NOCASE," +
        "Connection TEXT NOT NULL COLLATE NOCASE," +
        "TimeUpdated INTEGER NOT NULL," +
        "TimeLastSync INTEGER NOT NULL" +
    ");" +

    // Connections should be unique per organization. We do not appear to have
    // better information about the organization and connection at this time, so
    // we will use Connection as the unique constraint. It is also used in the
    // constructor, so while the same organization may have multiple connections,
    // each connection should correspond to only one organization.
    "CREATE UNIQUE INDEX IDX_Organization_Connection ON Organization (Connection);";

    private const string Repository =
    @"CREATE TABLE Repository (" +
        "Id INTEGER PRIMARY KEY NOT NULL," +
        "Name TEXT NOT NULL COLLATE NOCASE," +
        "InternalId TEXT NOT NULL," +
        "ProjectId INTEGER NOT NULL," +
        "CloneUrl TEXT NOT NULL COLLATE NOCASE," +
        "IsPrivate INTEGER NOT NULL," +
        "TimeUpdated INTEGER NOT NULL" +
    ");" +

    // While Name and ProjectId should be unique, it is possible renaming occurs and
    // we might have a collision if we have cached a repository prior to rename and
    // then encounter a different repository with that name. Therefore we will not
    // create a unique index on ProjectId and Name.
    // Repository InternalId is a Guid, so by definition is unique.
    "CREATE UNIQUE INDEX IDX_Repository_InternalId ON Repository (InternalId);";

    private const string RepositoryReference =
    @"CREATE TABLE RepositoryReference (" +
        "Id INTEGER PRIMARY KEY NOT NULL," +
        "RepositoryId INTEGER NOT NULL," +
        "DeveloperId INTEGER NOT NULL," +
        "PullRequestCount INTEGER NOT NULL" +
    ");" +

    // Repository references are unique by DeveloperId and ProjectId
    "CREATE UNIQUE INDEX IDX_RepositoryReference_RepositoryIdDeveloperId ON RepositoryReference (RepositoryId, DeveloperId);";

    private const string Query =
    @"CREATE TABLE Query (" +
        "Id INTEGER PRIMARY KEY NOT NULL," +
        "QueryId TEXT NOT NULL COLLATE NOCASE," +
        "DisplayName TEXT NOT NULL," +
        "DeveloperLogin TEXT NOT NULL COLLATE NOCASE," +
        "QueryResults TEXT NOT NULL," +
        "QueryResultCount INTEGER NOT NULL," +
        "ProjectId INTEGER NOT NULL," +
        "TimeUpdated INTEGER NOT NULL" +
    ");" +

    // Queries can differ in their results by developerId, so while QueryId is
    // a guid, the result information could depend on which developer is making
    // the query. Therefore we make unique constraint on QueryId AND DeveloperLogin.
    "CREATE UNIQUE INDEX IDX_Query_QueryIdDeveloperLogin ON Query (QueryId, DeveloperLogin);";

    private const string WorkItemType =
    @"CREATE TABLE WorkItemType (" +
        "Id INTEGER PRIMARY KEY NOT NULL," +
        "Name TEXT NOT NULL COLLATE NOCASE," +
        "Icon TEXT NOT NULL," +
        "Color TEXT NOT NULL," +
        "Description TEXT NOT NULL," +
        "ProjectId INTEGER NOT NULL," +
        "TimeUpdated INTEGER NOT NULL" +
    ");" +

    // WorkItemType is looked up on the server by Name and Project name/guid, so a unique
    // constraint on Name and ProjectId is consistent and safe.
    "CREATE UNIQUE INDEX IDX_WorkItemType_NameProjectId ON WorkItemType (Name, ProjectId);";

    private const string PullRequests =
    @"CREATE TABLE PullRequests (" +
        "Id INTEGER PRIMARY KEY NOT NULL," +
        "RepositoryId INTEGER NOT NULL," +
        "DeveloperLogin TEXT NOT NULL COLLATE NOCASE," +
        "Results TEXT NOT NULL," +
        "ProjectId INTEGER NOT NULL," +
        "ViewId INTEGER NOT NULL," +
        "TimeUpdated INTEGER NOT NULL" +
    ");" +

    // Developer Pull requests are unique on Org / Project / Repository and
    // the developer login, and the view.
    "CREATE UNIQUE INDEX IDX_PullRequests_ProjectIdRepositoryIdDeveloperLoginViewId ON PullRequests (ProjectId, RepositoryId, DeveloperLogin, ViewId);";

    // PullRequsetPolicyStatus is a snapshot of a developer's Pull Requests.
    private const string PullRequestPolicyStatus =
    @"CREATE TABLE PullRequestPolicyStatus (" +
        "Id INTEGER PRIMARY KEY NOT NULL," +
        "ArtifactId TEXT NULL COLLATE NOCASE," +
        "ProjectId INTEGER NOT NULL," +
        "RepositoryId INTEGER NOT NULL," +
        "PullRequestId INTEGER NOT NULL," +
        "Title TEXT NULL COLLATE NOCASE," +
        "PolicyStatusId INTEGER NOT NULL," +
        "PolicyStatusReason TEXT NULL COLLATE NOCASE," +
        "PullRequestStatusId INTEGER NOT NULL," +
        "TargetBranchName TEXT NULL COLLATE NOCASE," +
        "HtmlUrl TEXT NULL COLLATE NOCASE," +
        "TimeUpdated INTEGER NOT NULL," +
        "TimeCreated INTEGER NOT NULL" +
    ");";

    private const string Notification =
    @"CREATE TABLE Notification (" +
        "Id INTEGER PRIMARY KEY NOT NULL," +
        "TypeId INTEGER NOT NULL," +
        "ProjectId INTEGER NOT NULL," +
        "RepositoryId INTEGER NOT NULL," +
        "Title TEXT NOT NULL COLLATE NOCASE," +
        "Description TEXT NOT NULL COLLATE NOCASE," +
        "Identifier TEXT NULL COLLATE NOCASE," +
        "Result TEXT NULL COLLATE NOCASE," +
        "HtmlUrl TEXT NULL COLLATE NOCASE," +
        "ToastState INTEGER NOT NULL," +
        "TimeCreated INTEGER NOT NULL" +
    ");";

    // All Sqls together.
    private static readonly List<string> _schemaSqlsValue =
    [
        Metadata,
        Identity,
        Project,
        ProjectReference,
        Organization,
        Repository,
        RepositoryReference,
        Query,
        WorkItemType,
        PullRequests,
        PullRequestPolicyStatus,
        Notification,
    ];
}
