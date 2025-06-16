using Avalonia;
using Baballonia.Contracts;
using Baballonia.Desktop.Calibration.Aero;
using Baballonia.Desktop.Captures;
using Baballonia.Services;
using Baballonia.Services.Inference.Platforms;
using Baballonia.Views;
using System;
using Velopack;

namespace Baballonia.Desktop;

sealed class Program
{

    // Initialization code. Don't use any Avalonia, third-party APIs or any
    // SynchronizationContext-reliant code before AppMain is called: things aren't initialized
    // yet and stuff might break.
    [STAThread]
    public static int Main(string[] args)
    {
        var builder = BuildAvaloniaApp();
        VelopackApp.Build().Run();

        HomePageView.Overlay = new AeroOverlayTrainerCombo();
        HomePageView.Calibrator = new AeroOverlayTrainerCombo();
        InferenceService.PlatformConnectorType = typeof(DesktopConnector);

        return builder.StartWithClassicDesktopLifetime(args);
    }

    // Avalonia configuration, don't remove; also used by visual designer.
    public static AppBuilder BuildAvaloniaApp()
        => AppBuilder.Configure<App>()
            .UsePlatformDetect()
            .WithInterFont()
            .LogToTrace();
}
