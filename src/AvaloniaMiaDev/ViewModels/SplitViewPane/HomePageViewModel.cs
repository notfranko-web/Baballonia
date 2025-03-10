using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using AvaloniaMiaDev.Contracts;
using AvaloniaMiaDev.OSC;
using AvaloniaMiaDev.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.DependencyInjection;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.DependencyInjection;

namespace AvaloniaMiaDev.ViewModels.SplitViewPane;

public partial class HomePageViewModel : ViewModelBase
{
    // Left eye properties
    [ObservableProperty]
    private WriteableBitmap _leftEyeBitmap;

    [ObservableProperty]
    private int _leftOverlayRectangleCanvasX;

    [ObservableProperty]
    private int _leftOverlayRectangleCanvasY;

    [ObservableProperty]
    [SavedSetting("EyeSettings_FlipLeftEyeXAxis", false)]
    private bool _flipLeftEyeXAxis;

    [ObservableProperty]
    [SavedSetting("EyeSettings_LeftEyeCircleCrop", false)]
    private bool _leftEyeCircleCrop;

    [ObservableProperty]
    [SavedSetting("EyeSettings_LeftEyeRotation", 0f)]
    private float _leftEyeRotation;

    // Right eye properties
    [ObservableProperty]
    private WriteableBitmap _rightEyeBitmap;

    [ObservableProperty]
    private int _rightOverlayRectangleCanvasX;

    [ObservableProperty]
    private int _rightOverlayRectangleCanvasY;

    [ObservableProperty]
    [SavedSetting("EyeSettings_FlipRightEyeXAxis", false)]
    private bool _flipRightEyeXAxis;

    [ObservableProperty]
    [SavedSetting("EyeSettings_RightEyeCircleCrop", false)]
    private bool _rightEyeCircleCrop;

    [ObservableProperty]
    [SavedSetting("EyeSettings_RightEyeRotation", 0f)]
    private float _rightEyeRotation;

    // Shared properties
    [ObservableProperty]
    private Canvas _overlayCanvas;

    [ObservableProperty]
    private Rect _overlayRectangle;

    [ObservableProperty]
    [SavedSetting("EyeSettings_FlipEyeYAxis", false)]
    private bool _flipEyeYAxis;

    // Services and other properties
    public IOscTarget OscTarget { get; }
    private OscRecvService OscRecvService { get; }
    private OscSendService OscSendService { get; }
    private ILocalSettingsService LocalSettingsService { get; }

    private int _messagesRecvd;
    [ObservableProperty] private string _messagesInPerSecCount;

    private int _messagesSent;
    [ObservableProperty] private string _messagesOutPerSecCount;

    private readonly DispatcherTimer _msgCounterTimer;

    public HomePageViewModel()
    {
        // Services
        OscTarget = Ioc.Default.GetService<IOscTarget>()!;
        OscRecvService = Ioc.Default.GetService<OscRecvService>()!;
        OscSendService = Ioc.Default.GetService<OscSendService>()!;
        LocalSettingsService = Ioc.Default.GetService<ILocalSettingsService>()!;
        LocalSettingsService.Load(this);

        // Message Timer
        MessagesInPerSecCount = "0";
        MessagesOutPerSecCount = "0";
        OscRecvService.OnMessageReceived += MessageReceived;
        OscSendService.OnMessagesDispatched += MessageDispatched;
        _msgCounterTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromSeconds(1)
        };
        _msgCounterTimer.Tick += (_, _) =>
        {
            MessagesInPerSecCount = _messagesRecvd.ToString();
            _messagesRecvd = 0;

            MessagesOutPerSecCount = _messagesSent.ToString();
            _messagesSent = 0;
        };
        _msgCounterTimer.Start();

        PropertyChanged += (_, _) =>
        {
            LocalSettingsService.Save(this);
        };
    }

    private void MessageReceived(OscMessage msg) => _messagesRecvd++;
    private void MessageDispatched(int msgCount) => _messagesSent += msgCount;

    ~HomePageViewModel()
    {
        OscRecvService.OnMessageReceived -= MessageReceived;
        OscSendService.OnMessagesDispatched -= MessageDispatched;

        _msgCounterTimer.Stop();
    }
}
