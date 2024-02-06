// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Microsoft.Windows.DevHome.SDK;

namespace DevHomeAzureExtension.Test;

[TestClass]
public partial class DeveloperIdTests : IDisposable
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

    private static readonly Semaphore AuthenticationEventTriggered = new(initialCount: 0, maximumCount: 1);

    public void AuthenticationEvent(object? sender, IDeveloperId developerId)
    {
        if (developerId.LoginId is not null)
        {
            AuthenticationEventTriggered.Release();
        }
    }

    public void Dispose()
    {
        GC.SuppressFinalize(this);
        AuthenticationEventTriggered.Dispose();
    }
}
