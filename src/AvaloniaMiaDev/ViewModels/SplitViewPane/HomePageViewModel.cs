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

namespace AvaloniaMiaDev.ViewModels.SplitViewPane;

public partial class HomePageViewModel : ViewModelBase
{
    // Left eye properties
    [ObservableProperty]
    private WriteableBitmap _leftEyeBitmap;

    [ObservableProperty]
    [property: SavedSetting("EyeTrackVRService_LeftCameraIndex", "0")]
    private string _leftCameraAddress;

    [ObservableProperty]
    private int _leftOverlayRectangleCanvasX;

    [ObservableProperty]
    private int _leftOverlayRectangleCanvasY;

    [ObservableProperty]
    [property: SavedSetting("EyeSettings_FlipLeftEyeXAxis", false)]
    private bool _flipLeftEyeXAxis;

    [ObservableProperty]
    [property: SavedSetting("EyeSettings_FlipLeftEyeYAxis", false)]
    private bool _flipLeftEyeYAxis;

    [ObservableProperty]
    [property: SavedSetting("EyeSettings_LeftEyeRotation", 0f)]
    private float _leftEyeRotation;

    // Right eye properties
    [ObservableProperty]
    private WriteableBitmap _rightEyeBitmap;

    [ObservableProperty]
    [property: SavedSetting("EyeTrackVRService_IndexCameraIndex", "0")]
    private string _rightCameraAddress;

    [ObservableProperty]
    private int _rightOverlayRectangleCanvasX;

    [ObservableProperty]
    private int _rightOverlayRectangleCanvasY;

    [ObservableProperty]
    [property: SavedSetting("EyeSettings_FlipRightEyeXAxis", false)]
    private bool _flipRightEyeXAxis;

    [ObservableProperty]
    [property: SavedSetting("EyeSettings_FlipRightEyeYAxis", false)]
    private bool _flipRightEyeYAxis;

    [ObservableProperty]
    [property: SavedSetting("EyeSettings_RightEyeRotation", 0f)]
    private float _rightEyeRotation;

    // Shared properties
    [ObservableProperty]
    private Canvas _overlayCanvas;

    [ObservableProperty]
    private Rect _overlayRectangle;

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
