// Copyright (c) Microsoft Corporation and Contributors
// Licensed under the MIT license.

namespace DevHomeAzureExtension.DataModel;

public interface IDataStoreTransaction : IDisposable
{
    void Commit();

    void Rollback();
}
