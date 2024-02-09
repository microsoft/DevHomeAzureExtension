// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace DevHomeAzureExtension.DataModel;

public partial class DataStoreOptions
{
    private const string DataStoreFileNameDefault = "AzureDataStore.db";

    public string DataStoreFileName { get; set; } = DataStoreFileNameDefault;

    // The Temp Path is used for storage by default so tests can run this code without being packaged.
    // If we directly put in the ApplicationData folder, it would fail anytime the program was not packaged.
    // For use with packaged application, set in Options to:
    //     ApplicationData.Current.LocalFolder.Path
    private readonly string dataStoreFolderPathDefault = Path.Combine(Path.GetTempPath(), "AzureExtension");

    // ApplicationData is not static, using a static folder for initialization.
    private string? dataStoreFolderPath;

    public string DataStoreFolderPath
    {
        get => dataStoreFolderPath is null ? dataStoreFolderPathDefault : dataStoreFolderPath;
        set => dataStoreFolderPath = string.IsNullOrEmpty(value) ? dataStoreFolderPathDefault : value;
    }

    public IDataStoreSchema? DataStoreSchema { get; set; }

    public bool RecreateDataStore { get; set; }
}
