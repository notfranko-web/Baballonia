using System;
using Baballonia.Contracts;
using Baballonia.Services.Inference.Enums;
using OpenCvSharp;

namespace Baballonia.Services.Inference;

public class EyeProcessingPipeline : DefaultProcessingPipeline
{
    private HistogramTranformer histogramTranformer = new();
    private ImageCollector imageCollector = new();

    public float[]? RunUpdate()
    {
        var frame = VideoSource?.GetFrame(ColorType.Gray8);
        if(frame == null)
            return null;

        InvokeNewFrameEvent(frame);

        var transformed = ImageTransformer?.Apply(frame);
        if(transformed == null)
            return null;

        InvokeTransformedFrameEvent(transformed);

        var collected = imageCollector.Apply(transformed);
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

        InvokeFilteredResultEvent(inferenceResult);

        frame.Dispose();
        transformed.Dispose();

        return inferenceResult;
    }
}
