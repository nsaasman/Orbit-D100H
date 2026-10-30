using System.ComponentModel;
using System.Runtime.InteropServices;
using Microsoft.Win32.SafeHandles;

namespace OrbitD100H.Core;

public sealed record HidCollection(string Path, ushort UsagePage, ushort Usage, ushort InputLength,
    ushort OutputLength, bool VendorInput, bool VendorOutput)
{
    public bool CanUseVendorProtocol => VendorInput && VendorOutput && InputLength >= Protocol.ReportLength
        && OutputLength >= Protocol.ReportLength;
}

public static class NativeHid
{
    public static IReadOnlyList<HidCollection> FindCollections()
    {
        var found = new List<HidCollection>();
        HidD_GetHidGuid(out var guid);
        var devices = SetupDiGetClassDevs(ref guid, null, 0, 0x12);
        if (devices == new nint(-1)) throw new Win32Exception(Marshal.GetLastWin32Error());
        try
        {
            for (uint index = 0; ; index++)
            {
                var data = new InterfaceData { Size = Marshal.SizeOf<InterfaceData>() };
                if (!SetupDiEnumDeviceInterfaces(devices, 0, ref guid, index, ref data))
                {
                    var error = Marshal.GetLastWin32Error();
                    if (error == 259) break;
                    throw new Win32Exception(error);
                }
                SetupDiGetDeviceInterfaceDetail(devices, ref data, 0, 0, out var size, 0);
                var detail = Marshal.AllocHGlobal(checked((int)size));
                try
                {
                    Marshal.WriteInt32(detail, nint.Size == 8 ? 8 : 6);
                    if (!SetupDiGetDeviceInterfaceDetail(devices, ref data, detail, size, out _, 0))
                        throw new Win32Exception(Marshal.GetLastWin32Error());
                    var path = Marshal.PtrToStringUni(detail + 4)!;
                    using var handle = CreateFile(path, 0, 3, 0, 3, 0, 0);
                    if (handle.IsInvalid) continue;
                    var attributes = new Attributes { Size = Marshal.SizeOf<Attributes>() };
                    if (!HidD_GetAttributes(handle, ref attributes) || attributes.VendorId != 0xFFF1
                        || attributes.ProductId != 0x0082) continue;
                    if (!HidD_GetPreparsedData(handle, out var preparsed)) continue;
                    try
                    {
                        if (HidP_GetCaps(preparsed, out var caps) != 0x00110000) continue;
                        found.Add(new(path, caps.UsagePage, caps.Usage, caps.InputLength, caps.OutputLength,
                            HasReport(preparsed, 0, caps.InputValueCaps, caps.InputButtonCaps),
                            HasReport(preparsed, 1, caps.OutputValueCaps, caps.OutputButtonCaps)));
                    }
                    finally { HidD_FreePreparsedData(preparsed); }
                }
                finally { Marshal.FreeHGlobal(detail); }
            }
        }
        finally { SetupDiDestroyDeviceInfoList(devices); }
        return found;
    }

    private static bool HasReport(nint preparsed, int type, ushort values, ushort buttons)
    {
        foreach (var valueCaps in new[] { true, false })
        {
            var count = valueCaps ? values : buttons;
            if (count == 0) continue;
            // HIDP_VALUE_CAPS and HIDP_BUTTON_CAPS are both 72 bytes; ReportID is at offset 2.
            var memory = Marshal.AllocHGlobal(count * 72);
            try
            {
                var status = valueCaps ? HidP_GetValueCaps(type, memory, ref count, preparsed)
                    : HidP_GetButtonCaps(type, memory, ref count, preparsed);
                if (status != 0x00110000) continue;
                for (var i = 0; i < count; i++)
                    if (Marshal.ReadByte(memory, i * 72 + 2) == Protocol.ReportId) return true;
            }
            finally { Marshal.FreeHGlobal(memory); }
        }
        return false;
    }

    public static FileStream Open(HidCollection collection)
    {
        if (!collection.CanUseVendorProtocol) throw new ArgumentException("Geen passende vendorcollectie.", nameof(collection));
        return OpenStream(collection, FileAccess.ReadWrite);
    }

    public static FileStream OpenInput(HidCollection collection)
    {
        if (collection.InputLength == 0) throw new ArgumentException("Geen inputrapporten.", nameof(collection));
        return OpenStream(collection, FileAccess.Read);
    }

    private static FileStream OpenStream(HidCollection collection, FileAccess access)
    {
        var handle = CreateFile(collection.Path, access == FileAccess.Read ? 0x80000000 : 0xC0000000, 3, 0, 3, 0x40000000, 0);
        if (handle.IsInvalid)
        {
            var error = Marshal.GetLastWin32Error();
            handle.Dispose();
            throw new Win32Exception(error, "HID-collectie openen mislukt.");
        }
        try { return new FileStream(handle, access, 1, isAsync: true); }
        catch { handle.Dispose(); throw; }
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct InterfaceData { public int Size; public Guid Guid; public uint Flags; public nint Reserved; }
    [StructLayout(LayoutKind.Sequential)]
    private struct Attributes { public int Size; public ushort VendorId, ProductId, Version; }
    [StructLayout(LayoutKind.Sequential)]
    private struct Caps
    {
        public ushort Usage, UsagePage, InputLength, OutputLength, FeatureLength;
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 17)] public ushort[] Reserved;
        public ushort LinkNodes, InputButtonCaps, InputValueCaps, InputDataIndices;
        public ushort OutputButtonCaps, OutputValueCaps, OutputDataIndices;
        public ushort FeatureButtonCaps, FeatureValueCaps, FeatureDataIndices;
    }

    [DllImport("hid.dll")] private static extern void HidD_GetHidGuid(out Guid guid);
    [DllImport("hid.dll")][return: MarshalAs(UnmanagedType.U1)]
    private static extern bool HidD_GetAttributes(SafeFileHandle handle, ref Attributes attributes);
    [DllImport("hid.dll")][return: MarshalAs(UnmanagedType.U1)]
    private static extern bool HidD_GetPreparsedData(SafeFileHandle handle, out nint data);
    [DllImport("hid.dll")][return: MarshalAs(UnmanagedType.U1)]
    private static extern bool HidD_FreePreparsedData(nint data);
    [DllImport("hid.dll")] private static extern int HidP_GetCaps(nint data, out Caps caps);
    [DllImport("hid.dll")] private static extern int HidP_GetValueCaps(int type, nint caps, ref ushort length, nint data);
    [DllImport("hid.dll")] private static extern int HidP_GetButtonCaps(int type, nint caps, ref ushort length, nint data);
    [DllImport("setupapi.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern nint SetupDiGetClassDevs(ref Guid guid, string? enumerator, nint parent, uint flags);
    [DllImport("setupapi.dll", SetLastError = true)]
    private static extern bool SetupDiEnumDeviceInterfaces(nint devices, nint info, ref Guid guid, uint index, ref InterfaceData data);
    [DllImport("setupapi.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern bool SetupDiGetDeviceInterfaceDetail(nint devices, ref InterfaceData data, nint detail,
        uint size, out uint required, nint info);
    [DllImport("setupapi.dll")] private static extern bool SetupDiDestroyDeviceInfoList(nint devices);
    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern SafeFileHandle CreateFile(string path, uint access, uint share, nint security,
        uint creation, uint flags, nint template);
}
