using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Linq;
using System.Threading.Tasks;
using Avalonia.Markup.Xaml.MarkupExtensions;
using AvaloniaMiaDev.Contracts;
using AvaloniaMiaDev.Models;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.DependencyInjection;

namespace AvaloniaMiaDev.ViewModels.SplitViewPane;

public partial class FaceCalibrationViewModel : ViewModelBase
{
    [ObservableProperty]
    private ObservableCollection<CalibrationItem> _calibrationItems;

    private ILocalSettingsService SettingsService { get; }
    private const string CalibrationItemKeyPrefix = "FaceHome_CalibrationItem";
    private bool _isInitializing = true;

    private static readonly string[] DefaultCalibrationShapes =
    [
        "/cheekPuffLeft",
        "/cheekPuffRight",
        "/cheekSuckLeft",
        "/cheekSuckRight",
        "/jawOpen",
        "/jawForward",
        "/jawLeft",
        "/jawRight",
        "/noseSneerLeft",
        "/noseSneerRight",
        "/mouthFunnel",
        "/mouthPucker",
        "/mouthLeft",
        "/mouthRight",
        "/mouthRollUpper",
        "/mouthRollLower",
        "/mouthShrugUpper",
        "/mouthShrugLower",
        "/mouthClose",
        "/mouthSmileLeft",
        "/mouthSmileRight",
        "/mouthFrownLeft",
        "/mouthFrownRight",
        "/mouthDimpleLeft",
        "/mouthDimpleRight",
        "/mouthUpperUpLeft",
        "/mouthUpperUpRight",
        "/mouthLowerDownLeft",
        "/mouthLowerDownRight",
        "/mouthPressLeft",
        "/mouthPressRight",
        "/mouthStretchLeft",
        "/mouthStretchRight",
        "/tongueOut",
        "/tongueUp",
        "/tongueDown",
        "/tongueLeft",
        "/tongueRight",
        "/tongueRoll",
        "/tongueBendDown",
        "/tongueCurlUp",
        "/tongueSquish",
        "/tongueFlat",
        "/tongueTwistLeft",
        "/tongueTwistRight"
    ];

    public FaceCalibrationViewModel()
    {
        SettingsService = Ioc.Default.GetService<ILocalSettingsService>()!;
        SettingsService.Load(this);

        Task.Run(async () =>
        {
            _calibrationItems = [];
            await LoadIndividualItemSettings();
            SubscribeToItemPropertyChanges();
            _isInitializing = false;
            return Task.CompletedTask;
        });

        PropertyChanged += (_, args) =>
        {
            SettingsService.Save(this);
        };
    }

    private async Task LoadIndividualItemSettings()
    {
        foreach (var item in DefaultCalibrationShapes)
        {
            var savedItem = await SettingsService.ReadSettingAsync<CalibrationItem>(GetCalibrationItemKey(item));
            _calibrationItems.Add(savedItem);
        }
    }

    private void SubscribeToItemPropertyChanges()
    {
        foreach (var item in _calibrationItems)
        {
            if (item is INotifyPropertyChanged notifyItem)
            {
                notifyItem.PropertyChanged += (sender, args) =>
                {
                    if (!_isInitializing && sender is CalibrationItem changedItem)
                    {
                        SaveCalibrationItem(changedItem);
                    }
                };
            }
        }
    }

    private void SaveCalibrationItem(CalibrationItem item)
    {
        var key = GetCalibrationItemKey(item);
        SettingsService.SaveSettingAsync(key, item).ConfigureAwait(false);
    }

    public static string GetCalibrationItemKey(CalibrationItem item)
    {
        return $"{CalibrationItemKeyPrefix}{item.ShapeName!.Replace("/", "_")}";
    }

    public static string GetCalibrationItemKey(string item)
    {
        return $"{CalibrationItemKeyPrefix}{item.Replace("/", "_")}";
    }
}
