using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AvaloniaMiaDev.Contracts;
using AvaloniaMiaDev.OSC;
using AvaloniaMiaDev.Services.Inference.Enums;
using AvaloniaMiaDev.ViewModels.SplitViewPane;
using Microsoft.Extensions.Hosting;

namespace AvaloniaMiaDev.Services;

public class ParameterSenderService : BackgroundService
{
    // We probably don't need a queue since we use osc message bundles, but for now, we're keeping it as
    // we might want to allow a way for the user to specify bundle or single message sends in the future
    private static readonly Queue<OscMessage> SendQueue = new();

    private readonly Camera[] _cameras = [Camera.Face]; // Enum.GetValues<Camera>();

    private readonly IInferenceService _inferenceService;
    private readonly FaceCalibrationViewModel _faceCalibrationViewModel;
    private readonly OscSendService _sendService;

    public ParameterSenderService(
        IInferenceService inferenceService,
        OscSendService sendService,
        FaceCalibrationViewModel faceCalibrationViewModel)
    {
        _inferenceService = inferenceService;
        _sendService = sendService;
        _faceCalibrationViewModel = faceCalibrationViewModel;
    }

    public static void Enqueue(OscMessage message) => SendQueue.Enqueue(message);
    public static void Clear() => SendQueue.Clear();

    protected async override Task ExecuteAsync(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                foreach (var camera in _cameras)
                {
                    // TODO: Switch string expressions on camera type
                    // Right now this just sends lower mouth information!
                    if (_inferenceService.GetExpressionData(camera, out var arKitExpressions))
                    {
                        var expressions = _faceCalibrationViewModel.CalibrationItems.Zip(arKitExpressions);

                        foreach (var exp in expressions)
                        {
                            var message = new OscMessage(exp.First.ShapeName!, typeof(float))
                            {
                                Value = Math.Clamp(exp.Second, exp.First.Min, exp.First.Max)
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

                await _sendService.Send(SendQueue.ToArray(), cancellationToken);

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
