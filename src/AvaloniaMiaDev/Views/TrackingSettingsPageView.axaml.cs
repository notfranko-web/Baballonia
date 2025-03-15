using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using AvaloniaMiaDev.Models;
using AvaloniaMiaDev.ViewModels.SplitViewPane;

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
        var module = button!.DataContext as TrackingAlgorithm;

        if (module != null)
        {
            module.Order--;
        }
    }

    private void IncrementOrder(object sender, RoutedEventArgs e)
    {
        var button = sender as Button;

        if (button!.DataContext is TrackingAlgorithm module)
        {
            module.Order++;
        }
    }

    private void OnDetachedFromVisualTree(object sender, VisualTreeAttachmentEventArgs e)
    {
        var viewModel = DataContext as TrackingSettingsPageViewModel;
        viewModel!.DetachedFromVisualTree();
    }
}

