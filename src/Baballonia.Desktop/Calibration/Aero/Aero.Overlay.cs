using System;
using System.Threading.Tasks;
using Baballonia.Desktop.Calibration.Aero.Overlay;
using Baballonia.Models;
using Newtonsoft.Json;

namespace Baballonia.Desktop.Calibration.Aero;

public partial class AeroOverlayTrainerCombo
{
    private async Task<bool> StartOverlay(string[]? arguments = null, string[]? blacklistedPrograms = null, bool waitForExit = false)
    {
        return await StartProcess(Overlay, arguments, blacklistedPrograms, waitForExit);
    }

    public bool StopOverlay()
    {
        return StopProcess(Overlay);
    }

    private async Task<VrCalibrationStatus> GetStatusAsync()
    {
        await StartProcess(Overlay, []);
        var response = await _httpClient.GetStringAsync($"{_baseUrl}/status");
        return JsonConvert.DeserializeObject<VrCalibrationStatus>(response)!;
    }

    public async Task<bool> StartPreviewAsync(VrCalibration calibration)
    {
        var modelPath = Uri.EscapeDataString(calibration.ModelSavePath + VrCalibration.ModelName);
        var url = $"{_baseUrl}/start_preview?model_path={Uri.EscapeDataString(modelPath)}";
        var response = await _httpClient.GetStringAsync(url);
        var result = JsonConvert.DeserializeObject<ApiResponse>(response);
        return result!.Result == "ok";
    }

    public async Task<bool> StopPreviewAsync()
    {
        var response = await _httpClient.GetStringAsync($"{_baseUrl}/stop_preview");
        var result = JsonConvert.DeserializeObject<ApiResponse>(response);
        return result!.Result == "ok";
    }
}
