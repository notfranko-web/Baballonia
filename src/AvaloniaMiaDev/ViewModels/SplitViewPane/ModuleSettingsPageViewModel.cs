using AvaloniaMiaDev.Contracts;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.DependencyInjection;

namespace AvaloniaMiaDev.ViewModels.SplitViewPane;

public partial class ModuleSettingsPageViewModel : ViewModelBase
{
    [ObservableProperty]
    [property: SavedSetting("ModuleSettings_EmulateEyeWiden", false)]
    private bool _emulateEyeWiden = false;

    [ObservableProperty]
    [property: SavedSetting("ModuleSettings_EyeWidenLower", 0f)]
    private float _eyeWidenLower = 0f;

    [ObservableProperty]
    [property: SavedSetting("ModuleSettings_EyeWidenUpper", 1f)]
    private float _eyeWidenUpper = 1f;

    [ObservableProperty]
    [property: SavedSetting("ModuleSettings_EmulateEyeSquint", false)]
    private bool _emulateEyeSquint = false;

    [ObservableProperty]
    [property: SavedSetting("ModuleSettings_EyeSquintLower", 0f)]
    private float _eyeSquintLower = 0f;

    [ObservableProperty]
    [property: SavedSetting("ModuleSettings_EyeSquintUpper", 1f)]
    private float _eyeSquintUpper = 1f;

    [ObservableProperty]
    [property: SavedSetting("ModuleSettings_EmulateEyebrows", false)]
    private bool _emulateEyebrows = false;

    [ObservableProperty]
    [property: SavedSetting("ModuleSettings_EyebrowsLower", 0f)]
    private float _eyeBrowsLower = 0f;

    [ObservableProperty]
    [property: SavedSetting("ModuleSettings_EyebrowsRaise", 1f)]
    private float _eyeBrowsUpper = 1f;

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
