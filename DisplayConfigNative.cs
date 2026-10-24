using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Text;

namespace DisplaySwitcher;

internal static class DisplayConfigNative
{
    internal const uint QDC_ALL_PATHS = 0x00000001;
    internal const uint QDC_ONLY_ACTIVE_PATHS = 0x00000002;

    internal const uint DISPLAYCONFIG_PATH_ACTIVE = 0x00000001;
    internal const uint DISPLAYCONFIG_DEVICE_INFO_GET_TARGET_NAME = 2;
    internal const uint DISPLAYCONFIG_PATH_MODE_IDX_INVALID = 0xFFFFFFFF;

    // DISPLAYCONFIG_OUTPUT_TECHNOLOGY values used to infer the user's physical roles.
    internal const uint OUTPUT_TECHNOLOGY_HDMI = 5;
    internal const uint OUTPUT_TECHNOLOGY_LVDS = 6;
    internal const uint OUTPUT_TECHNOLOGY_DISPLAYPORT = 10;
    internal const uint OUTPUT_TECHNOLOGY_DISPLAYPORT_EMBEDDED = 11;
    internal const uint OUTPUT_TECHNOLOGY_UDI_EMBEDDED = 13;
    internal const uint OUTPUT_TECHNOLOGY_INTERNAL = 0x80000000;

    internal const uint SDC_USE_SUPPLIED_DISPLAY_CONFIG = 0x00000020;
    internal const uint SDC_ALLOW_CHANGES = 0x00000400;
    internal const uint SDC_APPLY = 0x00000080;
    internal const uint SDC_SAVE_TO_DATABASE = 0x00000200;

    internal const int ERROR_SUCCESS = 0;
    internal const int ERROR_INSUFFICIENT_BUFFER = 122;

    [DllImport("user32.dll")]
    internal static extern int GetDisplayConfigBufferSizes(
        uint flags, out uint numPathArrayElements, out uint numModeInfoArrayElements);

    [DllImport("user32.dll")]
    internal static extern int QueryDisplayConfig(
        uint flags,
        ref uint numPathArrayElements,
        [Out] DISPLAYCONFIG_PATH_INFO[] pathArray,
        ref uint numModeInfoArrayElements,
        [Out] DISPLAYCONFIG_MODE_INFO[] modeInfoArray,
        IntPtr currentTopologyId);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    internal static extern int DisplayConfigGetDeviceInfo(ref DISPLAYCONFIG_TARGET_DEVICE_NAME requestPacket);

    [DllImport("user32.dll")]
    internal static extern int SetDisplayConfig(
        uint numPathArrayElements,
        [In] DISPLAYCONFIG_PATH_INFO[]? pathArray,
        uint numModeInfoArrayElements,
        [In] DISPLAYCONFIG_MODE_INFO[]? modeInfoArray,
        uint flags);

