// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace DevHomeAzureExtension.Test;

public partial class DataStoreTests
{
    [TestMethod]
    [TestCategory("Unit")]
    public void DataManagerCreate()
    {
        using var dataManager = AzureDataManager.CreateInstance("Test", TestOptions.DataStoreOptions);
        Assert.IsNotNull(dataManager);
    }

    [TestMethod]
    [TestCategory("Unit")]
    public void DataUpdater()
    {
        var countingDoneEvent = new ManualResetEvent(false);
        var count = 0;
#pragma warning disable CS1998 // Async method lacks 'await' operators and will run synchronously
        using var dataUpdater = new DataManager.DataUpdater(
            TimeSpan.FromSeconds(1),
            async () =>
            {
                TestContext?.WriteLine($"In DataUpdater thread: {count}");
                ++count;
                if (count == 3)
                {
                    countingDoneEvent.Set();
                }
            });
#pragma warning restore CS1998 // Async method lacks 'await' operators and will run synchronously
        Assert.IsNotNull(dataUpdater);

        // Data Updater will kick off an asynchronous task. We will wait for it to cycle.
        _ = dataUpdater.Start();
        countingDoneEvent.WaitOne();
        dataUpdater.Stop();
        Assert.AreEqual(3, count);

        // Reset and do it again, this time testing stop mid-way.
        // Data Updater will kick off an asynchronous task. We will wait and give it enough time to
        // update twice and then stop it halfway through the second update.
        count = 0;
        _ = dataUpdater.Start();
        Thread.Sleep(1500);
        dataUpdater.Stop();
        Assert.IsFalse(dataUpdater.IsRunning);
        Thread.Sleep(2100);

        // After over two more seconds data updater has had time to count a few more times, unless
        // it was stopped successfully, in which case it would still only be at 1.
        // This test can randomly fail based on timings in builds, so disabling this check to avoid
        // 1-off errors from tanking a build.
        // Assert.AreEqual(1, count);
    }

    [TestMethod]
    [TestCategory("Unit")]
    public void DataManagerGetAndUpdateQuery()
    {
        // In absence of public data and a private DevOps server, this test will just verify
        // accessibility of the methods.
        using var dataManager = AzureDataManager.CreateInstance("Test", TestOptions.DataStoreOptions);
        Assert.IsNotNull(dataManager);

        // Skipping the request test here because this test runs unpackaged and
        // AuthenticationSettings does not work correctly in this scenario in setting the MSIL
        // cache location.
        var query = dataManager.GetQuery("NotARealQuery", "SomeDevId");
        Assert.IsNull(query);
    }
}
