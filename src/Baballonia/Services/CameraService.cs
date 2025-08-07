using Baballonia.Contracts;
using Baballonia.Services.Inference.Enums;

namespace Baballonia.Services;

public class CameraService
{
    private readonly ILocalSettingsService _localSettingsService;
    private readonly IInferenceService _inferenceService;
    private Camera _camera;


    public CameraService(
        ILocalSettingsService localSettingsService,
        IInferenceService inferenceService,
        Camera camera)
    {
        _localSettingsService = localSettingsService;
        _inferenceService = inferenceService;
        _camera = camera;
    }

    public void StartCamera(string cameraAddress)
    {
        if (!string.IsNullOrEmpty(cameraAddress))
        {
            StopCamera();
            _inferenceService.SetupInference(_camera, cameraAddress);
        }
    }
    public void StopCamera()
    {
        _inferenceService.Shutdown(_camera);
    }
}
