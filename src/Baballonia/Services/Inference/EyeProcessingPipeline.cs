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

        // NOW apply stabilization to the converted expressions
        if (StabilizeEyes)
        {
            // Calculate convergence from the converted expressions
            var leftPitchStable = convertedExpressions[0];
            var leftYawStable = convertedExpressions[1];
            var rightPitchStable = convertedExpressions[3];
            var rightYawStable = convertedExpressions[4];

            // Calculate convergence before averaging
            var rawConvergence = (leftYawStable - rightYawStable) / 2.0f;
            var convergence = Math.Max(0, rawConvergence);
            
            // Calculate averaged eye positions
            var averagedPitch = (leftPitchStable + rightPitchStable) / 2.0f;
            var averagedYaw = (leftYawStable + rightYawStable) / 2.0f;
            
            // Apply convergence back to the averaged positions
            var leftYawWithConvergence = averagedYaw + convergence;
            var rightYawWithConvergence = averagedYaw - convergence;

            // Update the converted expressions with stabilized values
            convertedExpressions[0] = averagedPitch;              // left pitch (averaged)
            convertedExpressions[1] = leftYawWithConvergence;     // left yaw (averaged + convergence)
            convertedExpressions[3] = averagedPitch;              // right pitch (averaged)
            convertedExpressions[4] = rightYawWithConvergence;    // right yaw (averaged - convergence)
        }

        arKitExpressions = convertedExpressions;

        return true;
    }
}
