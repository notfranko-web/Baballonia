using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using Avalonia.Controls;
using AvaloniaMiaDev.Models;
using AvaloniaMiaDev.ViewModels.SplitViewPane;
using AvaloniaMiaDev.Views;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.DependencyInjection;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using Jeek.Avalonia.Localization;

namespace AvaloniaMiaDev.ViewModels;

public partial class MainViewModel : ViewModelBase
{
    public MainViewModel(IMessenger messenger)
    {
        Items = new ObservableCollection<ListItemTemplate>(_templates);

        SelectedListItem = Items.First(vm => vm.ModelType == typeof(EyeHomePageViewModel));
    }

    private readonly List<ListItemTemplate> _templates =
    [
        new ListItemTemplate(typeof(EyeHomePageViewModel), "EyeTrackingRegular", "Eye Tracking Home"),
        new ListItemTemplate(typeof(FaceHomeViewModel), "EmojiLaughRegular", "Face Tracking Home"),
        new ListItemTemplate(typeof(EyeCalibrationViewModel), "EditRegular", "Eye Tracking Settings"),
        new ListItemTemplate(typeof(FaceCalibrationViewModel), "EditRegular", "Face Tracking Settings"),
        new ListItemTemplate(typeof(OutputPageViewModel), "TextFirstLineRegular", Localizer.Get("Shell_Output.Content")),
        new ListItemTemplate(typeof(ModuleSettingsViewModel), "HeadsetVrRegular", "VRChat Module Settings"),
        new ListItemTemplate(typeof(AppSettingsViewModel), "SettingsRegular", "App Settings"),
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
