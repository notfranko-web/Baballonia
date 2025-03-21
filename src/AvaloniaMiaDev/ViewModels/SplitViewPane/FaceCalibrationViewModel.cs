using System.Collections.ObjectModel;
using AvaloniaMiaDev.Contracts;
using AvaloniaMiaDev.Models;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.DependencyInjection;

namespace AvaloniaMiaDev.ViewModels.SplitViewPane;

public partial class FaceCalibrationViewModel : ViewModelBase
{
    public ObservableCollection<CalibrationItem> CalibrationItems { get; } =
    [
        new CalibrationItem { ShapeName = "/cheekPuffLeft", Min = 0, Max = 1 },
        new CalibrationItem { ShapeName = "/cheekPuffRight", Min = 0, Max = 1 },
        new CalibrationItem { ShapeName = "/cheekSuckLeft", Min = 0, Max = 1 },
        new CalibrationItem { ShapeName = "/cheekSuckRight", Min = 0, Max = 1 },
        new CalibrationItem { ShapeName = "/jawOpen", Min = 0, Max = 1 },
        new CalibrationItem { ShapeName = "/jawForward", Min = 0, Max = 1 },
        new CalibrationItem { ShapeName = "/jawLeft", Min = 0, Max = 1 },
        new CalibrationItem { ShapeName = "/jawRight", Min = 0, Max = 1 },
        new CalibrationItem { ShapeName = "/noseSneerLeft", Min = 0, Max = 1 },
        new CalibrationItem { ShapeName = "/noseSneerRight", Min = 0, Max = 1 },
        new CalibrationItem { ShapeName = "/mouthFunnel", Min = 0, Max = 1 },
        new CalibrationItem { ShapeName = "/mouthPucker", Min = 0, Max = 1 },
        new CalibrationItem { ShapeName = "/mouthLeft", Min = 0, Max = 1 },
        new CalibrationItem { ShapeName = "/mouthRight", Min = 0, Max = 1 },
        new CalibrationItem { ShapeName = "/mouthRollUpper", Min = 0, Max = 1 },
        new CalibrationItem { ShapeName = "/mouthRollLower", Min = 0, Max = 1 },
        new CalibrationItem { ShapeName = "/mouthShrugUpper", Min = 0, Max = 1 },
        new CalibrationItem { ShapeName = "/mouthShrugLower", Min = 0, Max = 1 },
        new CalibrationItem { ShapeName = "/mouthClose", Min = 0, Max = 1 },
        new CalibrationItem { ShapeName = "/mouthSmileLeft", Min = 0, Max = 1 },
        new CalibrationItem { ShapeName = "/mouthSmileRight", Min = 0, Max = 1 },
        new CalibrationItem { ShapeName = "/mouthFrownLeft", Min = 0, Max = 1 },
        new CalibrationItem { ShapeName = "/mouthFrownRight", Min = 0, Max = 1 },
        new CalibrationItem { ShapeName = "/mouthDimpleLeft", Min = 0, Max = 1 },
        new CalibrationItem { ShapeName = "/mouthDimpleRight", Min = 0, Max = 1 },
        new CalibrationItem { ShapeName = "/mouthUpperUpLeft", Min = 0, Max = 1 },
        new CalibrationItem { ShapeName = "/mouthUpperUpRight", Min = 0, Max = 1 },
        new CalibrationItem { ShapeName = "/mouthLowerDownLeft", Min = 0, Max = 1 },
        new CalibrationItem { ShapeName = "/mouthLowerDownRight", Min = 0, Max = 1 },
        new CalibrationItem { ShapeName = "/mouthPressLeft", Min = 0, Max = 1 },
        new CalibrationItem { ShapeName = "/mouthPressRight", Min = 0, Max = 1 },
        new CalibrationItem { ShapeName = "/mouthStretchLeft", Min = 0, Max = 1 },
        new CalibrationItem { ShapeName = "/mouthStretchRight", Min = 0, Max = 1 },
        new CalibrationItem { ShapeName = "/tongueOut", Min = 0, Max = 1 },
        new CalibrationItem { ShapeName = "/tongueUp", Min = 0, Max = 1 },
        new CalibrationItem { ShapeName = "/tongueDown", Min = 0, Max = 1 },
        new CalibrationItem { ShapeName = "/tongueLeft", Min = 0, Max = 1 },
        new CalibrationItem { ShapeName = "/tongueRight", Min = 0, Max = 1 },
        new CalibrationItem { ShapeName = "/tongueRoll", Min = 0, Max = 1 },
        new CalibrationItem { ShapeName = "/tongueBendDown", Min = 0, Max = 1 },
        new CalibrationItem { ShapeName = "/tongueCurlUp", Min = 0, Max = 1 },
        new CalibrationItem { ShapeName = "/tongueSquish", Min = 0, Max = 1 },
        new CalibrationItem { ShapeName = "/tongueFlat", Min = 0, Max = 1 },
        new CalibrationItem { ShapeName = "/tongueTwistLeft", Min = 0, Max = 1 },
        new CalibrationItem { ShapeName = "/tongueTwistRight", Min = 0, Max = 1 }
    ];

    private ILocalSettingsService _settingsService { get; }

    public FaceCalibrationViewModel()
    {
        _settingsService = Ioc.Default.GetService<ILocalSettingsService>()!;
        _settingsService.Load(this);

        PropertyChanged += (_, _) =>
        {
            _settingsService.Save(this);
        };
    }
}
