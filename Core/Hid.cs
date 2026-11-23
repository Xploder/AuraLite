using System.ComponentModel;
using System.Runtime.InteropServices;
using Microsoft.Win32.SafeHandles;

namespace AuraLite;

public sealed record HidDevice(string Path, ushort UsagePage, ushort Usage,
    ushort InputLength, ushort OutputLength, ushort FeatureLength, bool AuraReport)
{
    public bool Supported => Hid.AuraProduct(Path) == 0x19af
        && UsagePage == 0xff72 && Usage == 0x00a1 && InputLength == 65 && OutputLength == 65 && AuraReport;

    public override string ToString() =>
        $"{Path}\nUsage {UsagePage:X4}:{Usage:X4} · Input {InputLength} · Output {OutputLength} · Feature {FeatureLength} · EC {AuraReport}";
}

public static class Hid
{
    private const int Success = 0x00110000;
    private const int AccessDenied = 5;
    private const int BufferSmall = 0x1a;

    internal static ushort AuraProduct(string path)
    {
        const string prefix = @"\\?\HID#VID_0B05&PID_";
        ReadOnlySpan<char> value = path.AsSpan();
        if (!value.StartsWith(prefix, StringComparison.OrdinalIgnoreCase) ||
            value.Length <= prefix.Length + 4 || value[prefix.Length + 4] is not ('&' or '#'))
            return 0;
        return ushort.TryParse(value.Slice(prefix.Length, 4), System.Globalization.NumberStyles.HexNumber,
            System.Globalization.CultureInfo.InvariantCulture, out ushort product) ? product : (ushort)0;
    }

    public static IReadOnlyList<HidDevice> List(bool includeRelated = true)
    {
        HidD_GetHidGuid(out Guid guid);
        char[]? paths = null;
        for (int attempt = 0; attempt < 3; attempt++)
        {
            CheckConfig(CM_Get_Device_Interface_List_Size(out uint size, ref guid, null, 0));
            paths = new char[checked((int)size)];
            int status = CM_Get_Device_Interface_List(ref guid, null, paths, size, 0);
            if (status == BufferSmall)
            {
                paths = null;
                continue;
            }
            CheckConfig(status);
            break;
        }

        if (paths is null)
            throw new IOException("Die HID-Geräteliste hat sich während der Abfrage geändert.");

        var devices = new List<HidDevice>();
        foreach (string path in new string(paths).Split('\0', StringSplitOptions.RemoveEmptyEntries))
        {
            ushort product = AuraProduct(path);
            if (product != 0x19af && (!includeRelated || product is not (0x18f3 or 0x1939 or 0x1aa6 or 0x1bed)))
                continue;
            using SafeFileHandle handle = Open(path, 0, 3, 0);
            devices.Add(Describe(path, handle));
        }
        return devices;
    }

    internal static FileStream Connect(HidDevice device)
    {
        if (!device.Supported)
            throw new IOException("Die HID-Schnittstelle passt nicht zum Aura-Protokoll.");
        SafeFileHandle handle = Open(device.Path, 0xc0000000, 0, 0x40000000);
        try
        {
            if (Describe(device.Path, handle) != device)
                throw new IOException("Die Geräteeigenschaften haben sich geändert.");
            if (!HidD_FlushQueue(handle))
                throw new Win32Exception(Marshal.GetLastWin32Error(), "HID-Eingangspuffer konnte nicht geleert werden.");
            return new FileStream(handle, FileAccess.ReadWrite, bufferSize: 1, isAsync: true);
        }
        catch
        {
            handle.Dispose();
            throw;
        }
    }

    private static SafeFileHandle Open(string path, uint access, uint share, uint flags)
    {
        SafeFileHandle handle = CreateFile(path, access, share, 0, 3, flags, 0);
        if (!handle.IsInvalid)
            return handle;
        int error = Marshal.GetLastWin32Error();
        handle.Dispose();
        string message = error is AccessDenied or 32
            ? "Zugriff auf die HID-Schnittstelle verweigert oder durch ein anderes Programm belegt."
            : $"HID-Schnittstelle konnte nicht geöffnet werden (Windows-Fehler {error}).";
        throw new Win32Exception(error, $"{message} (Code {error}) {path}");
    }

