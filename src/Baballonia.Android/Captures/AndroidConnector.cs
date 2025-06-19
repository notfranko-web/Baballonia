using System;
using System.Collections.Generic;
using Baballonia.Contracts;
using Baballonia.Services.Inference;
using Baballonia.Services.Inference.Platforms;
using Microsoft.Extensions.Logging;

namespace Baballonia.Android.Captures;

/// <summary>
/// Special class for iOS, Android and UWP platforms where VideoCapture is not fully implemented
/// Support for MJPEG video streams only presently!
/// </summary>
public class AndroidConnector : PlatformConnector, IPlatformConnector
{
    private static readonly HashSet<string> IpConnectionsPrefixes
        = new(StringComparer.OrdinalIgnoreCase) { "http", };

    private static readonly HashSet<string> IpConnectionsSuffixes
        = new(StringComparer.OrdinalIgnoreCase) { "local", "local/" };

    protected override Type DefaultCapture => typeof(AndroidCamera2Capture);

    public AndroidConnector(string url, ILogger logger, ILocalSettingsService settingsService) : base(url, logger, settingsService)
    {
        Captures = new()
        {
            { (IpConnectionsPrefixes, areSuffixes: false), typeof(IpCameraCapture) },
            { (IpConnectionsSuffixes, areSuffixes: true), typeof(IpCameraCapture) }
        };
    }
}
