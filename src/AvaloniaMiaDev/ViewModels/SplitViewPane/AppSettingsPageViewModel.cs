using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using Avalonia.Threading;
using AvaloniaMiaDev.Contracts;
using AvaloniaMiaDev.Models;
using AvaloniaMiaDev.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.DependencyInjection;
using Vector = Avalonia.Vector;

namespace AvaloniaMiaDev.ViewModels.SplitViewPane;

public partial class AppSettingsPageViewModel : ViewModelBase
{
    public IOscTarget OscTarget { get; private set;}
    public ILocalSettingsService SettingsService { get; private set;}
    public GithubService GithubService { get; private set;}
    public ParameterSenderService ParameterSenderService { get; private set;}

    [ObservableProperty]
    [SavedSetting("AppSettings_RecalibrateAddress", "/avatar/parameters/etvr_recalibrate")]
    private string _recalibrateAddress;

    [ObservableProperty]
    [SavedSetting("AppSettings_RecalibrateAddress", "/avatar/parameters/etvr_recenter")]
    private string _recenterAddress;

    [ObservableProperty] [SavedSetting("AppSettings_CheckForUpdates", false)]
    private bool checkForUpdates;

    public AppSettingsPageViewModel()
    {
        // General/Calibration Settings
        OscTarget = Ioc.Default.GetService<IOscTarget>()!;
        GithubService = Ioc.Default.GetService<GithubService>()!;
        SettingsService = Ioc.Default.GetService<ILocalSettingsService>()!;
        SettingsService.Load(this);

        // Risky Settings
        ParameterSenderService = Ioc.Default.GetService<ParameterSenderService>()!;

        PropertyChanged += (_, _) =>
        {
            SettingsService.Save(this);
        };
    }
}