    [StructLayout(LayoutKind.Sequential)]
    internal struct LUID : IEquatable<LUID>
    {
        public uint LowPart;
        public int HighPart;
        public bool Equals(LUID other) => LowPart == other.LowPart && HighPart == other.HighPart;
        public override bool Equals(object? obj) => obj is LUID other && Equals(other);
        public override int GetHashCode() => HashCode.Combine(LowPart, HighPart);
        public override string ToString() => $"{HighPart:X8}:{LowPart:X8}";
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct DISPLAYCONFIG_PATH_SOURCE_INFO
    {
        public LUID adapterId;
        public uint id;
        public uint modeInfoIdx;
        public uint statusFlags;
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    internal struct DISPLAYCONFIG_RATIONAL
    {
        public uint Numerator;
        public uint Denominator;
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct DISPLAYCONFIG_PATH_TARGET_INFO
    {
        public LUID adapterId;
        public uint id;
        public uint modeInfoIdx;
        public uint outputTechnology;
        public uint rotation;
        public uint scaling;
        public DISPLAYCONFIG_RATIONAL refreshRate;
        public uint scanLineOrdering;
        [MarshalAs(UnmanagedType.Bool)] public bool targetAvailable;
        public uint statusFlags;
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct DISPLAYCONFIG_PATH_INFO
    {
        public DISPLAYCONFIG_PATH_SOURCE_INFO sourceInfo;
        public DISPLAYCONFIG_PATH_TARGET_INFO targetInfo;
        public uint flags;
    }

    [StructLayout(LayoutKind.Explicit, Size = 64)]
    internal struct DISPLAYCONFIG_MODE_INFO
    {
        [FieldOffset(0)] public uint infoType;
        [FieldOffset(4)] public uint id;
        [FieldOffset(8)] public LUID adapterId;
        // The native structure has a 48-byte union beginning at offset 16.
        // Explicit size/layout avoids managed ByValArray initialization issues.
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    internal struct DISPLAYCONFIG_DEVICE_INFO_HEADER
    {
        public uint type;
        public uint size;
        public LUID adapterId;
        public uint id;
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    internal struct DISPLAYCONFIG_TARGET_DEVICE_NAME
    {
        public DISPLAYCONFIG_DEVICE_INFO_HEADER header;
        public uint flags;
        public uint outputTechnology;
        public ushort edidManufactureId;
        public ushort edidProductCodeId;
        public uint connectorInstance;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 64)]
        public string monitorFriendlyDeviceName;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)]
        public string monitorDevicePath;
    }

    internal sealed record DisplayEndpoint(
        string Key, string Name, string DevicePath, LUID AdapterId, uint TargetId,
        bool IsActive, bool IsAvailable, uint OutputTechnology, DISPLAYCONFIG_PATH_INFO Path = default);

    internal static List<DisplayEndpoint> EnumerateDisplays()
    {
        // QDC_ALL_PATHS includes inactive paths so a TV that is on and connected by HDMI
        // can be discovered even if it is not currently part of the desktop.
        for (int attempt = 0; attempt < 4; attempt++)
        {
            int rc = GetDisplayConfigBufferSizes(QDC_ALL_PATHS, out uint pathCount, out uint modeCount);
            if (rc != ERROR_SUCCESS)
                throw new Win32Exception(rc, $"GetDisplayConfigBufferSizes failed (Win32 {rc}).");

            var paths = new DISPLAYCONFIG_PATH_INFO[(int)Math.Max(pathCount, 1u)];
            var modes = new DISPLAYCONFIG_MODE_INFO[(int)Math.Max(modeCount, 1u)];
            uint pc = (uint)paths.Length, mc = (uint)modes.Length;
            rc = QueryDisplayConfig(QDC_ALL_PATHS, ref pc, paths, ref mc, modes, IntPtr.Zero);
            if (rc == ERROR_INSUFFICIENT_BUFFER) continue;
            if (rc != ERROR_SUCCESS)
                throw new Win32Exception(rc, $"QueryDisplayConfig failed (Win32 {rc}).");

            var result = new Dictionary<string, DisplayEndpoint>(StringComparer.OrdinalIgnoreCase);
            for (int i = 0; i < pc; i++)
            {
                var path = paths[i];
                var req = new DISPLAYCONFIG_TARGET_DEVICE_NAME
                {
                    header = new DISPLAYCONFIG_DEVICE_INFO_HEADER
                    {
                        type = DISPLAYCONFIG_DEVICE_INFO_GET_TARGET_NAME,
                        size = (uint)Marshal.SizeOf<DISPLAYCONFIG_TARGET_DEVICE_NAME>(),
                        adapterId = path.targetInfo.adapterId,
                        id = path.targetInfo.id
                    },
                    monitorFriendlyDeviceName = string.Empty,
                    monitorDevicePath = string.Empty
                };

                int nameRc = DisplayConfigGetDeviceInfo(ref req);
                if (nameRc != ERROR_SUCCESS) continue;

                string devicePath = req.monitorDevicePath?.Trim() ?? "";
                string friendly = req.monitorFriendlyDeviceName?.Trim() ?? "";
                if (string.IsNullOrWhiteSpace(devicePath))
                    devicePath = $"TARGET:{path.targetInfo.adapterId}:{path.targetInfo.id}";
                if (string.IsNullOrWhiteSpace(friendly))
                    friendly = $"Display target {path.targetInfo.id}";

                string key = $"{path.targetInfo.adapterId}:{path.targetInfo.id}:{devicePath}";
                bool active = (path.flags & DISPLAYCONFIG_PATH_ACTIVE) != 0;
                var ep = new DisplayEndpoint(key, friendly, devicePath, path.targetInfo.adapterId,
                    path.targetInfo.id, active, path.targetInfo.targetAvailable,
                    path.targetInfo.outputTechnology, path);
                // Prefer the active path entry, because it contains current source/mode details.
                if (!result.TryGetValue(key, out var previous) || (active && !previous.IsActive))
                    result[key] = ep;
            }

            return result.Values
                .OrderByDescending(d => d.IsActive)
                .ThenBy(d => d.Name, StringComparer.CurrentCultureIgnoreCase)
                .ToList();
        }

        throw new InvalidOperationException("Display topology kept changing while Windows was being queried. Try again.");
    }

    internal static void ApplyDisplays(IReadOnlyCollection<DisplayEndpoint> chosen)
    {
        if (chosen.Count == 0) throw new InvalidOperationException("No displays were selected.");

        var paths = new List<DISPLAYCONFIG_PATH_INFO>();
        foreach (var display in chosen)
        {
            var path = display.Path;
            // Request Windows to choose a valid mode for this path. This avoids reusing
            // stale mode indexes from a previous topology.
            path.sourceInfo.modeInfoIdx = DISPLAYCONFIG_PATH_MODE_IDX_INVALID;
            path.targetInfo.modeInfoIdx = DISPLAYCONFIG_PATH_MODE_IDX_INVALID;
            path.flags |= DISPLAYCONFIG_PATH_ACTIVE;
            paths.Add(path);
        }

        // Windows validates and applies the supplied paths. SDC_ALLOW_CHANGES lets the
        // display driver choose compatible source/target modes for newly enabled paths.
        uint flags = SDC_USE_SUPPLIED_DISPLAY_CONFIG | SDC_ALLOW_CHANGES | SDC_APPLY | SDC_SAVE_TO_DATABASE;
        int rc = SetDisplayConfig((uint)paths.Count, paths.ToArray(), 0, null, flags);
        if (rc != ERROR_SUCCESS)
            throw new Win32Exception(rc,
                $"Windows rejected this display layout (Win32 {rc}). Check that each requested screen is powered on and connected, then retry.");
    }
}
