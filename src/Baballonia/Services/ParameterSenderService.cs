using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Baballonia.Contracts;
using Baballonia.Helpers;
using Baballonia.Services.Inference;
using Microsoft.Extensions.Hosting;
using OscCore;

namespace Baballonia.Services;

public class ParameterSenderService(
    OscSendService sendService,
    ILocalSettingsService localSettingsService,
    ICalibrationService calibrationService) : BackgroundService
{
    private readonly Queue<OscMessage> _sendQueue = new();

    // Expression parameter names
    private readonly Dictionary<string, string> _eyeExpressionMap = new()
    {
        { "/LeftEyeX", "/LeftEyeX" },
        { "/LeftEyeY", "/LeftEyeY" },
        { "/RightEyeX", "/RightEyeX" },
        { "/RightEyeY", "/RightEyeY" },
        { "/LeftEyeLid", "/LeftEyeLid" },
        { "/RightEyeLid", "/RightEyeLid" },
        { "/BrowRaise", "/BrowRaise" },
        { "/BrowAngry", "/BrowAngry" },
        { "/EyeWiden", "/EyeWiden" },
        { "/EyeSquint", "/EyeSquint" },
        { "/EyeDilate", "/EyeDilate" },
    };

    private readonly Dictionary<string, string> _faceExpressionMap = new()
    {
        { "CheekPuffLeft", "/cheekPuffLeft" },
        { "CheekPuffRight", "/cheekPuffRight" },
        { "CheekSuckLeft", "/cheekSuckLeft" },
        { "CheekSuckRight", "/cheekSuckRight" },
        { "JawOpen", "/jawOpen" },
        { "JawForward", "/jawForward" },
        { "JawLeft", "/jawLeft" },
        { "JawRight", "/jawRight" },
        { "NoseSneerLeft", "/noseSneerLeft" },
        { "NoseSneerRight", "/noseSneerRight" },
        { "MouthFunnel", "/mouthFunnel" },
        { "MouthPucker", "/mouthPucker" },
        { "MouthLeft", "/mouthLeft" },
        { "MouthRight", "/mouthRight" },
        { "MouthRollUpper", "/mouthRollUpper" },
        { "MouthRollLower", "/mouthRollLower" },
        { "MouthShrugUpper", "/mouthShrugUpper" },
        { "MouthShrugLower", "/mouthShrugLower" },
        { "MouthClose", "/mouthClose" },
        { "MouthSmileLeft", "/mouthSmileLeft" },
        { "MouthSmileRight", "/mouthSmileRight" },
        { "MouthFrownLeft", "/mouthFrownLeft" },
        { "MouthFrownRight", "/mouthFrownRight" },
        { "MouthDimpleLeft", "/mouthDimpleLeft" },
        { "MouthDimpleRight", "/mouthDimpleRight" },
        { "MouthUpperUpLeft", "/mouthUpperUpLeft" },
        { "MouthUpperUpRight", "/mouthUpperUpRight" },
        { "MouthLowerDownLeft", "/mouthLowerDownLeft" },
        { "MouthLowerDownRight", "/mouthLowerDownRight" },
        { "MouthPressLeft", "/mouthPressLeft" },
        { "MouthPressRight", "/mouthPressRight" },
        { "MouthStretchLeft", "/mouthStretchLeft" },
        { "MouthStretchRight", "/mouthStretchRight" },
        { "TongueOut", "/tongueOut" },
        { "TongueUp", "/tongueUp" },
        { "TongueDown", "/tongueDown" },
        { "TongueLeft", "/tongueLeft" },
        { "TongueRight", "/tongueRight" },
        { "TongueRoll", "/tongueRoll" },
        { "TongueBendDown", "/tongueBendDown" },
        { "TongueCurlUp", "/tongueCurlUp" },
        { "TongueSquish", "/tongueSquish" },
        { "TongueFlat", "/tongueFlat" },
        { "TongueTwistLeft", "/tongueTwistLeft" },
        { "TongueTwistRight", "/tongueTwistRight" }
    };

    protected override async Task ExecuteAsync(CancellationToken cancellationToken)
    {
        try
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                var prefix = await localSettingsService.ReadSettingAsync<string>("AppSettings_OSCPrefix");

                try
                {
                    ProcessEyeExpressionData(CameraController.EyeExpressions, prefix);
                    ProcessFaceExpressionData(CameraController.FaceExpressions, prefix);

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

        }
    }

    private void ProcessEyeExpressionData(float[] expressions, string prefix = "")
    {
        if (expressions is null) return;
        if (expressions.Length == 0) return;

        // Process each expression and create OSC messages
        for (int i = 0; i < Math.Min(expressions.Length, _eyeExpressionMap.Count); i++)
        {
            var weight = expressions[i];
            var settings = calibrationService.GetExpressionSettings(_eyeExpressionMap.ElementAt(i).Key);

            var msg = new OscMessage(prefix + _eyeExpressionMap.ElementAt(i).Value,
                Math.Clamp(
                    weight.Remap(settings.Lower, settings.Upper),
                    0,
                    1));
            _sendQueue.Enqueue(msg);
        }
    }

    private void ProcessFaceExpressionData(float[] expressions, string prefix = "")
    {
        if (expressions == null) return;
        if (expressions.Length == 0) return;

        // Process each expression and create OSC messages
        for (int i = 0; i < Math.Min(expressions.Length, _faceExpressionMap.Count); i++)
        {
            var weight = expressions[i];
            var settings = calibrationService.GetExpressionSettings(_faceExpressionMap.ElementAt(i).Key);

            var msg = new OscMessage(prefix + _faceExpressionMap.ElementAt(i).Value,
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
