using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AvaloniaMiaDev.Contracts;
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
    // We probably don't need a queue since we use osc message bundles, but for now, we're keeping it as
    // we might want to allow a way for the user to specify bundle or single message sends in the future
    private static readonly Queue<OscMessage> SendQueue = new();

    private readonly Camera[] _cameras = Enum.GetValues<Camera>();

    private CalibrationItem[] _leftEyeCalibrationItems =
    [
        new CalibrationItem { ShapeName = "/LeftEyeX", Min = -1, Max = 1 },
        new CalibrationItem { ShapeName = "/LeftEyeY", Min = -1, Max = 1 }
    ];

    private CalibrationItem[] _rightEyeCalibrationItems =
    [
        new CalibrationItem { ShapeName = "/RightEyeX", Min = -1, Max = 1 },
        new CalibrationItem { ShapeName = "/RightEyeY", Min = -1, Max = 1 }
    ];

    public static void Enqueue(OscMessage message) => SendQueue.Enqueue(message);
    public static void Clear() => SendQueue.Clear();

    protected async override Task ExecuteAsync(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                for (var index = 0; index < _cameras.Length; index++)
                {
                    var camera = _cameras[index];
                    if (inferenceService.GetExpressionData(camera, out var expressions))
                    {
                        (CalibrationItem calibrationItem, float weight)[] weights = null!;
                        switch (index)
                        {
                            case 0:
                                weights = _leftEyeCalibrationItems.Zip(expressions).ToArray();
                                break;
                            case 1:
                                weights = _rightEyeCalibrationItems.Zip(expressions).ToArray();
                                break;
                            case 2:
                                weights = faceCalibrationViewModel.CalibrationItems.Zip(expressions).ToArray();
                                break;
                        }

                        foreach (var exp in weights)
                        {
                            var message = new OscMessage(exp.calibrationItem.ShapeName!, typeof(float))
                            {
                                Value = Math.Clamp(exp.weight, exp.calibrationItem.Min, exp.calibrationItem.Max)
                            };
                            SendQueue.Enqueue(message);
                        }
                    }

                    await Task.Delay(10, cancellationToken);
                }

                // Send all messages in OSCParams.SendQueue
                if (SendQueue.Count <= 0)
                {
                    continue;
                }

                await sendService.Send(SendQueue.ToArray(), cancellationToken);

                SendQueue.Clear();

                await Task.Delay(10, cancellationToken);
            }
            catch (Exception)
            {
                // ignore!
            }
        }
    }
}
