// Copyright (c) Microsoft Corporation and Contributors
// Licensed under the MIT license.

using AzureExtension.Contracts;
using AzureExtension.DevBox;
using AzureExtension.Services.DevBox;
using DevHomeAzureExtension.DataModel;
using DevHomeAzureExtension.DeveloperId;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Windows.AppLifecycle;
using Microsoft.Windows.AppNotifications;
using Windows.ApplicationModel.Activation;
using Windows.Management.Deployment;
using Windows.Storage;
using Log = DevHomeAzureExtension.ExtensionServer.Log;

namespace DevHomeAzureExtension;

public sealed class Program
{
    [MTAThread]
    public static void Main([System.Runtime.InteropServices.WindowsRuntime.ReadOnlyArray] string[] args)
    {
        Log.Logger()?.ReportInfo($"Launched with args: {string.Join(' ', args.ToArray())}");
        LogPackageInformation();

        // Set up notification handling. This must happen before GetActivatedEventArgs().
        var notificationManager = new Notifications.NotificationManager(Notifications.NotificationHandler.OnNotificationInvoked);

        // Force the app to be single instanced.
        // Get or register the main instance.
        var mainInstance = AppInstance.FindOrRegisterForKey("mainInstance");
        var activationArgs = AppInstance.GetCurrent().GetActivatedEventArgs();
        if (!mainInstance.IsCurrent)
        {
            Log.Logger()?.ReportInfo($"Not main instance, redirecting.");
            mainInstance.RedirectActivationToAsync(activationArgs).AsTask().Wait();
            notificationManager.Unregister();
            return;
        }

        // Register for activation redirection.
        AppInstance.GetCurrent().Activated += AppActivationRedirected;

        if (args.Length > 0 && args[0] == "-RegisterProcessAsComServer")
        {
            HandleCOMServerActivation();
        }
        else if (activationArgs.Kind == ExtendedActivationKind.AppNotification)
        {
            HandleNotificationActivation(activationArgs);
        }
        else
        {
            Log.Logger()?.ReportWarn("Not being launched as a ComServer... exiting.");
        }

        notificationManager.Unregister();
        Log.Logger()?.Dispose();
    }

    private static void AppActivationRedirected(object? sender, Microsoft.Windows.AppLifecycle.AppActivationArguments activationArgs)
    {
        Log.Logger()?.ReportInfo($"Redirected with kind: {activationArgs.Kind}");

        // Handle COM server.
        if (activationArgs.Kind == ExtendedActivationKind.Launch)
        {
            var d = activationArgs.Data as ILaunchActivatedEventArgs;
            var args = d?.Arguments.Split();

            if (args?.Length > 0 && args[1] == "-RegisterProcessAsComServer")
            {
                Log.Logger()?.ReportInfo($"Activation COM Registration Redirect: {string.Join(' ', args.ToList())}");
                HandleCOMServerActivation();
            }
        }

        // Handle Notification.
        if (activationArgs.Kind == ExtendedActivationKind.AppNotification)
        {
            HandleNotificationActivation(activationArgs);
        }

        // Handle Protocol.
        if (activationArgs.Kind == ExtendedActivationKind.Protocol)
        {
            var d = activationArgs.Data as IProtocolActivatedEventArgs;
            if (d is not null)
            {
                Log.Logger()?.ReportInfo($"Protocol Activation redirected from: {d.Uri}");
                HandleProtocolActivation(d.Uri);
            }
        }
    }

    private static void HandleNotificationActivation(AppActivationArguments activationArgs)
    {
        var notificationArgs = activationArgs.Data as AppNotificationActivatedEventArgs;
        if (notificationArgs != null)
        {
            Log.Logger()?.ReportInfo($"Notification Activation.");
            Notifications.NotificationHandler.NotificationActivation(notificationArgs);
        }
    }

    private static void HandleProtocolActivation(Uri oauthRedirectUri)
    {
        Log.Logger()?.ReportError($"Protocol Activation not implemented.");
    }

