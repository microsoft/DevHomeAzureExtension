// Copyright (c) Microsoft Corporation and Contributors
// Licensed under the MIT license.

namespace DevHomeAzureExtension.Test;

[TestClass]
public partial class RepositoryProviderTests
{
#pragma warning disable SA1310 // Field names should not contain underscore
    private const string WASDK_URL = "https://github.com/microsoft/windowsappsdk";
#pragma warning restore SA1310 // Field names should not contain underscore
#pragma warning disable SA1310 // Field names should not contain underscore
    private const string WASDK_CLONE_URL = "https://github.com/microsoft/WindowsAppSDK.git";
#pragma warning restore SA1310 // Field names should not contain underscore
#pragma warning disable SA1310 // Field names should not contain underscore
    private const string CLONE_LOCATION = "CloneToMe";
#pragma warning restore SA1310 // Field names should not contain underscore

    public void SetUpRepoTests()
    {
        if (Directory.Exists(CLONE_LOCATION))
        {
            Directory.Delete(CLONE_LOCATION, true);
        }

        Directory.CreateDirectory(CLONE_LOCATION);
    }

    public void RemoveCloneLocation()
    {
        if (Directory.Exists(CLONE_LOCATION))
        {
            Directory.Delete(CLONE_LOCATION, true);
        }
    }

    public TestContext? TestContext
    {
        get;
        set;
    }

    private TestOptions testOptions = new();

    private TestOptions TestOptions
    {
        get => testOptions;
        set => testOptions = value;
    }

    [TestInitialize]
    public void TestInitialize()
    {
        SetUpRepoTests();
        TestOptions = TestHelpers.SetupTempTestOptions(TestContext!);
    }

    [TestCleanup]
    public void Cleanup()
    {
        RemoveCloneLocation();
        TestHelpers.CleanupTempTestOptions(TestOptions, TestContext!);
    }
}
