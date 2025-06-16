using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Baballonia.Contracts;
using Microsoft.Extensions.Logging;

namespace Baballonia.Desktop.Calibration.Aero;

public partial class AeroOverlayTrainerCombo : IVROverlay, IVRCalibrator, IDisposable
{
    private static string Overlay { get; } = null!;
    private static string Trainer { get; } = null!;
    private static string OverlayPath { get; } = null!;

    private readonly HttpClient _httpClient;
    private readonly string _baseUrl;

    private Dictionary<string, Process> _activeProcesses = new();

    static AeroOverlayTrainerCombo()
    {
        if (OperatingSystem.IsWindows())
        {
            OverlayPath = Path.Combine(AppContext.BaseDirectory, "Calibration", "Windows");
            Overlay = Path.Combine(OverlayPath, "gaze_overlay.exe");
            Trainer = Path.Combine(OverlayPath, "calibration_runner.exe");
        }
        else if (OperatingSystem.IsLinux())
        {
            OverlayPath = Path.Combine(AppContext.BaseDirectory, "Calibration", "Linux");
            Overlay = Path.Combine(OverlayPath, "gaze_overlay");
            Trainer = Path.Combine(OverlayPath, "calibration_runner");
        }
    }

    public AeroOverlayTrainerCombo()
    {
        _baseUrl = "http://localhost:23951";
        _httpClient = new HttpClient();
    }

    private async Task<bool> StartProcess(string program, string[]? arguments = null, string[]? blacklistedPrograms = null, bool waitForExit = false)
    {
        // Make sure program exists
        if (!File.Exists(program))
        {
            return false;
        }

        string processName = Path.GetFileNameWithoutExtension(program);

        // Make sure program isn't already running
        if (Process.GetProcesses().Any(p => p.ProcessName == processName))
        {
            return true; // Already running is considered success
        }

        // Check if any blacklisted programs are running
        if (blacklistedPrograms != null && blacklistedPrograms.Length > 0)
        {
            foreach (var blacklisted in blacklistedPrograms)
            {
                if (Process.GetProcesses().Any(p => p.ProcessName == Path.GetFileNameWithoutExtension(blacklisted)))
                {
                    return false;
                }
            }
        }

        // Check if SteamVR is running. The VR programs need it!
        /*if (!Process.GetProcesses().Any(p => p.ProcessName.ToLower().Contains("vrserver")))
        {
            _logger.LogError("SteamVR is not running. Required for VR operations.");
            return false;
        }*/

        var startInfo = new ProcessStartInfo
        {
            FileName = program,
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true
        };

        if (arguments != null)
            startInfo.Arguments = string.Join(" ", arguments);

        var process = new Process
        {
            StartInfo = startInfo,
            EnableRaisingEvents = true
        };

        process.Exited += (_, _) =>
        {
            _activeProcesses.Remove(processName);
        };

        process.Start();
        process.BeginOutputReadLine();
        process.BeginErrorReadLine();

        _activeProcesses[processName] = process;

        if (waitForExit)
        {
            await process.WaitForExitAsync();
        }

        return true;
    }

    private bool StopProcess(string program)
    {
        string processName = Path.GetFileNameWithoutExtension(program);

        if (_activeProcesses.TryGetValue(processName, out var process))
        {
            try
            {
                if (!process.HasExited)
                {
                    process.Kill();
                    process.WaitForExit(3000); // Wait up to 3 seconds for graceful exit
                }
                process.Dispose();
                _activeProcesses.Remove(processName);
                return true;
            }
            catch (Exception)
            {
                return false;
            }
        }
        else
        {
            // Check if it's running but not tracked by us
            var runningProcess = Process.GetProcesses().FirstOrDefault(p => p.ProcessName == processName);
            if (runningProcess != null)
            {
                try
                {
                    runningProcess.Kill();
                    runningProcess.WaitForExit(3000);
                    runningProcess.Dispose();
                    return true;
                }
                catch (Exception)
                {
                    return false;
                }
            }

            return true; // Not running is considered success for stopping
        }
    }

    private void StopAllProcesses()
    {
        foreach (var process in _activeProcesses.Values.ToList())
        {
            try
            {
                if (!process.HasExited)
                {
                    process.Kill();
                    process.WaitForExit(3000);
                }
                process.Dispose();
            }
            catch (Exception)
            {
                // ignore
            }
        }
        _activeProcesses.Clear();
    }

    public void Dispose()
    {
        StopAllProcesses();
        _httpClient?.Dispose();
    }

    private static void DeleteCaptureFiles(string directoryPath)
    {
        // Validate directory exists
        if (!Directory.Exists(directoryPath))
            return;

        // Get all files matching the capture pattern
        string[] filesToDelete = Directory.GetFiles(directoryPath, "capture.bin");

        // Delete each file
        foreach (string file in filesToDelete)
        {
            File.Delete(file);
        }
    }
}
