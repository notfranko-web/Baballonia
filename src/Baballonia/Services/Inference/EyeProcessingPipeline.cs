using System;
using Baballonia.Contracts;
using Baballonia.Helpers;
using Baballonia.Services.Inference.Enums;
using OpenCvSharp;
using System.Threading.Tasks;

namespace Baballonia.Services.Inference;

public class EyeProcessingPipeline : DefaultProcessingPipeline
{
    private readonly FastCorruptionDetector _fastCorruptionDetector = new();
    private readonly ImageCollector _imageCollector = new();
    
    // Add a property to control stabilization
    public bool StabilizeEyes { get; set; } = false;

    public EyeProcessingPipeline()
    {
        // The original constructor body is removed as per the edit hint.
        // The LoadEyeStabilizationSetting() call is also removed.
    }

    public float[]? RunUpdate()
    {
        var frame = VideoSource?.GetFrame(ColorType.Gray8);
        if(frame == null)
            return null;

        if (_fastCorruptionDetector.IsCorrupted(frame).isCorrupted)
            return null;

        InvokeNewFrameEvent(frame);

        var transformed = ImageTransformer?.Apply(frame);
        if(transformed == null)
            return null;

        InvokeTransformedFrameEvent(transformed);

        var collected = _imageCollector.Apply(transformed);
        transformed.Dispose();
        if (collected == null)
            return null;

        if (InferenceService == null)
            return null;

        ImageConverter?.Convert(collected, InferenceService.GetInputTensor());

        var inferenceResult = InferenceService?.Run();
        if(inferenceResult == null)
            return null;

        if(Filter != null)
            inferenceResult = Filter.Filter(inferenceResult);

        ProcessExpressions(ref inferenceResult);

        InvokeFilteredResultEvent(inferenceResult);

        frame.Dispose();
        transformed.Dispose();

        return inferenceResult;
    }

    // Keep the method synchronous
    private bool ProcessExpressions(ref float[] arKitExpressions)
    {
        if (arKitExpressions.Length < Utils.EyeRawExpressions)
            return false;

        const float mulV = 2.0f;
        const float mulY = 2.0f;

        var leftPitch = arKitExpressions[0] * mulY - mulY / 2;
        var leftYaw = arKitExpressions[1] * mulV - mulV / 2;
        var leftLid = 1 - arKitExpressions[2];

        var rightPitch = arKitExpressions[3] * mulY - mulY / 2;
        var rightYaw = arKitExpressions[4] * mulV - mulV / 2;
        var rightLid = 1 - arKitExpressions[5];

        // Use the property instead of async call
        if (StabilizeEyes)
        {
            // Calculate convergence before averaging
            // Clamp convergence to never go below 0 (no wall-eyed behavior)
            var rawConvergence = (leftYaw - rightYaw) / 2.0f;
            var convergence = Math.Max(0, rawConvergence);
            
            // Calculate averaged eye positions
            var averagedPitch = (leftPitch + rightPitch) / 2.0f;
            var averagedYaw = (leftYaw + rightYaw) / 2.0f;
            
            // Apply convergence back to the averaged positions
            var leftYawWithConvergence = averagedYaw + convergence;
            var rightYawWithConvergence = averagedYaw - convergence;

            // Update the expressions with stabilized values
            arKitExpressions[0] = averagedPitch;              // left pitch (averaged)
            arKitExpressions[1] = leftYawWithConvergence;     // left yaw (averaged + convergence)
            arKitExpressions[3] = averagedPitch;              // right pitch (averaged)
            arKitExpressions[4] = rightYawWithConvergence;    // right yaw (averaged - convergence)
        }

        var eyeY = (leftPitch * leftLid + rightPitch * rightLid) / (leftLid + rightLid);

        var leftEyeYawCorrected = rightYaw * (1 - leftLid) + leftYaw * leftLid;
        var rightEyeYawCorrected = leftYaw * (1 - rightLid) + rightYaw * rightLid;

        // [left pitch, left yaw, left lid...
        float[] convertedExpressions = new float[Utils.EyeRawExpressions];

        // swap eyes at this point
        convertedExpressions[0] = rightEyeYawCorrected; // left pitch
        convertedExpressions[1] = eyeY;                   // left yaw
        convertedExpressions[2] = rightLid;               // left lid
        convertedExpressions[3] = leftEyeYawCorrected;  // right pitch
        convertedExpressions[4] = eyeY;                   // right yaw
        convertedExpressions[5] = leftLid;                // right lid

        arKitExpressions = convertedExpressions;

        return true;
    }
}
