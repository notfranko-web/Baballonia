using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Baballonia.Contracts;
using Baballonia.Helpers;
using Baballonia.Models;
using Baballonia.Services.Inference;
using Baballonia.ViewModels.SplitViewPane;
using Microsoft.Extensions.Hosting;
using OscCore;

namespace Baballonia.Services;

public class ParameterSenderService(
    OscSendService sendService,
    ILocalSettingsService localSettingsService,
    CalibrationViewModel calibrationViewModel) : BackgroundService
{
    public void Enqueue(OscMessage message) => _sendQueue.Enqueue(message);
    public void Clear() => _sendQueue.Clear();

    public void RegisterLeftCameraController(CameraController controller) => _leftCameraController = controller;
    public void RegisterRightCameraController(CameraController controller) => _rightCameraController = controller;
    public void RegisterFaceCameraController(CameraController controller) => _faceCameraController = controller;

    private readonly Queue<OscMessage> _sendQueue = new();

    // Camera controller references
    private CameraController _leftCameraController;
    private CameraController _rightCameraController;
    private CameraController _faceCameraController;

    // Cache for expression settings to avoid repeated async calls
    private readonly Dictionary<string, (float Lower, float Upper)> _expressionSettings = new();
    private readonly object _settingsLock = new();

    // Expression parameter names
    private readonly string[] EyeExpressionNames =
    [
        "/LeftEyeX",
        "/LeftEyeY",
        "/RightEyeX",
        "/RightEyeY",
        "/LeftEyeLid",
        "/RightEyeLid",
        "/BrowRaise",
        "/BrowAngry",
        "/EyeWiden",
        "/EyeSquint",
        "/EyeDilate"
    ];

    // Define all expression parameter names
    private readonly string[] FaceExpressionNames =
    {
        "/cheekPuffLeft",
        "/cheekPuffRight",
        "/cheekSuckLeft",
        "/cheekSuckRight",
        "/jawOpen",
        "/jawForward",
        "/jawLeft",
        "/jawRight",
        "/noseSneerLeft",
        "/noseSneerRight",
        "/mouthFunnel",
        "/mouthPucker",
        "/mouthLeft",
        "/mouthRight",
        "/mouthRollUpper",
        "/mouthRollLower",
        "/mouthShrugUpper",
        "/mouthShrugLower",
        "/mouthClose",
        "/mouthSmileLeft",
        "/mouthSmileRight",
        "/mouthFrownLeft",
        "/mouthFrownRight",
        "/mouthDimpleLeft",
        "/mouthDimpleRight",
        "/mouthUpperUpLeft",
        "/mouthUpperUpRight",
        "/mouthLowerDownLeft",
        "/mouthLowerDownRight",
        "/mouthPressLeft",
        "/mouthPressRight",
        "/mouthStretchLeft",
        "/mouthStretchRight",
        "/tongueOut",
        "/tongueUp",
        "/tongueDown",
        "/tongueLeft",
        "/tongueRight",
        "/tongueRoll",
        "/tongueBendDown",
        "/tongueCurlUp",
        "/tongueSquish",
        "/tongueFlat",
        "/tongueTwistLeft",
        "/tongueTwistRight"
    };

    // Mapping from parameter names to setting keys
    private readonly Dictionary<string, string> _parameterToSettingMap = new()
    {
        { "/LeftEyeX", "LeftEyeX" },
        { "/LeftEyeY", "LeftEyeY" },
        { "/RightEyeX", "RightEyeX" },
        { "/RightEyeY", "RightEyeY" },
        { "/LeftEyeLid", "LeftEyeLid" },
        { "/RightEyeLid", "RightEyeLid" },
        { "/BrowRaise", "BrowRaise" },
        { "/BrowAngry", "BrowAngry" },
        { "/EyeWiden", "EyeWiden" },
        { "/EyeSquint", "EyeSquint" },
        { "/EyeDilate", "EyeDilate" },
        { "/cheekPuffLeft", "CheekPuffLeft" },
        { "/cheekPuffRight", "CheekPuffRight" },
        { "/cheekSuckLeft", "CheekSuckLeft" },
        { "/cheekSuckRight", "CheekSuckRight" },
        { "/jawOpen", "JawOpen" },
        { "/jawForward", "JawForward" },
        { "/jawLeft", "JawLeft" },
        { "/jawRight", "JawRight" },
        { "/noseSneerLeft", "NoseSneerLeft" },
        { "/noseSneerRight", "NoseSneerRight" },
        { "/mouthFunnel", "MouthFunnel" },
        { "/mouthPucker", "MouthPucker" },
        { "/mouthLeft", "MouthLeft" },
        { "/mouthRight", "MouthRight" },
        { "/mouthRollUpper", "MouthRollUpper" },
        { "/mouthRollLower", "MouthRollLower" },
        { "/mouthShrugUpper", "MouthShrugUpper" },
        { "/mouthShrugLower", "MouthShrugLower" },
        { "/mouthClose", "MouthClose" },
        { "/mouthSmileLeft", "MouthSmileLeft" },
        { "/mouthSmileRight", "MouthSmileRight" },
        { "/mouthFrownLeft", "MouthFrownLeft" },
        { "/mouthFrownRight", "MouthFrownRight" },
        { "/mouthDimpleLeft", "MouthDimpleLeft" },
        { "/mouthDimpleRight", "MouthDimpleRight" },
        { "/mouthUpperUpLeft", "MouthUpperUpLeft" },
        { "/mouthUpperUpRight", "MouthUpperUpRight" },
        { "/mouthLowerDownLeft", "MouthLowerDownLeft" },
        { "/mouthLowerDownRight", "MouthLowerDownRight" },
        { "/mouthPressLeft", "MouthPressLeft" },
        { "/mouthPressRight", "MouthPressRight" },
        { "/mouthStretchLeft", "MouthStretchLeft" },
        { "/mouthStretchRight", "MouthStretchRight" },
        { "/tongueOut", "TongueOut" },
        { "/tongueUp", "TongueUp" },
        { "/tongueDown", "TongueDown" },
        { "/tongueLeft", "TongueLeft" },
        { "/tongueRight", "TongueRight" },
        { "/tongueRoll", "TongueRoll" },
        { "/tongueBendDown", "TongueBendDown" },
        { "/tongueCurlUp", "TongueCurlUp" },
        { "/tongueSquish", "TongueSquish" },
        { "/tongueFlat", "TongueFlat" },
        { "/tongueTwistLeft", "TongueTwistLeft" },
        { "/tongueTwistRight", "TongueTwistRight" }
    };

    protected override async Task ExecuteAsync(CancellationToken cancellationToken)
    {
        // Subscribe to property changes
        CalibrationViewModel.ExpressionUpdated += OnFaceCalibrationPropertyChanged;

        // Load initial settings
        await LoadInitialSettings();

        try
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                var prefix = await localSettingsService.ReadSettingAsync<string>("AppSettings_OSCPrefix");

                try
                {
                    if (_leftCameraController != null) ProcessEyeExpressionData(_leftCameraController.ArExpressions, prefix);
                    if (_faceCameraController != null) ProcessFaceExpressionData(_faceCameraController.ArExpressions, prefix);

                    await SendAndClearQueue(cancellationToken);
                    await Task.Delay(10, cancellationToken);
                }
                catch (Exception)
                {
                    // ignore!
                }
            }
        }
        finally
        {
            // Unsubscribe when done
            CalibrationViewModel.ExpressionUpdated -= OnFaceCalibrationPropertyChanged;
        }
    }

    private async Task LoadInitialSettings()
    {
        // Load all initial settings into cache
        var allParameterNames = EyeExpressionNames.Concat(FaceExpressionNames);

        foreach (var parameterName in allParameterNames)
        {
            if (_parameterToSettingMap.TryGetValue(parameterName, out var settingKey))
            {
                var lower = await localSettingsService.ReadSettingAsync<float>($"{settingKey}Lower");
                var upper = await localSettingsService.ReadSettingAsync<float>($"{settingKey}Upper");

                lock (_settingsLock)
                {
                    _expressionSettings[parameterName] = (lower, upper);
                }
            }
        }
    }

    private void OnFaceCalibrationPropertyChanged(string expression)
    {
        if (string.IsNullOrEmpty(expression))
            return;

        // Parse property name to extract parameter and bound type
        // Assuming property names follow pattern like "LeftEyeXLower" or "LeftEyeXUpper"
        string parameterKey = string.Empty;
        bool isUpper = false;

        if (expression.EndsWith("Lower"))
        {
            parameterKey = expression.Substring(0, expression.Length - 5); // Remove "Lower"
            isUpper = false;
        }
        else if (expression.EndsWith("Upper"))
        {
            parameterKey = expression.Substring(0, expression.Length - 5); // Remove "Upper"
            isUpper = true;
        }

        if (parameterKey == null)
            return;

        // Find the corresponding parameter name
        var parameterName = _parameterToSettingMap.FirstOrDefault(kvp => kvp.Value == parameterKey).Key;
        if (parameterName == null)
            return;

        // Get the new value from the view model
        var newValue = GetPropertyValue(calibrationViewModel, expression);
        if (newValue is float floatValue)
        {
            lock (_settingsLock)
            {
                if (_expressionSettings.TryGetValue(parameterName, out var currentSettings))
                {
                    if (isUpper)
                    {
                        _expressionSettings[parameterName] = (currentSettings.Lower, floatValue);
                    }
                    else
                    {
                        _expressionSettings[parameterName] = (floatValue, currentSettings.Upper);
                    }
                }
                else
                {
                    // If not found, create new entry with default for the other bound
                    if (isUpper)
                    {
                        _expressionSettings[parameterName] = (0f, floatValue);
                    }
                    else
                    {
                        _expressionSettings[parameterName] = (floatValue, 1f);
                    }
                }
            }
        }
    }

    private object GetPropertyValue(object obj, string propertyName)
    {
        var propertyInfo = obj.GetType().GetProperty(propertyName);
        return propertyInfo?.GetValue(obj)!;
    }

    private (float Lower, float Upper) GetExpressionSettings(string parameterName)
    {
        lock (_settingsLock)
        {
            return _expressionSettings.TryGetValue(parameterName, out var settings) ? settings : (0f, 1f);
        }
    }

    private void ProcessEyeExpressionData(float[] expressions, string prefix = "")
    {
        if (expressions is null) return;
        if (expressions.Length == 0) return;

        // Process each expression and create OSC messages
        for (int i = 0; i < Math.Min(expressions.Length, EyeExpressionNames.Length); i++)
        {
            var weight = expressions[i];
            var settings = GetExpressionSettings(EyeExpressionNames[i]);

            var msg = new OscMessage(prefix + EyeExpressionNames[i],
                Math.Clamp(
                    weight.Remap(settings.Lower, settings.Upper),
                    0,
                    1));
            _sendQueue.Enqueue(msg);
        }
    }

    private void ProcessFaceExpressionData(float[] expressions, string prefix = "")
    {
        if (expressions is null) return;
        if (expressions.Length == 0) return;

        // Process each expression and create OSC messages
        for (int i = 0; i < Math.Min(expressions.Length, FaceExpressionNames.Length); i++)
        {
            var weight = expressions[i];
            var settings = GetExpressionSettings(FaceExpressionNames[i]);

            var msg = new OscMessage(prefix + FaceExpressionNames[i],
                Math.Clamp(
                    weight.Remap(settings.Lower, settings.Upper),
                    0,
                    1));
            _sendQueue.Enqueue(msg);
        }
    }

    private async Task SendAndClearQueue(CancellationToken cancellationToken)
    {
        if (_sendQueue.Count == 0)
            return;

        await sendService.Send(_sendQueue.ToArray(), cancellationToken);
        _sendQueue.Clear();
    }
}
