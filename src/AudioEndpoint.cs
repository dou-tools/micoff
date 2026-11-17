using System.Runtime.InteropServices;

namespace MicMute;

sealed class AudioEndpoint : IDisposable
{
    public static readonly Guid OwnContext = new("6f1c2a77-3d5e-4b8a-9c41-7e2d0b9a5f13");

    readonly IMMDeviceEnumerator enumerator = (IMMDeviceEnumerator)new MMDeviceEnumeratorCom();
    readonly VolumeCallback callback;
    IAudioEndpointVolume? volume;
    string? deviceId;

    public string? DeviceFriendlyName { get; private set; }
    public event Action<bool, bool>? MuteChanged;

    public AudioEndpoint()
    {
        callback = new VolumeCallback(this);
    }

    public bool Resolve(string nameFilter)
    {
        var device = FindCapture(nameFilter) ?? DefaultCommunicationsCapture();
        if (device == null)
        {
            Release();
            return false;
        }

        device.GetId(out var id);
        if (id == deviceId && volume != null) return true;

        Release();
        var iid = typeof(IAudioEndpointVolume).GUID;
        Marshal.ThrowExceptionForHR(device.Activate(ref iid, 1, IntPtr.Zero, out var obj));
        volume = (IAudioEndpointVolume)obj;
        volume.RegisterControlChangeNotify(callback);
        deviceId = id;
        DeviceFriendlyName = GetFriendlyName(device);
        return true;
    }

    public bool? GetMute()
    {
        if (volume == null) return null;
        try
        {
            Marshal.ThrowExceptionForHR(volume.GetMute(out var muted));
            return muted;
        }
        catch (COMException)
        {
            Release();
            return null;
        }
    }

    public bool SetMute(bool muted)
    {
        if (volume == null) return false;
        var context = OwnContext;
        try
        {
            Marshal.ThrowExceptionForHR(volume.SetMute(muted, ref context));
            return true;
        }
        catch (COMException)
        {
            Release();
            return false;
        }
    }

    IMMDevice? FindCapture(string nameFilter)
    {
        if (string.IsNullOrWhiteSpace(nameFilter)) return null;
        // 1 = eCapture, 1 = DEVICE_STATE_ACTIVE
        if (enumerator.EnumAudioEndpoints(1, 1, out var collection) != 0) return null;
        collection.GetCount(out var count);
        for (uint i = 0; i < count; i++)
        {
            collection.Item(i, out var device);
            var name = GetFriendlyName(device);
            if (name != null && name.Contains(nameFilter, StringComparison.OrdinalIgnoreCase)) return device;
        }
        return null;
    }

    IMMDevice? DefaultCommunicationsCapture()
    {
        // 1 = eCapture, 2 = eCommunications
        return enumerator.GetDefaultAudioEndpoint(1, 2, out var device) == 0 ? device : null;
    }

    static string? GetFriendlyName(IMMDevice device)
    {
        if (device.OpenPropertyStore(0, out var store) != 0) return null;
        var key = new PropertyKey { FormatId = new Guid("a45c254e-df1c-4efd-8020-67d146a850e0"), PropertyId = 14 };
        if (store.GetValue(ref key, out var value) != 0) return null;
        try
        {
            // 31 = VT_LPWSTR
            return value.VarType == 31 ? Marshal.PtrToStringUni(value.Pointer) : null;
        }
        finally
        {
            PropVariantClear(ref value);
        }
    }

    void Release()
    {
        if (volume != null)
        {
            try { volume.UnregisterControlChangeNotify(callback); } catch (COMException) { }
            Marshal.ReleaseComObject(volume);
        }
        volume = null;
        deviceId = null;
        DeviceFriendlyName = null;
    }

    public void Dispose() => Release();

    [DllImport("ole32.dll")]
    static extern int PropVariantClear(ref PropVariant value);

