using System;
using Baballonia.Contracts;
using OpenCvSharp;

namespace Baballonia.Services.Inference;

public interface IProcessingPipeline
{
    float[]? RunUpdate();
}
public class DefaultProcessingPipeline : IProcessingPipeline
{
    public IVideoSource? VideoSource;
    public IImageTransformer? ImageTransformer;
    public IImageConverter? ImageConverter;
    public IInferenceRunner? InferenceService;
    public IFilter? Filter;

    public event Action<Mat> NewFrameEvent;
    public event Action<Mat> TransformedFrameEvent;
    public event Action<float[]> FilteredResultEvent;

    public float[]? RunUpdate()
    {
        var frame = VideoSource?.GetFrame();
        if(frame == null)
            return null;
        NewFrameEvent?.Invoke(frame);

        var transformed = ImageTransformer?.Apply(frame);
        if(transformed == null)
            return null;

        TransformedFrameEvent?.Invoke(transformed);

        var converted = ImageConverter?.Convert(transformed);
        if(converted == null)
            return null;

        var inferenceResult = InferenceService?.Run(converted);
        if(inferenceResult == null)
            return null;

        if(Filter != null)
            inferenceResult = Filter.Filter(inferenceResult);

        FilteredResultEvent?.Invoke(inferenceResult);

        return inferenceResult;
    }
}
