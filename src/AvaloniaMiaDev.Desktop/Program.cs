using System;
using System.Linq;
using Avalonia;
using AvaloniaMiaDev.Models;
using DesktopNotifications;
using DesktopNotifications.Avalonia;
using INotificationManager = DesktopNotifications.INotificationManager;
using Notification = DesktopNotifications.Notification;

namespace AvaloniaMiaDev.Desktop;

sealed class Program
{
    internal static INotificationManager NotificationManager = null!;

    // Initialization code. Don't use any Avalonia, third-party APIs or any
    // SynchronizationContext-reliant code before AppMain is called: things aren't initialized
    // yet and stuff might break.
    [STAThread]
    public static int Main(string[] args)
    {
        var builder = BuildAvaloniaApp();

        App.SendNotification += NotificationRequested;
        NotificationManager.NotificationActivated += OnNotificationActivated;
        NotificationManager.NotificationDismissed += OnNotificationDismissed;

        return builder.StartWithClassicDesktopLifetime(args);
    }

    private static void NotificationRequested(NotificationModel notificationModel)
    {
        Notification notification = new()
        {
            Title = notificationModel.Title,
            Body = notificationModel.Body,
            BodyImagePath = notificationModel.BodyImagePath,
            BodyImageAltText = notificationModel.BodyAltText!
        };

        if (notificationModel.ActionButtons.Count != 0)
            notification.Buttons.AddRange(notificationModel.ActionButtons.
                Where(x => x.HasValue).
                Select(x => x!.Value));

        if (notificationModel is { OptionalScheduledTime: not null, OptionalExpirationTime: not null })
            NotificationManager.ScheduleNotification(notification, notificationModel.OptionalScheduledTime.Value, notificationModel.OptionalExpirationTime.Value);
        else if (notificationModel.OptionalScheduledTime.HasValue)
            NotificationManager.ScheduleNotification(notification, notificationModel.OptionalScheduledTime.Value);
        else
            NotificationManager.ShowNotification(notification);
    }

    private static void OnNotificationActivated(object? sender, NotificationActivatedEventArgs e)
    {

    }

    private static void OnNotificationDismissed(object? sender, NotificationDismissedEventArgs e)
    {

    }

    // Avalonia configuration, don't remove; also used by visual designer.
    public static AppBuilder BuildAvaloniaApp()
        => AppBuilder.Configure<App>()
            .UsePlatformDetect()
            .WithInterFont()
            .LogToTrace()
            .SetupDesktopNotifications(out NotificationManager!);
}
