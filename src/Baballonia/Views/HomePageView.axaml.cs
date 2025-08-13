using System;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.VisualTree;
using Baballonia.Contracts;
using Baballonia.Helpers;
using Baballonia.ViewModels.SplitViewPane;
using CommunityToolkit.Mvvm.DependencyInjection;

namespace Baballonia.Views;

public partial class HomePageView : UserControl
{
    public HomePageView()
    {
        InitializeComponent();

        if (!(OperatingSystem.IsAndroid() || OperatingSystem.IsIOS()))
        {
            SizeChanged += (_, _) =>
            {
                if (this.GetVisualRoot() is not Window window) return;

                var grid = this.FindControl<Grid>("CameraControlsGrid");
                var isMobile = window.ClientSize.Width < Utils.MobileWidth;
                if (isMobile)
                {
                    grid!.ColumnDefinitions = new ColumnDefinitions("*"); // Vertical layout
                    grid.RowDefinitions = new RowDefinitions("*,*,*");
                }
                else
                {
                    grid!.ColumnDefinitions = new ColumnDefinitions("*,*,*"); // Horizontal layout
                    grid.RowDefinitions = new RowDefinitions("*");
                }

                // there is no default control to have equal width cells with automatic cell assignment
                // Uniform grid always has cells of equal width and height
                // and Grid requires children rows and cols position to be specified manually
                // so we use Grid and assign children here
                int columnsCount = isMobile ? 1 : 3;
                int row = 0;
                int col = 0;
                foreach (var child in grid.Children)
                {
                    Grid.SetRow(child, row);
                    Grid.SetColumn(child, col);

                    col++;
                    if (col >= columnsCount)
                    {
                        col = 0;
                        row++;
                    }
                }
            };
        }
        Loaded += (_, _) =>
        {
            if (DataContext is HomePageViewModel vm)
            {
                SetupCropEvents(vm.LeftCamera, LeftMouthWindow);
                SetupCropEvents(vm.RightCamera, RightMouthWindow);
                SetupCropEvents(vm.FaceCamera, FaceWindow);
                EyeAddressEntry_OnTextChanged(null, null!);
                FaceAddressEntry_OnTextChanged(null, null!);

                vm.SelectedCalibrationText = "Full Calibration";
            }
        };
    }

    private void SetupCropEvents(HomePageViewModel.CameraControllerModel model, Image image)
    {
        // in theory should be cleaned up by the GC so no need to manually unsubscribe
        image.PointerPressed += (sender, e) =>
        {
            if (model.Controller.CamViewMode != CamViewMode.Cropping) return;
            var pos = e.GetPosition(image);
            model.Controller.CropManager.StartCrop(pos);
            model.OverlayRectangle = model.Controller.CropManager.CropZone.GetRect();
        };
        image.PointerMoved += (sender, e) =>
        {
            if (model.Controller.CamViewMode != CamViewMode.Cropping) return;

            var pos = e.GetPosition(image);
            model.Controller.CropManager.UpdateCrop(pos);
            model.OverlayRectangle = model.Controller.CropManager.CropZone.GetRect();
        };
        image.PointerReleased += (sender, e) =>
        {
            if (model.Controller.CamViewMode != CamViewMode.Cropping) return;

            model.Controller.CropManager.EndCrop();
            model.OverlayRectangle = model.Controller.CropManager.CropZone.GetRect();
        };
    }

    private void EyeAddressEntry_OnTextChanged(object? sender, TextChangedEventArgs e)
    {
        if (DataContext is not HomePageViewModel vm) return;

        if (vm.LeftCamera.DisplayAddress != null && vm.RightCamera.DisplayAddress != null)
        {
            if (vm.LeftCamera.DisplayAddress.Length == 0)
            {
                LeftAddressHint.Text = "Please enter addresses for both eyes before starting!";
                vm.LeftCamera.HintEnabled = true;
                vm.LeftCamera.InferEnabled = false;
            }

            if (vm.RightCamera.DisplayAddress.Length == 0)
            {
                RightAddressHint.Text = "Please enter addresses for both eyes before starting!";
                vm.RightCamera.HintEnabled = true;
                vm.RightCamera.InferEnabled = false;
            }

            if (vm.LeftCamera.DisplayAddress.Length > 0 && vm.RightCamera.DisplayAddress.Length > 0)
            {
                vm.LeftCamera.HintEnabled = false;
                vm.RightCamera.HintEnabled = false;
                vm.LeftCamera.InferEnabled = true;
                vm.RightCamera.InferEnabled = true;
            }
        }
    }

    private void FaceAddressEntry_OnTextChanged(object? sender, TextChangedEventArgs e)
    {
        if (this.DataContext is not HomePageViewModel vm) return;

        if (vm.FaceCamera.DisplayAddress != null)
        {
            vm.FaceCamera.InferEnabled = vm.FaceCamera.DisplayAddress.Length > 0;
        }
    }

    private void OnCalibrationMenuItemClick(object? sender, RoutedEventArgs e)
    {
        if (sender is MenuItem menuItem && DataContext is HomePageViewModel vm)
        {
            vm.SelectedCalibrationText = menuItem.Header?.ToString() ?? "";
        }
    }
}
