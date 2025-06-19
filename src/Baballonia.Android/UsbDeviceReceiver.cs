using Android.App;
using Android.Content;
using Android.Hardware.Usb;
using Android.Util;
using System;

namespace Baballonia.Android.Receivers;

[BroadcastReceiver(Enabled = true, Exported = true, Label = "USB Device Broadcast Receiver")]
[IntentFilter([UsbManager.ActionUsbDeviceAttached, UsbManager.ActionUsbDeviceDetached])]
public class UsbDeviceReceiver : BroadcastReceiver
{
    public static event Action<UsbDevice, bool> DeviceStateChanged; // device, isAttached

    public override void OnReceive(Context context, Intent intent)
    {
        try
        {
            var device = (UsbDevice)intent.GetParcelableExtra(UsbManager.ExtraDevice);
            
            if (UsbManager.ActionUsbDeviceAttached.Equals(intent.Action))
            {
                Log.Info("UsbDeviceReceiver", $"USB device attached: {device?.DeviceName}");
                DeviceStateChanged?.Invoke(device, true);
            }
            else if (UsbManager.ActionUsbDeviceDetached.Equals(intent.Action))
            {
                Log.Info("UsbDeviceReceiver", $"USB device detached: {device?.DeviceName}");
                DeviceStateChanged?.Invoke(device, false);
            }
        }
        catch (Exception ex)
        {
            Log.Error("UsbDeviceReceiver", $"Error handling USB device event: {ex.Message}");
        }
    }
}
