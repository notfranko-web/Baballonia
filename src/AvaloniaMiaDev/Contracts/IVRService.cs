using AvaloniaMiaDev.Services;
using System.Threading.Tasks;
using AvaloniaMiaDev.Models;

namespace AvaloniaMiaDev.Contracts;

internal interface IVRService
{
    public Task<VRCalibrationStatus> GetStatusAsync();

    public Task<bool> StartCamerasAsync(VRCalibration calibration);

    public Task<bool> StartCalibrationAsync(VRCalibration calibration);

    public Task<bool> StartPreviewAsync(VRCalibration calibration);

    public Task<bool> StopPreviewAsync();
}
