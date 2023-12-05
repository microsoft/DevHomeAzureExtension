// Copyright (c) Microsoft Corporation and Contributors
// Licensed under the MIT license.

using AzureExtension.Contracts;
using AzureExtension.DevBox;
using AzureExtension.Services.DevBox;
using AzureExtension.Test.DevBox;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace DevHomeAzureExtension.Test;

public partial class DevBoxTests
{
    [TestMethod]
    [TestCategory("Unit")]
    public void DevBox_MockHTTPCalls()
    {
        // Arrange
        var host = Microsoft.Extensions.Hosting.Host.
            CreateDefaultBuilder().
            UseContentRoot(AppContext.BaseDirectory).
            UseDefaultServiceProvider((context, options) =>
            {
                options.ValidateOnBuild = true;
            }).
            ConfigureServices((context, services) =>
            {
                services.AddSingleton<IHttpClientFactory>(mockFactory.Object);
                services.AddSingleton<HttpClient>(mockHttpClient);
                services.AddSingleton<IDevBoxManagementService, DevBoxManagementService>();
                services.AddSingleton<IDevBoxAuthService, AuthService>();
                services.AddSingleton<IArmTokenService, ArmTestTokenService>();
                services.AddSingleton<IDataTokenService, DataTestTokenService>();
                services.AddSingleton<DevBoxProvider>();
                services.AddTransient<DevBoxInstance>();
            }).
            Build();

        // Act
        var instance = host.Services.GetService<DevBoxProvider>();
        var systems = instance?.GetComputeSystemsAsync(null).Result;

        // Assert
        Assert.IsTrue(systems?.Any());
    }
}
