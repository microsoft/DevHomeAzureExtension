// Copyright (c) Microsoft Corporation and Contributors
// Licensed under the MIT license.

using Microsoft.Windows.DevHome.SDK;

namespace DevHomeAzureExtension.Test;

[TestClass]
public partial class DevBoxTests : IDisposable
{
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
        TestOptions = TestHelpers.SetupTempTestOptions(TestContext!);
    }

    [TestCleanup]
    public void Cleanup()
    {
        TestHelpers.CleanupTempTestOptions(TestOptions, TestContext!);
    }

    public void Dispose()
    {
        GC.SuppressFinalize(this);
    }
}
