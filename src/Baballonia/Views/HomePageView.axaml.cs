using System;
using Avalonia.Controls;
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
                var window = this.GetVisualRoot() as Window;
                if (window != null)
                {
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
                }
            };
        }
        Loaded += (s, e) =>
        {
            if (this.DataContext is HomePageViewModel vm)
            {
                SetupCropEvents(vm.LeftCamera, LeftMouthWindow);
                SetupCropEvents(vm.RightCamera, RightMouthWindow);
                SetupCropEvents(vm.FaceCamera, FaceMouthWindow);
            }
        };
    }

    private void SetupCropEvents(HomePageViewModel.CameraControllerModel model, Image image)
    {
        var vm = this.DataContext as HomePageViewModel;
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

}
