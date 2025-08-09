using System;
using System.Drawing;
using System.IO;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Baballonia.Contracts;
using Baballonia.Helpers;
using Baballonia.Models;
using Baballonia.Services;
using Baballonia.Services.Inference;
using Baballonia.Services.Inference.Enums;
using Baballonia.Services.Inference.Models;
using Baballonia.ViewModels.SplitViewPane;
using CommunityToolkit.Mvvm.DependencyInjection;
using Microsoft.ML.OnnxRuntime;
using Rectangle = Avalonia.Controls.Shapes.Rectangle;
using Size = Avalonia.Size;

namespace Baballonia.Views;

public partial class HomePageView : UserControl
{
    private CameraController LeftCameraController { get; set; }
    private CameraController RightCameraController { get; set; }
    private CameraController FaceCameraController { get; set; }

    private readonly IEyeInferenceService _eyeInferenceService;
    private readonly IFaceInferenceService _faceInferenceService;
    private readonly HomePageViewModel _viewModel;
    private readonly ILocalSettingsService _localSettingsService;

    private readonly DispatcherTimer _drawTimer = new()
    {
        Interval = TimeSpan.FromMilliseconds(10)
    };

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
                    var uniformGrid = this.FindControl<UniformGrid>("UniformGridPanel");
                    if (window.ClientSize.Width < Utils.MobileWidth)
                    {
                        uniformGrid!.Columns = 1; // Vertical layout
                        uniformGrid.Rows = 3;
                    }
                    else
                    {
                        uniformGrid!.Columns = 3; // Horizontal layout
                        uniformGrid.Rows = 1;
                    }
                }
            };
        }


        _viewModel = Ioc.Default.GetRequiredService<HomePageViewModel>()!;
        _localSettingsService = Ioc.Default.GetRequiredService<ILocalSettingsService>()!;
        _eyeInferenceService = Ioc.Default.GetService<IEyeInferenceService>()!;
        _faceInferenceService = Ioc.Default.GetService<IFaceInferenceService>()!;
        _localSettingsService.Load(this);

        Loaded += (s, e) =>
        {
            if (this.DataContext is HomePageViewModel vm)
            {
                SetupCropEvents(vm.LeftCamera, LeftMouthWindow);
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
            model.OverlayRectangle = model.Controller.CropManager.CropZone;
        };
        image.PointerMoved += (sender, e) =>
        {
            if (model.Controller.CamViewMode != CamViewMode.Cropping) return;

            var pos = e.GetPosition(image);
            model.Controller.CropManager.UpdateCrop(pos);
            model.OverlayRectangle = model.Controller.CropManager.CropZone;
        };
        image.PointerReleased += (sender, e) =>
        {
            model.Controller.CropManager.EndCrop();
            model.OverlayRectangle = model.Controller.CropManager.CropZone;
        };
    }

}
