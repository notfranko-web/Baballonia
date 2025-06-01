using System;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.VisualTree;
using Baballonia.ViewModels.SplitViewPane;
using CommunityToolkit.Mvvm.DependencyInjection;

namespace Baballonia.Views;

public partial class FaceCalibrationView : UserControl
{
    private FaceCalibrationViewModel _viewModel;
    private HomePageView _homeView;

    public FaceCalibrationView()
    {
        InitializeComponent();
        _viewModel = Ioc.Default.GetService<FaceCalibrationViewModel>()!;
        _homeView = Ioc.Default.GetService<HomePageView>()!;

        if (!(OperatingSystem.IsAndroid() || OperatingSystem.IsIOS()))
        {
            SizeChanged += (_, _) =>
            {
                var window = this.GetVisualRoot() as Window;
                if (window != null)
                {
                    var desktopLayout = this.FindControl<StackPanel>("ResetDesktopStackPanel");
                    var mobileLayout = this.FindControl<StackPanel>("ResetMobileStackPanel");
                    if (window.ClientSize.Width < Utils.MobileWidth)
                    {
                        desktopLayout!.IsVisible = false;
                        mobileLayout!.IsVisible = true;
                    }
                    else
                    {
                        desktopLayout!.IsVisible = true;
                        mobileLayout!.IsVisible = false;
                    }
                }
            };
        }
    }

    private void ResetMin(object? sender, RoutedEventArgs e)
    {
        _viewModel.ResetCalibrationValues(FaceCalibrationViewModel.Selection.Min);
    }

    private void ResetMax(object? sender, RoutedEventArgs e)
    {
        _viewModel.ResetCalibrationValues(FaceCalibrationViewModel.Selection.Max);
    }
}