    private static void HandleCOMServerActivation()
    {
        Log.Logger()?.ReportInfo($"Activating COM Server");

        // Register and run COM server.
        // This could be called by either of the COM registrations, we will do them all to avoid deadlock and bind all on the extension's lifetime.
        using var extensionServer = new Microsoft.Windows.DevHome.SDK.ExtensionServer();
        var extensionDisposedEvent = new ManualResetEvent(false);

        // Create host with dependency injection
        using var host = CreateHost();
        var extensionInstance = new AzureExtension(extensionDisposedEvent, host);

        // We are instantiating extension instance once above, and returning it every time the callback in RegisterExtension below is called.
        // This makes sure that only one instance of SampleExtension is alive, which is returned every time the host asks for the IExtension object.
        // If you want to instantiate a new instance each time the host asks, create the new instance inside the delegate.
        extensionServer.RegisterExtension(() => extensionInstance, true);

        // We may have received an event on previous launch that the datastore should be recreated.
        // It should be recreated now before anything else tries to use it.
        RecreateDataStoreIfNecessary();

        // In the case that this is the first launch we will try to automatically connect the default Windows account
        var devIdProvider = DeveloperIdProvider.GetInstance();
        devIdProvider.EnableSSOForAzureExtensionAsync();

        // Do Widget COM server registration
        // We are not using a disposed event for this, as we want the widgets to be disposed when the extension is disposed.
        using var widgetServer = new Widgets.WidgetServer();
        var widgetProviderInstance = new Widgets.WidgetProvider();
        widgetServer.RegisterWidget(() => widgetProviderInstance);

        // This will make the main thread wait until the event is signaled by the extension class.
        // Since we have single instance of the extension object, we exit as soon as it is disposed.
        extensionDisposedEvent.WaitOne();
        Log.Logger()?.ReportInfo($"Extension is disposed.");
    }

    private static void LogPackageInformation()
    {
        var relatedPackageFamilyNames = new string[]
        {
              "MicrosoftWindows.Client.WebExperience_cw5n1h2txyewy",
              "Microsoft.Windows.DevHome_8wekyb3d8bbwe",
              "Microsoft.Windows.DevHomeAzureExtension_8wekyb3d8bbwe",
              "Microsoft.Windows.DevHomeAzureExtension.Dev_8wekyb3d8bbwe",
        };

        try
        {
            var packageManager = new PackageManager();
            foreach (var pfn in relatedPackageFamilyNames)
            {
                foreach (var package in packageManager.FindPackagesForUser(string.Empty, pfn))
                {
                    Log.Logger()?.ReportInfo($"{package.Id.FullName}  DevMode: {package.IsDevelopmentMode}  Signature: {package.SignatureKind}");
                }
            }
        }
        catch (Exception ex)
        {
            Log.Logger()?.ReportError("Failed getting package information.", ex);
        }
    }

    private static void RecreateDataStoreIfNecessary()
    {
        try
        {
            var localSettings = ApplicationData.Current.LocalSettings;
            if (localSettings.Values.ContainsKey(AzureDataManager.RecreateDataStoreSettingsKey))
            {
                var recreateDataStore = (bool)localSettings.Values[AzureDataManager.RecreateDataStoreSettingsKey];
                if (recreateDataStore)
                {
                    Log.Logger()?.ReportInfo("Recreating DataStore");

                    // Creating an instance of AzureDataManager with the recreate option will
                    // attempt to recreate the datastore. A new options is created to avoid
                    // altering the default options since it is a singleton.
                    var dataStoreOptions = new DataStoreOptions
                    {
                        DataStoreFileName = AzureDataManager.DefaultOptions.DataStoreFileName,
                        DataStoreSchema = AzureDataManager.DefaultOptions.DataStoreSchema,
                        DataStoreFolderPath = AzureDataManager.DefaultOptions.DataStoreFolderPath,
                        RecreateDataStore = true,
                    };

                    using var dataManager = AzureDataManager.CreateInstance("RecreateDataStore", dataStoreOptions);

                    // After we get this key once, set it back to false.
                    localSettings.Values[AzureDataManager.RecreateDataStoreSettingsKey] = false;
                }
            }
        }
        catch (Exception ex)
        {
            Log.Logger()?.ReportError("Failed attempting to verify or perform database recreation.", ex);
        }
    }

    private static IHost CreateHost()
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
                // Dev Box
                services.AddHttpClient();
                services.AddSingleton<IDevBoxManagementService, DevBoxManagementService>();
                services.AddSingleton<IDevBoxAuthService, AuthService>();
                services.AddSingleton<IArmTokenService, ARMTokenService>();
                services.AddSingleton<IDataTokenService, DataTokenService>();
                services.AddSingleton<DevBoxProvider>();
                services.AddTransient<DevBoxInstance>();
            }).
        Build();

        Log.Logger()?.ReportInfo("Services Host creation successful");
        return host;
    }
}
