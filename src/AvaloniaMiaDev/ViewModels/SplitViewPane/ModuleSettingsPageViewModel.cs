using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Linq;
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

public partial class ModuleSettingsPageViewModel : ViewModelBase
{
    [ObservableProperty]
    [SavedSetting("ModuleSettings_EmulateEyeWiden", false)]
    private bool emulateEyeWiden = false;

    [ObservableProperty]
    [SavedSetting("ModuleSettings_EyeWidenLower", 0f)]
    private float eyeWidenLower = 0f;

    [ObservableProperty]
    [SavedSetting("ModuleSettings_EyeWidenUpper", 1f)]
    private float eyeWidenUpper = 1f;

    [ObservableProperty]
    [SavedSetting("ModuleSettings_EmulateEyeSquint", false)]
    private bool emulateEyeSquint = false;

    [ObservableProperty]
    [SavedSetting("ModuleSettings_EyeSquintLower", 0f)]
    private float eyeSquintLower = 0f;

    [ObservableProperty]
    [SavedSetting("ModuleSettings_EyeSquintUpper", 1f)]
    private float eyeSquintUpper = 1f;

    [ObservableProperty]
    [SavedSetting("ModuleSettings_EmulateEyebrows", false)]
    private bool emulateEyebrows = false;

    [ObservableProperty]
    [SavedSetting("ModuleSettings_EyebrowsLower", 0f)]
    private float eyeBrowsLower = 0f;

    [ObservableProperty]
    [SavedSetting("ModuleSettings_EyebrowsRaise", 1f)]
    private float eyeBrowsRaise = 1f;

    private ILocalSettingsService SettingsService { get; }

    public ModuleSettingsPageViewModel()
    {
        SettingsService = Ioc.Default.GetService<ILocalSettingsService>()!;
        SettingsService.Load(this);

        PropertyChanged += (_, _) =>
        {
            SettingsService.Save(this);
        };
    }
}
