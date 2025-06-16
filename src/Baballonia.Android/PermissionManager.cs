using Android;
using Android.App;
using Android.Content;
using Android.Content.PM;
using Android.Hardware.Usb;
using Android.Util;
using AndroidX.Core.App;
using AndroidX.Core.Content;
using Baballonia.Android.Receivers;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Baballonia.Android.Services;

public class PermissionManager
{
    private const int PERMISSION_REQUEST_CODE = 1001;
    
    private readonly Activity _activity;
    private readonly UsbManager _usbManager;
    private readonly PendingIntent _permissionIntent;
    private TaskCompletionSource<bool> _permissionTaskSource;

    // All permissions we need to request
    private readonly string[] _requiredPermissions = new[]
    {
        Manifest.Permission.Camera,
        Manifest.Permission.RecordAudio,
        Manifest.Permission.PostNotifications,
        Manifest.Permission.WakeLock,
        Manifest.Permission.ForegroundService,
        Manifest.Permission.Internet,
        Manifest.Permission.AccessWifiState,
        Manifest.Permission.AccessNetworkState,
        Manifest.Permission.ChangeWifiMulticastState,
    };

    public PermissionManager(Activity activity)
    {
        _activity = activity;
        _usbManager = (UsbManager)activity.GetSystemService(Context.UsbService);
        
        // Setup permission intent
        _permissionIntent = PendingIntent.GetBroadcast(
            activity, 
            0, 
            new Intent(UsbPermissionReceiver.ACTION_USB_PERMISSION), 
            PendingIntentFlags.UpdateCurrent | PendingIntentFlags.Immutable);

        // Subscribe to USB permission results
        UsbPermissionReceiver.PermissionResult += OnUsbPermissionResult;
    }

    public void Dispose()
    {
        UsbPermissionReceiver.PermissionResult -= OnUsbPermissionResult;
    }

    public async Task RequestAllPermissionsAsync()
    {
        try
        {
            Log.Info("PermissionManager", "Starting permission requests...");
            
            // Request standard Android permissions (non-blocking)
            RequestStandardPermissions();
            
            // Wait a bit for UI to settle, then request USB permissions
            await Task.Delay(2000);
            await RequestUsbPermissionsAsync();
            
            Log.Info("PermissionManager", "Permission requests initiated");
        }
        catch (Exception ex)
        {
            Log.Error("PermissionManager", $"Error requesting permissions: {ex.Message}");
        }
    }

    private void RequestStandardPermissions()
    {
        try
        {
            var permissionsToRequest = new List<string>();

            // Check which permissions we don't have
            foreach (var permission in _requiredPermissions)
            {
                if (ContextCompat.CheckSelfPermission(_activity, permission) != Permission.Granted)
                {
                    permissionsToRequest.Add(permission);
                }
            }

            if (permissionsToRequest.Any())
            {
                Log.Info("PermissionManager", $"Requesting {permissionsToRequest.Count} permissions");
                
                // Request permissions (this is async by nature)
                ActivityCompat.RequestPermissions(_activity, permissionsToRequest.ToArray(), PERMISSION_REQUEST_CODE);
            }
            else
            {
                Log.Info("PermissionManager", "All standard permissions already granted");
            }
        }
        catch (Exception ex)
        {
            Log.Error("PermissionManager", $"Error requesting standard permissions: {ex.Message}");
        }
    }

    public void HandlePermissionResult(int requestCode, string[] permissions, Permission[] grantResults)
    {
        if (requestCode == PERMISSION_REQUEST_CODE)
        {
            var deniedPermissions = new List<string>();
            
            for (int i = 0; i < permissions.Length; i++)
            {
                if (grantResults[i] == Permission.Granted)
                {
                    Log.Info("PermissionManager", $"Permission granted: {permissions[i]}");
                }
                else
                {
                    Log.Warn("PermissionManager", $"Permission denied: {permissions[i]}");
                    deniedPermissions.Add(permissions[i]);
                }
            }

            if (deniedPermissions.Any())
            {
                // Show explanation dialog for denied permissions
                ShowPermissionExplanationDialog(deniedPermissions);
            }
        }
    }

