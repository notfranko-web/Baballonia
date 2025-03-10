using System;
using System.Collections.Generic;
using AvaloniaMiaDev.Contracts;
using AvaloniaMiaDev.Services.Camera.Captures;
using Microsoft.Extensions.Logging;

namespace AvaloniaMiaDev.Services.Camera.Platforms;

/// <summary>
/// Base class for camera capture and frame processing
/// </summary>
public class DesktopConnector : PlatformConnector
{
    private static readonly HashSet<string> _serialConnections
        = new(StringComparer.OrdinalIgnoreCase) { "com" };

    private static readonly HashSet<string> _IPConnectionsPrefixes
        = new(StringComparer.OrdinalIgnoreCase) { "http", };

    private static readonly HashSet<string> _IPConnectionsSuffixes
        = new(StringComparer.OrdinalIgnoreCase) { "local", "local/" };

    protected override Type DefaultCapture => typeof(OpenCVCapture);

    public DesktopConnector(string Url, ILogger Logger, ILocalSettingsService SettingsService) : base(Url, Logger, SettingsService)
    {
        Captures = new()
        {
            { (_serialConnections, false), typeof(SerialCameraCapture) },
            { (_IPConnectionsPrefixes, false), typeof(IPCameraCapture) },
            { (_IPConnectionsSuffixes, true), typeof(IPCameraCapture) }
        };
    }
}
