using System.Collections.ObjectModel;
using AvaloniaMiaDev.Contracts;
using AvaloniaMiaDev.Models;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.DependencyInjection;

namespace AvaloniaMiaDev.ViewModels.SplitViewPane;

public partial class EyeCalibrationViewModel : ViewModelBase
{
    [ObservableProperty]
    [property: SavedSetting("TrackingSettings_Algorithms")]
    private ObservableCollection<TrackingAlgorithm> _trackingAlgorithms;

    [ObservableProperty]
    [property: SavedSetting("TrackingSettings_OneEuroMinFreqCutoff", 0.0004f)]
    private float _oneEuroMinFreqCutoff;

    [ObservableProperty]
    [property: SavedSetting("TrackingSettings_OneEuroSpeedCutoff", 0.9f)]
    private float _oneEuroSpeedCutoff;

    [ObservableProperty]
    [property: SavedSetting("TrackingSettings_OuterEyeFalloff", false)]
    private float _outerEyeFalloff;

    [ObservableProperty]
    [property: SavedSetting("TrackingSettings_EyeDominantIndex", 0)]
    private int _eyeDominantIndex;

    [ObservableProperty]
    [property: SavedSetting("TrackingSettings_EyeDifferenceThreshold", 0.3f)]
    private int _eyeDifferenceThreshold;

    [ObservableProperty]
    [property: SavedSetting("TrackingSettings_BlinkDetectionAlgorithmIndex", 0)]
    private int _blinkDetectionAlgorithmIndex;

    [ObservableProperty]
    [property: SavedSetting("TrackingSettings_LEAPCalibrationSamples", 2000f)]
    private float _leapCalibrationSamples;

    [ObservableProperty]
    [property: SavedSetting("TrackingSettings_IBOFilterSampleSize", 400f)]
    private float _iboFilterSampleSize;

    [ObservableProperty]
    [property: SavedSetting("TrackingSettings_CalibrationSamples", 600f)]
    private float _calibrationSamples;

    [ObservableProperty]
    [property: SavedSetting("TrackingSettings_IBOCloseThreshold", 0.3f)]
    private float _iboCloseThreshold;

    [ObservableProperty]
    [property: SavedSetting("TrackingSettings_EclipseBasedDilation?", false)]
    private bool _eclipseBasedDilation;

    [ObservableProperty]
    [property: SavedSetting("AdvancedControls_SkipAutoRadius", false)]
    private bool _skipAutoRadius;

    [ObservableProperty]
    [property: SavedSetting("AdvancedControls_LeftHSFRadius", 10f)]
    private float _leftHsfRadius;

    [ObservableProperty]
    [property: SavedSetting("AdvancedControls_RightHSFRadius", 10f)]
    private float _rightHsfRadius;

    [ObservableProperty]
    [property: SavedSetting("AdvancedControls_RansacThreshAdd", 11f)]
    private float _ransacThreshAdd;

    [ObservableProperty]
    [property: SavedSetting("AdvancedControls_BlobThreshold", 65f)]
    private float _blobThreshold;

    [ObservableProperty]
    [property: SavedSetting("AdvancedControls_MinBlobSize", 10f)]
    private float _minBlobSize;

    [ObservableProperty]
    [property: SavedSetting("AdvancedControls_MaxBlobSize", 25f)]
    private float _maxBlobSize;

    [ObservableProperty]
    [property: SavedSetting("AdvancedControls_RightEyeThresh", 80f)]
    private float _rightEyeThresh;

    [ObservableProperty]
    [property: SavedSetting("AdvancedControls_LeftEyeThresh", 80f)]
    private float _leftEyeThresh;

    private ILocalSettingsService _settingsService { get; }

    public EyeCalibrationViewModel()
    {
        _settingsService = Ioc.Default.GetService<ILocalSettingsService>()!;
        _settingsService.Load(this);

        _trackingAlgorithms =
        [
            new TrackingAlgorithm(1, true, "ASHSFRAC", "Description"),
            new TrackingAlgorithm(2, false, "ASHSF", "Description"),
            new TrackingAlgorithm(3, false, "HSRAC", "Description"),
            new TrackingAlgorithm(4, false, "Haar Surround Feature", "Description"),
            new TrackingAlgorithm(5, false, "DADDY", "Description"),
            new TrackingAlgorithm(6, false, "RANSAC 3D", "Description"),
            new TrackingAlgorithm(7, false, "Blob", "Description"),
            new TrackingAlgorithm(8, false, "LEAP", "Description")
        ];

        PropertyChanged += (_, _) =>
        {
            _settingsService.Save(this);
        };
    }

    public void MoveModules(int currentIndex, int desiredIndex)
    {
        if (currentIndex < 0) return;

        if (desiredIndex >= 0 && desiredIndex < _trackingAlgorithms.Count)
            _trackingAlgorithms.Move(currentIndex, desiredIndex);

        RenumberModules();
    }

    private void RenumberModules()
    {
        for (int i = 0; i < _trackingAlgorithms.Count; i++)
        {
            _trackingAlgorithms[i].Order = i;
        }
    }
}
