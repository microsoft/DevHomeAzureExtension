// Copyright (c) Microsoft Corporation and Contributors
// Licensed under the MIT license.

using AzureExtension.Contracts;
using AzureExtension.DevBox;
using AzureExtension.Services.DeveloperBox;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace DevHomeAzureExtension.Test;

public partial class DevBoxTests
{
    [TestMethod]
    [TestCategory("Manual")]

    // [Ignore("Comment out to run")]
    public void DevBoxInstance()
    {
        var host = Microsoft.Extensions.Hosting.Host.
            CreateDefaultBuilder().
            UseContentRoot(AppContext.BaseDirectory).
            UseDefaultServiceProvider((context, options) =>
            {
                options.ValidateOnBuild = true;
            }).
            ConfigureServices((context, services) =>
            {
                services.AddSingleton<IDevBoxRESTService, DevBoxTestService>();
            }).
            Build();

        var instance = new DevBoxProvider(host);
        Assert.IsNotNull(instance.GetTest());
    }
}
