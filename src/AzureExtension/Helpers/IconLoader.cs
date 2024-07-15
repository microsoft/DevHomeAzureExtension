// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Serilog;

namespace DevHomeAzureExtension.Helpers;

public class IconLoader
{
    private static readonly Dictionary<string, string> _base64ImageRegistry = new();

    public static string GetIconAsBase64(string filename)
    {
        var log = Log.ForContext("SourceContext", nameof(IconLoader));
        log.Debug($"Asking for icon: {filename}");
        if (_base64ImageRegistry.TryAdd(filename, ConvertIconToDataString(filename)))
        {
            log.Debug($"The icon {filename} was converted and is now stored.");
        }

        return _base64ImageRegistry[filename];
    }

    private static string ConvertIconToDataString(string fileName)
    {
        var path = Path.Combine(AppContext.BaseDirectory, @"Widgets/Assets/", fileName);
        var imageData = Convert.ToBase64String(File.ReadAllBytes(path.ToString()));
        return imageData;
    }
}
