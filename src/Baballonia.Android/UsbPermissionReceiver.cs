using Android.Content;
using Android.Hardware.Usb;
using Android.Util;
using System;

namespace Baballonia.Android.Receivers;

[BroadcastReceiver(Enabled = true, Label = "USB Permission Broadcast Receiver")]
public class UsbPermissionReceiver : BroadcastReceiver
{
    public const string ACTION_USB_PERMISSION = "com.baballonia.USB_PERMISSION";
    
    public static event Action<bool, UsbDevice> PermissionResult;

    public override void OnReceive(Context context, Intent intent)
    {
        try
        {
            if (ACTION_USB_PERMISSION.Equals(intent.Action))
            {
                var device = (UsbDevice)intent.GetParcelableExtra(UsbManager.ExtraDevice);
                bool granted = intent.GetBooleanExtra(UsbManager.ExtraPermissionGranted, false);
                
                Log.Info("UsbPermissionReceiver", $"USB permission result: {granted} for device: {device?.DeviceName}");
                PermissionResult?.Invoke(granted, device);
            }
        }
        catch (Exception ex)
        {
            Log.Error("UsbPermissionReceiver", $"Error processing USB permission result: {ex.Message}");
        }
    }
}