using System;
using System.Collections.Generic;

namespace AvaloniaMiaDev.Models;

/*
 * 0, 1, 2 - Corresponds to a numerical device entry
 * COMX - Corresponds to a serial port on Windows
 * /dev/ttyAVMX - Corresponds to a serial port on Windows
 * http - Corresponds to a IP cam (MJPEG)
 * /dev/videoX - Used for VFT on Linux
 */

public class VRCalibration
{
    public const string LeftModelName = "leftEyeModel.onnx";
    public const string RightModelName = "rightEyeModel.onnx";
    public string ModelSavePath { get; set; }
    public uint[] CalibrationInstructions { get; set; }
    public float FOV { get; set; }
    public EyeInformation LeftEye { get; set; }
    public EyeInformation RightEye  { get; set; }
}

public class EyeInformation
{
    public string DeviceName { get; set; }
    public Crop Crop { get; set; }
    public float Rotation { get; set; }
    public bool HasHorizontalFlip  { get; set; }
    public bool HasVerticalFlip { get; set; }
}

public class Crop
{
    public double X;
    public double Y;
    public double W;
    public double H;
}
