using CommunityToolkit.Mvvm.ComponentModel;
using Baballonia.Models;
using Baballonia.Contracts;
using CommunityToolkit.Mvvm.DependencyInjection;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Threading;
using Baballonia.Helpers;
using Baballonia.Services;
using CommunityToolkit.Mvvm.Input;
using Google.Protobuf.WellKnownTypes;

namespace Baballonia.ViewModels.SplitViewPane;

public partial class CalibrationViewModel : ViewModelBase, IDisposable
{
    public ObservableCollection<SliderBindableSetting> EyeSettings { get; set; }
    public ObservableCollection<SliderBindableSetting> JawSettings { get; set; }
    public ObservableCollection<SliderBindableSetting> MouthSettings { get; set; }
    public ObservableCollection<SliderBindableSetting> TongueSettings { get; set; }
    public ObservableCollection<SliderBindableSetting> NoseSettings { get; set; }
    public ObservableCollection<SliderBindableSetting> CheekSettings { get; set; }

    private ILocalSettingsService _settingsService { get; }
    private readonly ICalibrationService _calibrationService;
    private readonly ParameterSenderService _parameterSenderService;
    private readonly ProcessingLoopService _processingLoopService;

    private readonly Dictionary<string, int> _keyIndexMap;
    public CalibrationViewModel()
    {
        _settingsService = Ioc.Default.GetService<ILocalSettingsService>()!;
        _calibrationService = Ioc.Default.GetService<ICalibrationService>()!;
        _parameterSenderService = Ioc.Default.GetService<ParameterSenderService>()!;
        _processingLoopService = Ioc.Default.GetService<ProcessingLoopService>()!;

        EyeSettings =
        [
            new("LeftEyeX", -1f, -1f),
            new("LeftEyeY", 1f, 1f),
            new("RightEyeX", -1f, -1f),
            new("RightEyeY", 1f, 1f)
        ];

        JawSettings =
        [
            new("JawOpen"),
            new("JawForward"),
            new("JawLeft"),
            new("JawRight")
        ];

        CheekSettings =
        [
            new("CheekPuffLeft", 0f, 1f),
            new("CheekPuffRight", 0f, 1f),
            new("CheekSuckLeft", 0f, 1f),
            new("CheekSuckRight", 0f, 1f)
        ];

        NoseSettings =
        [
            new("NoseSneerLeft", 0f, 1f),
            new("NoseSneerRight", 0f, 1f)
        ];

        MouthSettings =
        [
            new("MouthFunnel", 0f, 1f),
            new("MouthPucker", 0f, 1f),
            new("MouthLeft", 0f, 1f),
            new("MouthRight", 0f, 1f),
            new("MouthRollUpper", 0f, 1f),
            new("MouthRollLower", 0f, 1f),
            new("MouthShrugUpper", 0f, 1f),
            new("MouthShrugLower", 0f, 1f),
            new("MouthClose", 0f, 1f),
            new("MouthSmileLeft", 0f, 1f),
            new("MouthSmileRight", 0f, 1f),
            new("MouthFrownLeft", 0f, 1f),
            new("MouthFrownRight", 0f, 1f),
            new("MouthDimpleLeft", 0f, 1f),
            new("MouthDimpleRight", 0f, 1f),
            new("MouthUpperUpLeft", 0f, 1f),
            new("MouthUpperUpRight", 0f, 1f),
            new("MouthLowerDownLeft", 0f, 1f),
            new("MouthLowerDownRight", 0f, 1f),
            new("MouthPressLeft", 0f, 1f),
            new("MouthPressRight", 0f, 1f),
            new("MouthStretchLeft", 0f, 1f),
            new("MouthStretchRight", 0f, 1f)
        ];

        TongueSettings =
        [
            new("TongueOut", 0f, 1f),
            new("TongueUp", 0f, 1f),
            new("TongueDown", 0f, 1f),
            new("TongueLeft", 0f, 1f),
            new("TongueRight", 0f, 1f),
            new("TongueRoll", 0f, 1f),
            new("TongueBendDown", 0f, 1f),
            new("TongueCurlUp", 0f, 1f),
            new("TongueSquish", 0f, 1f),
            new("TongueFlat", 0f, 1f),
            new("TongueTwistLeft", 0f, 1f),
            new("TongueTwistRight", 0f, 1f)
        ];

        // Convert dictionary order into index mapping
        _keyIndexMap = _parameterSenderService.FaceExpressionMap.Keys
            .Select((key, index) => new { key, index })
            .ToDictionary(x => x.key, x => x.index);

        foreach (var setting in EyeSettings.Concat(JawSettings).Concat(CheekSettings)
                     .Concat(NoseSettings).Concat(MouthSettings).Concat(TongueSettings))
        {
            setting.PropertyChanged += OnSettingChanged;
        }

        PropertyChanged += async (o, p) =>
        {
            var propertyInfo = GetType().GetProperty(p.PropertyName!);
            object value = propertyInfo?.GetValue(this)!;
            if (value is float floatValue)
            {
                if (p.PropertyName == null) return;
                await _calibrationService.SetExpression(p.PropertyName!, floatValue);
            }
        };

        _processingLoopService.ExpressionUpdateEvent += ExpressionUpdateHandler;

        LoadInitialSettings();
    }

