using System;
using System.Collections.Generic;
using AvaloniaMiaDev.Contracts;
using AvaloniaMiaDev.Services.Camera.Captures;
using Microsoft.Extensions.Logging;

namespace AvaloniaMiaDev.Services.Camera.Platforms;

/// <summary>
/// Special class for iOS, Android and UWP platforms where VideoCapture is not fully implemented
/// Support for MJPEG video streams only presently!
/// </summary>
public class AndroidConnector : PlatformConnector
{
    private static readonly HashSet<string> _IPConnectionsPrefixes
        = new(StringComparer.OrdinalIgnoreCase) { "http", };

    private static readonly HashSet<string> _IPConnectionsSuffixes
        = new(StringComparer.OrdinalIgnoreCase) { "local", "local/" };

    protected override Type DefaultCapture => typeof(IPCameraCapture);

    public AndroidConnector(string Url, ILogger Logger, ILocalSettingsService SettingsService) : base(Url, Logger, SettingsService)
    {
        Captures = new()
        {
            { (_IPConnectionsPrefixes, false), typeof(IPCameraCapture) },
            { (_IPConnectionsSuffixes, true), typeof(IPCameraCapture) }
        };
    }
}
