# Display Switcher (Windows 11, C# WinForms)

A small Windows Forms utility with two quick topology actions plus four explicit role-based display modes:
1. External monitor only
2. External monitor + laptop screen
3. External monitor + laptop screen + TV
4. TV only


It uses the Windows CCD display configuration APIs (`QueryDisplayConfig`,
`DisplayConfigGetDeviceInfo`, and `SetDisplayConfig`) rather than keyboard shortcuts.

## Build
1. Install Visual Studio 2022 with the **.NET desktop development** workload and the .NET 8 SDK.
2. Open `DisplaySwitcher.csproj`.
3. Build the project.
4. Run `DisplaySwitcher.exe`.

No NuGet packages are required.

## First run
1. Turn on the TV and select the HDMI input connected to the laptop.
2. Click **Refresh connected displays**.
3. Assign the laptop screen, external monitor, and TV using the three dropdowns.
4. Click **Save display roles**.
5. Test **Monitor Only** first, then the other modes.

Saved roles use the display target's adapter/target identity and monitor device path, not a fragile "Display 1 / Display 2" ordering. If hardware ports, drivers, or the TV identity change, reassign the roles.

## Important notes
- This project must run in the interactive Windows desktop session; it is not a service.
- The TV must be on and detectable by Windows when switching to a TV mode.
- The Windows display driver chooses compatible modes when a path is enabled. If a requested layout is rejected, the app shows the Win32 error instead of reporting success.
- Multi-monitor driver behavior varies. Test each mode after saving your roles; if Windows rejects a custom topology, use Windows Settings to restore a working layout.
- The source has been prepared for .NET 8 WinForms. This environment does not include the Windows .NET SDK, so it could not be compiled or hardware-tested here. Please build in Visual Studio on the target Windows 11 machine before relying on it.


## Automatic role assignment

On refresh, the app now tries to assign roles automatically using Windows display connection technology: internal/embedded panel for the laptop, DisplayPort for the external monitor, and HDMI for the TV. Previously saved role assignments take priority when the corresponding target is detected. If Windows does not expose a powered-off TV at all, the app cannot apply a mode to that TV until it is detected again; turn it on and click **Refresh connected displays**. If auto-detection chooses the wrong device, select the correct role once and click **Save display roles**.
