// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using AzureExtension.DevBox.Helpers;
using Microsoft.Windows.DevHome.SDK;
using Windows.Foundation;

namespace AzureExtension.DevBox;

public class DevBoxApplyConfig : IApplyConfigurationOperation
{
    public event TypedEventHandler<IApplyConfigurationOperation, ApplyConfigurationActionRequiredEventArgs> ActionRequired = (s, e) => { };

    public event TypedEventHandler<IApplyConfigurationOperation, ConfigurationSetStateChangedEventArgs> ConfigurationSetStateChanged = (s, e) => { };

    public IAsyncOperation<ApplyConfigurationResult> StartAsync() => throw new NotImplementedException();

    public DevBoxApplyConfig(string config)
    {
        var encodedConfiguration = DevBoxOperationHelper.Base64Encode(config);
    }

    public DevBoxApplyConfig(Exception ex)
    {
    }
}