    [ComVisible(true)]
    sealed class VolumeCallback(AudioEndpoint owner) : IAudioEndpointVolumeCallback
    {
        public int OnNotify(IntPtr data)
        {
            // AUDIO_VOLUME_NOTIFICATION_DATA: guid de contexto y luego el bool de mute
            var context = Marshal.PtrToStructure<Guid>(data);
            var muted = Marshal.ReadInt32(data, 16) != 0;
            owner.MuteChanged?.Invoke(muted, context == OwnContext);
            return 0;
        }
    }

    [ComImport, Guid("BCDE0395-E52F-467C-8E3D-C4579291692E")]
    class MMDeviceEnumeratorCom;

    [ComImport, Guid("A95664D2-9614-4F35-A746-DE8DB63617E6"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    interface IMMDeviceEnumerator
    {
        [PreserveSig] int EnumAudioEndpoints(int dataFlow, uint stateMask, out IMMDeviceCollection devices);
        [PreserveSig] int GetDefaultAudioEndpoint(int dataFlow, int role, out IMMDevice device);
    }

    [ComImport, Guid("0BD7A1BE-7A1A-44DB-8397-CC5392387B5E"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    interface IMMDeviceCollection
    {
        [PreserveSig] int GetCount(out uint count);
        [PreserveSig] int Item(uint index, out IMMDevice device);
    }

    [ComImport, Guid("D666063F-1587-4E43-81F1-B948E807363F"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    interface IMMDevice
    {
        [PreserveSig] int Activate(ref Guid iid, uint clsCtx, IntPtr activationParams, [MarshalAs(UnmanagedType.IUnknown)] out object instance);
        [PreserveSig] int OpenPropertyStore(uint access, out IPropertyStore store);
        [PreserveSig] int GetId([MarshalAs(UnmanagedType.LPWStr)] out string id);
    }

    [ComImport, Guid("886d8eeb-8cf2-4446-8d02-cdba1dbdcf99"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    interface IPropertyStore
    {
        [PreserveSig] int GetCount(out uint count);
        [PreserveSig] int GetAt(uint index, out PropertyKey key);
        [PreserveSig] int GetValue(ref PropertyKey key, out PropVariant value);
    }

    [ComImport, Guid("5CDF2C82-841E-4546-9722-0CF74078229A"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    interface IAudioEndpointVolume
    {
        [PreserveSig] int RegisterControlChangeNotify(IAudioEndpointVolumeCallback notify);
        [PreserveSig] int UnregisterControlChangeNotify(IAudioEndpointVolumeCallback notify);
        [PreserveSig] int GetChannelCount(out uint count);
        [PreserveSig] int SetMasterVolumeLevel(float level, ref Guid context);
        [PreserveSig] int SetMasterVolumeLevelScalar(float level, ref Guid context);
        [PreserveSig] int GetMasterVolumeLevel(out float level);
        [PreserveSig] int GetMasterVolumeLevelScalar(out float level);
        [PreserveSig] int SetChannelVolumeLevel(uint channel, float level, ref Guid context);
        [PreserveSig] int SetChannelVolumeLevelScalar(uint channel, float level, ref Guid context);
        [PreserveSig] int GetChannelVolumeLevel(uint channel, out float level);
        [PreserveSig] int GetChannelVolumeLevelScalar(uint channel, out float level);
        [PreserveSig] int SetMute([MarshalAs(UnmanagedType.Bool)] bool muted, ref Guid context);
        [PreserveSig] int GetMute([MarshalAs(UnmanagedType.Bool)] out bool muted);
    }

    [ComImport, Guid("657804FA-D6AD-4496-8A60-352752AF4F89"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    public interface IAudioEndpointVolumeCallback
    {
        [PreserveSig] int OnNotify(IntPtr data);
    }

    [StructLayout(LayoutKind.Sequential)]
    struct PropertyKey
    {
        public Guid FormatId;
        public int PropertyId;
    }

    [StructLayout(LayoutKind.Explicit, Size = 24)]
    struct PropVariant
    {
        [FieldOffset(0)] public ushort VarType;
        [FieldOffset(8)] public IntPtr Pointer;
    }
}
