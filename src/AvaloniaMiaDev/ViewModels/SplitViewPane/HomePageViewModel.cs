using System;
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
    public IOscTarget OscTarget { get; }
    private OscRecvService OscRecvService { get; }
    private OscSendService OscSendService { get; }

    private int _messagesRecvd;
    [ObservableProperty] private string _messagesInPerSecCount;

    private int _messagesSent;
    [ObservableProperty] private string _messagesOutPerSecCount;

    [ObservableProperty]
    [SavedSetting("EyeSettings_FlipLeftEyeXAxis", false)]
    private bool _flipLeftEyeXAxis;

    [ObservableProperty]
    [SavedSetting("EyeSettings_FlipRightEyeYAxis", false)]
    private bool _flipRightEyeYAxis;

    [ObservableProperty]
    [SavedSetting("EyeSettings_FlipEyeYAxis", false)]
    private bool _flipEyeYAxis;

    [ObservableProperty]
    [SavedSetting("EyeSettings_LeftEyeCircleCrop", false)]
    private bool _leftEyeCircleCrop;

    [ObservableProperty]
    [SavedSetting("EyeSettings_RightEyeCircleCrop", false)]
    private bool _rightEyeCircleCrop;

    private readonly DispatcherTimer _msgCounterTimer;

    public HomePageViewModel()
    {
        // Services
        OscTarget = Ioc.Default.GetService<IOscTarget>()!;
        OscRecvService = Ioc.Default.GetService<OscRecvService>()!;
        OscSendService = Ioc.Default.GetService<OscSendService>()!;

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
