// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace DevHomeAzureExtension.DataModel;

public class AzureDataStoreSchema : IDataStoreSchema
{
    public long SchemaVersion => SchemaVersionValue;

    public List<string> SchemaSqls => SchemaSqlsValue;

    public AzureDataStoreSchema()
    {
    }

    // Update this anytime incompatible changes happen with a released version.
    private const long SchemaVersionValue = 0x0003;

    private static readonly string Metadata =
    @"CREATE TABLE Metadata (" +
        "Id INTEGER PRIMARY KEY NOT NULL," +
        "Key TEXT NOT NULL COLLATE NOCASE," +
        "Value TEXT NOT NULL" +
    ");" +
    "CREATE UNIQUE INDEX IDX_Metadata_Key ON Metadata (Key);";

    private static readonly string Identity =
    @"CREATE TABLE Identity (" +
        "Id INTEGER PRIMARY KEY NOT NULL," +
        "Name TEXT NOT NULL COLLATE NOCASE," +
        "InternalId TEXT NOT NULL," +
        "Avatar TEXT NOT NULL COLLATE NOCASE," +
        "TimeUpdated INTEGER NOT NULL" +
    ");" +

    // InternalId is a Guid from the server, so by definition is unique.
    "CREATE UNIQUE INDEX IDX_Identity_InternalId ON Identity (InternalId);";

    private static readonly string Project =
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

    private static readonly string Organization =
    @"CREATE TABLE Organization (" +
        "Id INTEGER PRIMARY KEY NOT NULL," +
        "Name TEXT NOT NULL COLLATE NOCASE," +
        "Connection TEXT NOT NULL COLLATE NOCASE," +
        "TimeUpdated INTEGER NOT NULL" +
    ");" +

    // Connections should be unique per organization. We do not appear to have
    // better information about the organization and connection at this time, so
    // we will use Connection as the unique constraint. It is also used in the
    // constructor, so while the same organization may have multiple connections,
    // each connection should correspond to only one organization.
    "CREATE UNIQUE INDEX IDX_Organization_Connection ON Organization (Connection);";

    private static readonly string Query =
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

    private static readonly string WorkItemType =
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

    private static readonly string PullRequests =
    @"CREATE TABLE PullRequests (" +
        "Id INTEGER PRIMARY KEY NOT NULL," +
        "RepositoryName TEXT NOT NULL COLLATE NOCASE," +
        "DeveloperLogin TEXT NOT NULL COLLATE NOCASE," +
        "Results TEXT NOT NULL," +
        "ProjectId INTEGER NOT NULL," +
        "ViewId INTEGER NOT NULL," +
        "TimeUpdated INTEGER NOT NULL" +
    ");" +

    // Developer Pull requests are unique on Org / Project / Repository and
    // the developer login, and the view.
    "CREATE UNIQUE INDEX IDX_PullRequests_ProjectIdRepositoryNameDeveloperLoginViewId ON PullRequests (ProjectId, RepositoryName, DeveloperLogin, ViewId);";

    // All Sqls together.
    private static readonly List<string> SchemaSqlsValue = new()
    {
        Metadata,
        Identity,
        Project,
        Organization,
        Query,
        WorkItemType,
        PullRequests,
    };
}
