using System;
using System.Threading.Tasks;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using Baballonia.Contracts;
using Baballonia.Services.Inference;
using CommunityToolkit.Mvvm.DependencyInjection;
using HarfBuzzSharp;
using Microsoft.Extensions.Logging;
using OpenCvSharp;

namespace Baballonia.Services;

public class ProcessingLoopService : IDisposable
{

    public record struct Expressions(float[]? FaceExpression, float[]? EyeExpression);

    public event Action<Expressions> ExpressionChangeEvent;

    public readonly DefaultProcessingPipeline FaceProcessingPipeline = new();
    public readonly DefaultProcessingPipeline EyesProcessingPipeline = new();

    private readonly ILocalSettingsService _localSettingsService;

    public event Action<Exception> PipelineExceptionEvent;

    private readonly DispatcherTimer _drawTimer = new()
    {
        Interval = TimeSpan.FromMilliseconds(10)
    };

    private readonly ILogger<ProcessingLoopService> _logger;

    public ProcessingLoopService(
        ILogger<ProcessingLoopService> logger,
        ILocalSettingsService localSettingsService)
    {
        _localSettingsService = localSettingsService;
        _logger = logger;

        FaceProcessingPipeline.ImageConverter = new MatToFloatTensorConverter();
        FaceProcessingPipeline.ImageTransformer = new ImageTransformer();
        EyesProcessingPipeline.ImageConverter = new MatToFloatTensorConverter();
        EyesProcessingPipeline.ImageTransformer = new DualImageTransformer();
        Setup();

        _drawTimer.Tick += TimerEvent;
        _drawTimer.Start();
    }

    private void Setup(bool useGpu = false)
    {
        Task.Run(() =>
        {
            var l = Ioc.Default.GetService<ILogger<DefaultInferenceRunner>>()!;
            var faceInference = new DefaultInferenceRunner(l);
            faceInference.Setup("faceModel.onnx", useGpu);

            var eyeInference = new DefaultInferenceRunner(l);
            eyeInference.Setup("eyeModel.onnx", useGpu);

            Dispatcher.UIThread.Post(() =>
            {
                FaceProcessingPipeline.InferenceService = faceInference;
                EyesProcessingPipeline.InferenceService = eyeInference;
            });
        });
    }

    private void TimerEvent(object? s, EventArgs e)
    {
        var expressions = new Expressions();

        try
        {
            var faceExpression = FaceProcessingPipeline.RunUpdate();
            if (faceExpression != null)
                expressions.FaceExpression = faceExpression;
        }
        catch (Exception ex)
        {
            _logger.LogError("Unexpected exception in Face Tracking pipeline stopping... : {}", ex);
            FaceProcessingPipeline.VideoSource?.Dispose();
            FaceProcessingPipeline.VideoSource = null;
            PipelineExceptionEvent.Invoke(ex);
        }

        try
        {
            var eyeExpression = EyesProcessingPipeline.RunUpdate();
            if (eyeExpression != null)
                expressions.EyeExpression = eyeExpression;
        }
        catch (Exception ex)
        {
            _logger.LogError("Unexpected exception, in Eye Tracking pipeline stopping: {}", ex);
            EyesProcessingPipeline.VideoSource?.Dispose();
            EyesProcessingPipeline.VideoSource = null;
            PipelineExceptionEvent.Invoke(ex);
        }

        if (expressions.FaceExpression != null || expressions.EyeExpression != null)
            ExpressionChangeEvent?.Invoke(expressions);
    }

    public void Start()
    {
        _drawTimer.Start();
    }

    public void Pause()
    {
        _drawTimer.Stop();
    }

    public void Dispose()
    {
        _drawTimer.Stop();
    }
}
