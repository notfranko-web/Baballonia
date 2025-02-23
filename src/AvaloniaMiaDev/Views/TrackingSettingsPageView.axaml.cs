using System.IO;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Styling;
using Avalonia.Threading;
using AvaloniaMiaDev.Contracts;
using AvaloniaMiaDev.Models;
using AvaloniaMiaDev.Services;
using AvaloniaMiaDev.ViewModels.SplitViewPane;
using CommunityToolkit.Mvvm.DependencyInjection;
using Jeek.Avalonia.Localization;

namespace AvaloniaMiaDev.Views;

public partial class TrackingSettingsPageView : UserControl
{
    public TrackingSettingsPageView()
    {
        InitializeComponent();
        DetachedFromVisualTree += OnDetachedFromVisualTree;
    }

    private void OnLocalModuleSelected(object? sender, SelectionChangedEventArgs e)
    {

    }

    private void DecrementOrder(object sender, RoutedEventArgs e)
    {
        var button = sender as Button;
        var module = button.DataContext as TrackingAlgorithm;

        if (module != null)
        {
            module.Order--;
        }
    }

    private void IncrementOrder(object sender, RoutedEventArgs e)
    {
        var button = sender as Button;
        var module = button.DataContext as TrackingAlgorithm;

        if (module != null)
        {
            module.Order++;
        }
    }

    private void OnDetachedFromVisualTree(object sender, VisualTreeAttachmentEventArgs e)
    {
        var viewModel = DataContext as TrackingSettingsPageViewModel;
        viewModel.DetachedFromVisualTree();
    }
}

