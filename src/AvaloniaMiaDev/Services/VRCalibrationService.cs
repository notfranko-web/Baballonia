// VRCalibrationService.cs
using System;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using AvaloniaMiaDev.Contracts;
using AvaloniaMiaDev.Models;
using Newtonsoft.Json;

namespace AvaloniaMiaDev.Services
{
    public class VRCalibrationService : IVRService
    {
        private readonly HttpClient _httpClient;
        private readonly string _baseUrl;

        public VRCalibrationService(string baseUrl = "http://localhost:23951")
        {
            _baseUrl = baseUrl;
            _httpClient = new HttpClient();
        }

        public async Task<VRCalibrationStatus> GetStatusAsync()
        {
            var response = await _httpClient.GetStringAsync($"{_baseUrl}/status");
            return JsonConvert.DeserializeObject<VRCalibrationStatus>(response)!;
        }

        public async Task<bool> StartCamerasAsync()
        {
            var response = await _httpClient.GetStringAsync($"{_baseUrl}/start_cameras");
            var result = JsonConvert.DeserializeObject<ApiResponse>(response);
            return result!.Result == "ok";
        }

        public async Task<bool> StartCalibrationAsync(string outputModelPath, int routineId)
        {
            var url = $"{_baseUrl}/start_calibration?onnx_filename={Uri.EscapeDataString(outputModelPath)}&routine_id={routineId}";
            var response = await _httpClient.GetStringAsync(url);
            var result = JsonConvert.DeserializeObject<ApiResponse>(response);
            return result!.Result == "ok";
        }

        public async Task<bool> StartPreviewAsync(string modelPath)
        {
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

    public class ApiResponse
    {
        [JsonProperty("result")]
        public string Result { get; set; }

        [JsonProperty("message")]
        public string Message { get; set; }
    }

    public class VRCalibrationStatus
    {
        [JsonProperty("running")]
        public string Running { get; set; }

        [JsonProperty("recording")]
        public string Recording { get; set; }

        [JsonProperty("calibrationComplete")]
        public string CalibrationComplete { get; set; }

        [JsonProperty("currentIndex")]
        public int CurrentIndex { get; set; }

        [JsonProperty("maxIndex")]
        public int MaxIndex { get; set; }

        public bool IsRunning => Running == "1";
        public bool IsRecording => Recording == "1";
        public bool IsCalibrationComplete => CalibrationComplete == "1";
        public double Progress => MaxIndex > 0 ? (double)CurrentIndex / MaxIndex : 0;
    }
}
