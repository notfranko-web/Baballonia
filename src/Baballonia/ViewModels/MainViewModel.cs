using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using Avalonia.Controls;
using Baballonia.Views;
using Baballonia.Models;
using Baballonia.ViewModels.SplitViewPane;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.DependencyInjection;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using Jeek.Avalonia.Localization;

namespace Baballonia.ViewModels;

public partial class MainViewModel : ViewModelBase
{
    public MainViewModel(IMessenger messenger)
    {
        if (OperatingSystem.IsAndroid() || OperatingSystem.IsIOS())
        {
            Items = new ObservableCollection<ListItemTemplate>(_androidTemplates);
        }
        else
        {
            Items = new ObservableCollection<ListItemTemplate>(_desktopTemplates);
        }

        SelectedListItem = Items.First(vm => vm.ModelType == typeof(HomePageViewModel));
    }

    private readonly List<ListItemTemplate> _desktopTemplates =
    [
        new(typeof(HomePageViewModel), "HomeRegular", "Home"),
        new(typeof(CalibrationViewModel), "EditRegular", "Calibration"),
        new(typeof(FirmwareViewModel), "DeveloperBoardRegular", "Firmware"),
        new(typeof(OutputPageViewModel), "TextFirstLineRegular", "Output"),
        new(typeof(AppSettingsViewModel), "SettingsRegular", "Settings"),
    ];

    private readonly List<ListItemTemplate> _androidTemplates =
    [
        new(typeof(HomePageViewModel), "HomeRegular", "Home"),
        new(typeof(CalibrationViewModel), "EditRegular", "Calibration"),
        new(typeof(OutputPageViewModel), "TextFirstLineRegular", "Output"),
        new(typeof(AppSettingsViewModel), "SettingsRegular", "Settings"),
    ];

    public MainViewModel() : this(new WeakReferenceMessenger()) { }

    [ObservableProperty]
    private bool _isPaneOpen;

    [ObservableProperty] private ViewModelBase _currentPage;

    [ObservableProperty]
    private ListItemTemplate? _selectedListItem;

    partial void OnSelectedListItemChanged(ListItemTemplate? value)
    {
        if (value is null) return;

        var vm = Design.IsDesignMode
            ? Activator.CreateInstance(value.ModelType)
            : Ioc.Default.GetService(value.ModelType);

        if (vm is not ViewModelBase vmb) return;

        CurrentPage = vmb;
    }

    public ObservableCollection<ListItemTemplate> Items { get; }

    [RelayCommand]
    private void TriggerPane()
    {
        IsPaneOpen = !IsPaneOpen;
    }
}