    private void ShowPermissionExplanationDialog(List<string> deniedPermissions)
    {
        try
        {
            var builder = new AlertDialog.Builder(_activity);
            builder.SetTitle("Permissions Required");
            builder.SetMessage($"This app requires the following permissions to function properly:\n\n" +
                              string.Join("\n", deniedPermissions.Select(GetPermissionDescription)));
            builder.SetPositiveButton("Grant Permissions", (sender, e) =>
            {
                // Re-request permissions
                ActivityCompat.RequestPermissions(_activity, deniedPermissions.ToArray(), PERMISSION_REQUEST_CODE);
            });
            builder.SetNegativeButton("Continue Without", (sender, e) =>
            {
                Log.Warn("PermissionManager", "User chose to continue without some permissions");
            });
            builder.Show();
        }
        catch (Exception ex)
        {
            Log.Error("PermissionManager", $"Error showing permission dialog: {ex.Message}");
        }
    }

    private string GetPermissionDescription(string permission)
    {
        return permission switch
        {
            Manifest.Permission.Camera => "• Camera - Required for video capture",
            Manifest.Permission.RecordAudio => "• Audio Recording - Required for audio capture",
            Manifest.Permission.PostNotifications => "• Notifications - For app status updates",
            _ => $"• {permission}"
        };
    }

    private async Task RequestUsbPermissionsAsync()
    {
        try
        {
            var deviceList = _usbManager.DeviceList;
            var uvcDevices = deviceList.Values.Where(IsUvcDevice).ToList();

            if (!uvcDevices.Any())
            {
                Log.Info("PermissionManager", "No UVC devices found");
                return;
            }

            Log.Info("PermissionManager", $"Found {uvcDevices.Count} UVC device(s)");

            foreach (var device in uvcDevices)
            {
                if (!_usbManager.HasPermission(device))
                {
                    Log.Info("PermissionManager", $"Requesting USB permission for device: {device.DeviceName}");
                    
                    var granted = await RequestPermissionForDeviceAsync(device);
                    if (granted)
                    {
                        Log.Info("PermissionManager", $"USB permission granted for device: {device.DeviceName}");
                    }
                    else
                    {
                        Log.Warn("PermissionManager", $"USB permission denied for device: {device.DeviceName}");
                    }
                }
                else
                {
                    Log.Info("PermissionManager", $"USB permission already granted for device: {device.DeviceName}");
                }
            }
        }
        catch (Exception ex)
        {
            Log.Error("PermissionManager", $"Error requesting USB permissions: {ex.Message}");
        }
    }

    private async Task<bool> RequestPermissionForDeviceAsync(UsbDevice device)
    {
        try
        {
            _permissionTaskSource = new TaskCompletionSource<bool>();
            
            // Request permission
            _usbManager.RequestPermission(device, _permissionIntent);
            
            // Wait for result with timeout
            var timeoutTask = Task.Delay(30000); // 30 second timeout
            var completedTask = await Task.WhenAny(_permissionTaskSource.Task, timeoutTask);
            
            if (completedTask == timeoutTask)
            {
                Log.Error("PermissionManager", "USB permission request timed out");
                return false;
            }
            
            return await _permissionTaskSource.Task;
        }
        catch (Exception ex)
        {
            Log.Error("PermissionManager", $"Error requesting USB permission for device: {ex.Message}");
            return false;
        }
    }

    public async Task HandleNewUsbDeviceAsync(UsbDevice device)
    {
        if (device != null && IsUvcDevice(device))
        {
            Log.Info("PermissionManager", $"UVC device attached: {device.DeviceName}");
            
            // Request permission for newly attached device
            await Task.Delay(500); // Brief delay
            if (!_usbManager.HasPermission(device))
            {
                await RequestPermissionForDeviceAsync(device);
            }
        }
    }

    private void OnUsbPermissionResult(bool granted, UsbDevice device)
    {
        if (_permissionTaskSource != null && !_permissionTaskSource.Task.IsCompleted)
        {
            _permissionTaskSource.SetResult(granted);
        }
    }

    private static bool IsUvcDevice(UsbDevice device)
    {
        // Check if device has Video class interface (class 14)
        for (int i = 0; i < device.InterfaceCount; i++)
        {
            var usbInterface = device.GetInterface(i);
            if (usbInterface.InterfaceClass == UsbClass.Video)
            {
                return true;
            }
        }
        return false;
    }
}
