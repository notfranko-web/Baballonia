using System.Collections.ObjectModel;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using AvaloniaMiaDev.Models;
using AvaloniaMiaDev.ViewModels.SplitViewPane;
using CommunityToolkit.Mvvm.DependencyInjection;

namespace AvaloniaMiaDev.Views;

public partial class FaceCalibrationView : UserControl
{
    private readonly FaceCalibrationViewModel _viewModel;

    public FaceCalibrationView()
    {
        _viewModel = Ioc.Default.GetService<FaceCalibrationViewModel>()!;

        InitializeComponent();

        DataContext = _viewModel;
    }

    public void OnResetMinClicked(object? sender, RoutedEventArgs e)
    {
        foreach (var item in _viewModel.CalibrationItems)
        {
            item.Min = 0.0f;
        }
    }

    public void OnResetMaxClicked(object? sender, RoutedEventArgs e)
    {
        foreach (var item in _viewModel.CalibrationItems)
        {
            item.Max = 1.0f;
        }
    }
}

