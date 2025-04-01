using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AvaloniaMiaDev.Contracts;
using AvaloniaMiaDev.Helpers;
using AvaloniaMiaDev.Models;
using AvaloniaMiaDev.OSC;
using AvaloniaMiaDev.Services.Inference.Enums;
using AvaloniaMiaDev.ViewModels.SplitViewPane;
using Microsoft.Extensions.Hosting;

namespace AvaloniaMiaDev.Services;

public class ParameterSenderService(
    IInferenceService inferenceService,
    OscSendService sendService,
    FaceCalibrationViewModel faceCalibrationViewModel) : BackgroundService
{
    private readonly Queue<OscMessage> _sendQueue = new();

    private readonly CalibrationItem[] _leftEyeCalibrationItems =
    [
        new CalibrationItem { ShapeName = "/LeftEyeX", Min = -1, Max = 1 },
        new CalibrationItem { ShapeName = "/LeftEyeY", Min = -1, Max = 1 }
    ];

    private readonly CalibrationItem[] _rightEyeCalibrationItems =
    [
        new CalibrationItem { ShapeName = "/RightEyeX", Min = -1, Max = 1 },
        new CalibrationItem { ShapeName = "/RightEyeY", Min = -1, Max = 1 }
    ];

    public void Enqueue(OscMessage message) => _sendQueue.Enqueue(message);
    public void Clear() => _sendQueue.Clear();

    protected async override Task ExecuteAsync(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                ProcessExpressionData(Camera.Left, _leftEyeCalibrationItems);
                ProcessExpressionData(Camera.Right, _rightEyeCalibrationItems);
                ProcessExpressionData(Camera.Face, faceCalibrationViewModel.GetCalibrationValues());

                await SendAndClearQueue(cancellationToken);
                await Task.Delay(10, cancellationToken);
            }
            catch (Exception)
            {
                // ignore!
            }
        }
    }

    private void ProcessExpressionData(Camera camera, Dictionary<string, (double Lower, double Upper)> calibrationItems)
    {
        if (!inferenceService.GetExpressionData(camera, out var expressions))
            return;

        foreach (var (remappedExpression, weight) in calibrationItems.Zip(expressions))
        {
            _sendQueue.Enqueue(new OscMessage(remappedExpression.Key, typeof(float))
            {
                Value = weight.Remap(0, 1, remappedExpression.Value.Lower, remappedExpression.Value.Upper)
            });
        }
    }

    private void ProcessExpressionData(Camera camera, CalibrationItem[] calibrationItems)
    {
        if (!inferenceService.GetExpressionData(camera, out var expressions))
            return;

        foreach (var (remappedExpression, weight) in calibrationItems.Zip(expressions))
        {
            _sendQueue.Enqueue(new OscMessage(remappedExpression.ShapeName!, typeof(float))
            {
                Value = Math.Clamp(weight, remappedExpression.Min, remappedExpression.Max)
            });
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
