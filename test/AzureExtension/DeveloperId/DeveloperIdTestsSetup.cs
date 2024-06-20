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

    private TestOptions _testOptions = new();

    private TestOptions TestOptions
    {
        get => _testOptions;
        set => _testOptions = value;
    }

    [TestInitialize]
    public void TestInitialize()
    {
        TestOptions = TestHelpers.SetupTempTestOptions(TestContext!);
        TestHelpers.ConfigureTestLog(TestOptions, TestContext!);
    }

    [TestCleanup]
    public void Cleanup()
    {
        TestHelpers.CloseTestLog();
        TestHelpers.CleanupTempTestOptions(TestOptions, TestContext!);
    }

    private static readonly Semaphore _authenticationEventTriggered = new(initialCount: 0, maximumCount: 1);

    public void AuthenticationEvent(object? sender, IDeveloperId developerId)
    {
        if (developerId.LoginId is not null)
        {
            _authenticationEventTriggered.Release();
        }
    }

    public void Dispose()
    {
        GC.SuppressFinalize(this);
        _authenticationEventTriggered.Dispose();
    }
}
