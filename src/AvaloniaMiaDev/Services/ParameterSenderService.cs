using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AvaloniaMiaDev.Contracts;
using AvaloniaMiaDev.OSC;
using AvaloniaMiaDev.Services.Inference.Enums;
using Microsoft.Extensions.Hosting;

namespace AvaloniaMiaDev.Services;

public class ParameterSenderService : BackgroundService
{
    // We probably don't need a queue since we use osc message bundles, but for now, we're keeping it as
    // we might want to allow a way for the user to specify bundle or single message sends in the future
    private static readonly Queue<OscMessage> SendQueue = new();

    private readonly IInferenceService _inferenceService;
    private readonly OscSendService _sendService;

    private readonly Camera[] _cameras = Enum.GetValues<Camera>();
    private readonly string[] _faceExpressionStrings =
    [
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
    ];

    public ParameterSenderService(IInferenceService inferenceService, OscSendService sendService)
    {
        _inferenceService = inferenceService;
        _sendService = sendService;
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
                        var expressions = _faceExpressionStrings.Zip(arKitExpressions);
                        foreach (var exp in expressions)
                        {
                            var message = new OscMessage(exp.First, typeof(float))
                            {
                                Value = exp.Second
                            };
                            SendQueue.Enqueue(message);

                            await Task.Delay(10, cancellationToken);
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
