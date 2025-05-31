using System;
using System.Collections.Generic;
using Avalonia.Controls;
using Avalonia.Controls.Templates;
using Baballonia.Services.Inference;
using Baballonia.ViewModels;
using Baballonia.ViewModels.SplitViewPane;
using Baballonia.Views;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.DependencyInjection;

namespace Baballonia;

public class ViewLocator : IDataTemplate
{
    private readonly Dictionary<Type, Func<Control?>> _locator = new();

    public ViewLocator()
    {
        RegisterViewFactory<MainViewModel, MainWindow>();
        RegisterViewFactory<HomePageViewModel, HomePageView>();
        RegisterViewFactory<FaceCalibrationViewModel, FaceCalibrationView>();
        RegisterViewFactory<OutputPageViewModel, OutputPageView>();
        RegisterViewFactory<AppSettingsViewModel, AppSettingsView>();

        if (OperatingSystem.IsAndroid() || OperatingSystem.IsIOS()) return;

        RegisterViewFactory<FirmwareViewModel, FirmwareView>();
        RegisterViewFactory<OnboardingViewModel, OnboardingView>();
    }

    public Control Build(object? data)
    {
        if (data is null)
        {
            return new TextBlock { Text = "No VM provided" };
        }

        var type = data.GetType();
        if (type != typeof(HomePageViewModel) || type != typeof(FaceCalibrationViewModel))
            CameraController.HackyImageDisplayBool = false;

        _locator.TryGetValue(data.GetType(), out var factory);

        var control = factory?.Invoke() ?? new TextBlock { Text = $"VM Not Registered: {data.GetType()}" };

        if (type == typeof(HomePageViewModel) || type == typeof(FaceCalibrationViewModel))
            CameraController.HackyImageDisplayBool = true;

        return control;
    }

    public bool Match(object? data)
    {
        return data is ObservableObject;
    }

    private void RegisterViewFactory<TViewModel, TView>()
        where TViewModel : class
        where TView : Control
        => _locator.Add(
            typeof(TViewModel),
            Design.IsDesignMode
                ? Activator.CreateInstance<TView>
                : Ioc.Default.GetService<TView>);
}
