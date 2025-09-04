# Baballonia

**Baballonia** is a cross-platform, hardware-agnostic VR eye and face tracking application.

## Installation

### Windows

Head to the [Releases](https://github.com/Project-Babble/Baballonia/releases/latest) tab and download the latest installer. Follow the instructions in the wizard to install.

You may be prompted to download the .NET runtime for desktop apps, install it if needs be.

### MacOS & Linux

Baballonia does not currently have an installer for MacOS or Linux. You will need to follow our build instructions and run it from source.

## Use

### VRChat

To use Baballonia with VRChat, you will need to use VRCFaceTracking with its VRCFaceTracking module.

You will need to download and install the latest version of VRCFaceTracking which can be found on [Steam](https://store.steampowered.com/app/3329480/VRCFaceTracking/)

Once you have the program installed, you will need to install our module from the registry.

More information can be found on the [VRCFT Docs](https://docs.vrcft.io/docs/vrcft-software/vrcft\#module-registry)

### Resonite / ChilloutVR

TBD - Existing mods should be compatible with Baballonia's lower face tracking.

## Supported Hardware

Baballonia supports many kinds of hardware for eye and face tracking:

| Device | Eyes | Face | Notes |
| ----- | ----- | ----- | ----- |
| Official Babble Face Tracker | 0 | X |  |
| DIY and 3rd party Babble Trackers | 0 | X |  |
| Vive Facial Tracker | 0 | X | Linux Only |
| DIY EyetrackVR | X | 0 |  |
| Bigscreen Beyond 2e | X | 0 |  |
| Vive Pro Eye | X | 0 | Requires [Revision](https://github.com/Blue-Doggo/ReVisionbro) |
| Varjo Aero | X | 0 | Requires the Varjo Streamer |
| HP Reverb G2 Omnicept | X | 0 | Requires [BrokenEye](https://github.com/ghostiam/BrokenEye) |
| Pimax Crystal | X | 0 | Requires [BrokenEye](https://github.com/ghostiam/BrokenEye) |

## Build Instructions

If you want to build from source, clone this repo and open the `.sln`/`.csproj` files in an editor of your choice. I have only tested and built this on Visual Studio 2022 and Rider, but it should work with other IDEs. Load and build the Baballonia and Baballonia.Desktop solutions.

### Special instructions for Linux 

`libtesseract 4` is deprecated in newer repositories, but is needed to build.