    private void ExpressionUpdateHandler(ProcessingLoopService.Expressions expressions)
    {
        if(expressions.FaceExpression != null)
            Dispatcher.UIThread.Post(() =>
            {
                ApplyCurrentFaceExpressionValues(expressions.FaceExpression, CheekSettings);
                ApplyCurrentFaceExpressionValues(expressions.FaceExpression, MouthSettings);
                ApplyCurrentFaceExpressionValues(expressions.FaceExpression, JawSettings);
                ApplyCurrentFaceExpressionValues(expressions.FaceExpression, NoseSettings);
                ApplyCurrentFaceExpressionValues(expressions.FaceExpression, TongueSettings);
            });
        if(expressions.EyeExpression != null)
            Dispatcher.UIThread.Post(() =>
            {
                ApplyCurrentEyeExpressionValues(expressions.EyeExpression, EyeSettings);
            });
    }
    private void OnSettingChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (sender is not SliderBindableSetting setting) return;

        Dispatcher.UIThread.Post(async () =>
        {
            if (e.PropertyName is nameof(SliderBindableSetting.Lower))
            {
                await _calibrationService.SetExpression(setting.Name + "Lower", setting.Lower);
            }

            if (e.PropertyName is nameof(SliderBindableSetting.Upper))
            {
                await _calibrationService.SetExpression(setting.Name + "Upper", setting.Upper);
            }
        });
    }
    public void ApplyCurrentFaceExpressionValues(float[] values, IEnumerable<SliderBindableSetting> settings)
    {
        foreach (var setting in settings)
        {
            if (_keyIndexMap.TryGetValue(setting.Name, out var index)
                && index < values.Length)
            {
                var weight = values[index];
                var val = Math.Clamp(
                    weight.Remap(setting.Lower, setting.Upper),
                    0,
                    1);
                setting.CurrentExpression = val;
            }
        }
    }
    public void ApplyCurrentEyeExpressionValues(float[] values, IEnumerable<SliderBindableSetting> settings)
    {
        foreach (var setting in settings)
        {
            if (_keyIndexMap.TryGetValue(setting.Name, out var index)
                && index < values.Length)
            {
                var weight = values[index];
                var val = Math.Clamp(
                    weight.Remap(setting.Lower, setting.Upper),
                    -1,
                    1);
                setting.CurrentExpression = val;
            }
        }
    }

    [RelayCommand]
    public void ResetMinimums()
    {
        Dispatcher.UIThread.Post(async () =>
        {
            await _calibrationService.ResetMinimums();
            LoadInitialSettings();
        });
    }

    [RelayCommand]
    public void ResetMaximums()
    {
        Dispatcher.UIThread.Post(async () =>
        {
            await _calibrationService.ResetMaximums();
            LoadInitialSettings();
        });
    }

    private void LoadInitialSettings()
    {
        LoadInitialSettings(EyeSettings);
        LoadInitialSettings(CheekSettings);
        LoadInitialSettings(JawSettings);
        LoadInitialSettings(MouthSettings);
        LoadInitialSettings(NoseSettings);
        LoadInitialSettings(TongueSettings);
    }

    private void LoadInitialSettings(IEnumerable<SliderBindableSetting> settings)
    {
        foreach (var setting in settings)
        {
            var val = _calibrationService.GetExpressionSettings(setting.Name);
            setting.Lower = val.Lower;
            setting.Upper = val.Upper;
        }
    }

    public void Dispose()
    {
        _processingLoopService.ExpressionUpdateEvent -= ExpressionUpdateHandler;
    }
}
