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
    [ObservableProperty] public ObservableCollection<TrackingAlgorithm> trackingAlgorithms;

    public TrackingSettingsPageViewModel()
    {
        trackingAlgorithms = new ObservableCollection<TrackingAlgorithm>()
        {
            new TrackingAlgorithm(1, true, "ASHSFRAC", "Description"),
            new TrackingAlgorithm(2, false, "ASHSF", "Description"),
            new TrackingAlgorithm(3, false, "HSRAC", "Description"),
            new TrackingAlgorithm(4, false, "Haar Surround Feature", "Description"),
            new TrackingAlgorithm(5, false, "DADDY", "Description"),
            new TrackingAlgorithm(6, false, "RANSAC 3D", "Description"),
            new TrackingAlgorithm(7, false, "Blob", "Description"),
            new TrackingAlgorithm(8, false, "LEAP", "Description"),
        };
        trackingAlgorithms.CollectionChanged += OnTrackingAlgorithmCollectionChanged;
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
