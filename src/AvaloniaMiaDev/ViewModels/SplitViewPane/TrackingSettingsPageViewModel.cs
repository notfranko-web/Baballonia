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

public partial class TrackingSettingsPageViewModel : ViewModelBase
{
    [ObservableProperty]
    private ObservableCollection<TrackingAlgorithm> trackingAlgorithms;

    [ObservableProperty]
    [property: SavedSetting("TrackingSettings_OneEuro", 0.0004f)]
    private float _oneEuroMinFreqCutoff;

    [ObservableProperty]
    [property: SavedSetting("TrackingSettings_OneEuro", 0.9f)]
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
    private float _leftHSFRadius;

    [ObservableProperty]
    [property: SavedSetting("AdvancedControls_RightHSFRadius", 10f)]
    private float _rightHSFRadius;

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

    public ILocalSettingsService SettingsService { get; private set;}

    public TrackingSettingsPageViewModel()
    {
        trackingAlgorithms =
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
        trackingAlgorithms.CollectionChanged += OnTrackingAlgorithmCollectionChanged;

        SettingsService = Ioc.Default.GetService<ILocalSettingsService>()!;
        SettingsService.Load(this);

        PropertyChanged += (_, _) =>
        {
            SettingsService.Save(this);
        };
    }

    private async void OnTrackingAlgorithmCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        // TrackingAlgorithms.CollectionChanged -= OnTrackingAlgorithmCollectionChanged;

        RenumberModules();

        // try
        // {
        //     var _installedModules = TrackingAlgorithms.ToList(); // Create a copy to avoid modification during save
        // }
        // finally
        // {
        //     // Re-enable the event handler
        //     TrackingAlgorithms.CollectionChanged += OnTrackingAlgorithmCollectionChanged;
        // }
    }

    private async void OnLocalModulePropertyChanged(object sender, PropertyChangedEventArgs e)
    {
        if (sender is not TrackingAlgorithm module)
            return;

        var desiredIndex = module.Order;
        var currentIndex = trackingAlgorithms.IndexOf(module);

        if (desiredIndex >= 0 && desiredIndex < trackingAlgorithms.Count)
            trackingAlgorithms.Move(currentIndex, desiredIndex);

        RenumberModules();
    }

    private void RenumberModules()
    {
        for (int i = 0; i < trackingAlgorithms.Count; i++)
        {
            trackingAlgorithms[i].Order = i;
        }
    }

    public void DetachedFromVisualTree()
    {

    }
}
