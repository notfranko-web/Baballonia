using System.Threading.Tasks;
using Avalonia;
using Avalonia.Styling;
using Avalonia.Threading;
using Baballonia.Contracts;

namespace Baballonia.Services;

public class ThemeSelectorService(ILocalSettingsService localSettingsService) : IThemeSelectorService
{
    private const string SettingsKey = "AppBackgroundRequestedTheme";

    public ThemeVariant Theme { get; set; } = ThemeVariant.Default;

    public async Task InitializeAsync()
    {
        Theme = await LoadThemeFromSettingsAsync();
        await SetRequestedThemeAsync();
        await Task.CompletedTask;
    }

    public async Task SetThemeAsync(ThemeVariant theme)
    {
        Theme = theme;
        await SetRequestedThemeAsync();
        await SaveThemeInSettingsAsync(Theme);
    }

    public Task SetRequestedThemeAsync()
    {
        Dispatcher.UIThread.Invoke(() => Application.Current!.RequestedThemeVariant = Theme);
        return Task.CompletedTask;
    }

    private async Task<ThemeVariant> LoadThemeFromSettingsAsync()
    {
        var themeName = await localSettingsService.ReadSettingAsync<string>(SettingsKey, "Default");
        return themeName switch
        {
            "Default" => ThemeVariant.Default,
            "Dark" => ThemeVariant.Dark,
            "Light" => ThemeVariant.Light,
            _ => ThemeVariant.Default
        };
    }

    private async Task SaveThemeInSettingsAsync(ThemeVariant theme)
    {
        await localSettingsService.SaveSettingAsync(SettingsKey, theme.ToString());
    }
}