    private static HidDevice Describe(string path, SafeFileHandle handle)
    {
        var attributes = new Attributes { Size = Marshal.SizeOf<Attributes>() };
        if (!HidD_GetAttributes(handle, ref attributes) ||
            attributes.Vendor != 0x0b05 || attributes.Product != AuraProduct(path))
            throw new IOException("HID VID/PID konnten nicht bestätigt werden.");
        if (!HidD_GetPreparsedData(handle, out nint data))
            throw new Win32Exception(Marshal.GetLastWin32Error());
        try
        {
            if (HidP_GetCaps(data, out Caps caps) != Success)
                throw new IOException("HID-Deskriptor konnte nicht ausgewertet werden.");
            bool report = caps.Input == 65 && caps.Output == 65
                && HidP_InitializeReportForID(0, 0xec, data, new byte[65], 65) == Success
                && HidP_InitializeReportForID(1, 0xec, data, new byte[65], 65) == Success;
            return new HidDevice(path, caps.UsagePage, caps.Usage, caps.Input, caps.Output, caps.Feature, report);
        }
        finally
        {
            HidD_FreePreparsedData(data);
        }
    }

    private static void CheckConfig(int status)
    {
        if (status != 0) throw new IOException($"HID-Auflistung fehlgeschlagen (CONFIGRET 0x{status:X}).");
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct Attributes { public int Size; public ushort Vendor, Product, Version; }

    [StructLayout(LayoutKind.Sequential)]
    private unsafe struct Caps
    {
        public ushort Usage, UsagePage, Input, Output, Feature;
        private fixed ushort reserved[27];
    }

    [DllImport("hid.dll")] private static extern void HidD_GetHidGuid(out Guid guid);
    [DllImport("hid.dll", SetLastError = true)] [return: MarshalAs(UnmanagedType.U1)]
    private static extern bool HidD_GetAttributes(SafeFileHandle device, ref Attributes attributes);
    [DllImport("hid.dll", SetLastError = true)] [return: MarshalAs(UnmanagedType.U1)]
    private static extern bool HidD_GetPreparsedData(SafeFileHandle device, out nint data);
    [DllImport("hid.dll")] [return: MarshalAs(UnmanagedType.U1)]
    private static extern bool HidD_FreePreparsedData(nint data);
    [DllImport("hid.dll", SetLastError = true)] [return: MarshalAs(UnmanagedType.U1)]
    private static extern bool HidD_FlushQueue(SafeFileHandle device);
    [DllImport("hid.dll")] private static extern int HidP_GetCaps(nint data, out Caps caps);
    [DllImport("hid.dll")] private static extern int HidP_InitializeReportForID(int type, byte id, nint data, [Out] byte[] report, uint length);
    [DllImport("cfgmgr32.dll", EntryPoint = "CM_Get_Device_Interface_List_SizeW", CharSet = CharSet.Unicode, ExactSpelling = true)]
    private static extern int CM_Get_Device_Interface_List_Size(out uint size, ref Guid guid, string? id, uint flags);
    [DllImport("cfgmgr32.dll", EntryPoint = "CM_Get_Device_Interface_ListW", CharSet = CharSet.Unicode, ExactSpelling = true)]
    private static extern int CM_Get_Device_Interface_List(ref Guid guid, string? id, [Out] char[] buffer, uint size, uint flags);
    [DllImport("kernel32.dll", EntryPoint = "CreateFileW", CharSet = CharSet.Unicode, ExactSpelling = true, SetLastError = true)]
    private static extern SafeFileHandle CreateFile(string name, uint access, uint share, nint security, uint creation, uint flags, nint template);
}
