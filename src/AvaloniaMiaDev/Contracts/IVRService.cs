using AvaloniaMiaDev.Services;
using System.Threading.Tasks;

namespace AvaloniaMiaDev.Contracts;

internal interface IVRService
{
    public Task<VRCalibrationStatus> GetStatusAsync();

    public Task<bool> StartCamerasAsync();

    public Task<bool> StartCalibrationAsync(string outputModelPath, int routineId);

    public Task<bool> StartPreviewAsync(string modelPath);

    public Task<bool> StopPreviewAsync();
}
