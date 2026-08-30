using System;
using System.Runtime.InteropServices;

namespace MyCustomDock
{
    // Small Core Audio interop used only for the default render endpoint mute
    // bit. It deliberately returns "unknown" on any COM/device failure.
    public static class AudioStateReader
    {
        private static readonly Guid MmDeviceEnumeratorClsid = new Guid("BCDE0395-E52F-467C-8E3D-C4579291692E");
        private static readonly Guid AudioEndpointVolumeIid = new Guid("5CDF2C82-841E-4546-9722-0CF74078229A");
        private static readonly object FailureLogLock = new object();
        private static DateTime lastFailureLogUtc = DateTime.MinValue;

        public static bool TryGetMuteState(out bool muted)
        {
            muted = false;
            IMMDeviceEnumerator enumerator = null;
            IMMDevice device = null;
            IAudioEndpointVolume endpoint = null;
            try
            {
                Type enumeratorType = Type.GetTypeFromCLSID(MmDeviceEnumeratorClsid);
                if (enumeratorType == null) return false;

                enumerator = (IMMDeviceEnumerator)Activator.CreateInstance(enumeratorType);
                int hr = enumerator.GetDefaultAudioEndpoint(0, 1, out device);
                if (hr < 0 || device == null) return false;

                Guid iid = AudioEndpointVolumeIid;
                hr = device.Activate(ref iid, 23, IntPtr.Zero, out endpoint);
                if (hr < 0 || endpoint == null) return false;

                hr = endpoint.GetMute(out muted);
                return hr >= 0;
            }
            catch (Exception ex)
            {
                LogFailure(ex);
                muted = false;
                return false;
            }
            finally
            {
                ReleaseCom(endpoint);
                ReleaseCom(device);
                ReleaseCom(enumerator);
            }
        }

        public static string GetDisplayGlyph(bool stateKnown, bool muted)
        {
            // The visual is rendered by DockWindow with WPF vector geometry.
            // Keep this small state label for probes and accessibility logic;
            // it must never be used as a color Emoji glyph in the UI.
            if (!stateKnown) return "neutral";
            return muted ? "muted" : "speaker";
        }

        public static bool TryGetMasterVolume(out float scalar)
        {
            scalar = 0.0f;
            IMMDeviceEnumerator enumerator = null;
            IMMDevice device = null;
            IAudioEndpointVolume endpoint = null;
            try
            {
                if (!TryCreateEndpoint(out enumerator, out device, out endpoint)) return false;
                int hr = endpoint.GetMasterVolumeLevelScalar(out scalar);
                return hr >= 0;
            }
            catch (Exception ex)
            {
                LogFailure(ex);
                scalar = 0.0f;
                return false;
            }
            finally
            {
                ReleaseCom(endpoint);
                ReleaseCom(device);
                ReleaseCom(enumerator);
            }
        }

        public static bool TrySetMasterVolume(float scalar)
        {
            IMMDeviceEnumerator enumerator = null;
            IMMDevice device = null;
            IAudioEndpointVolume endpoint = null;
            try
            {
                if (!TryCreateEndpoint(out enumerator, out device, out endpoint)) return false;
                Guid eventContext = Guid.Empty;
                scalar = Math.Max(0.0f, Math.Min(1.0f, scalar));
                return endpoint.SetMasterVolumeLevelScalar(scalar, ref eventContext) >= 0;
            }
            catch (Exception ex)
            {
                LogFailure(ex);
                return false;
            }
            finally
            {
                ReleaseCom(endpoint);
                ReleaseCom(device);
                ReleaseCom(enumerator);
            }
        }

        public static bool TrySetMuteState(bool muted)
        {
            IMMDeviceEnumerator enumerator = null;
            IMMDevice device = null;
            IAudioEndpointVolume endpoint = null;
            try
            {
                if (!TryCreateEndpoint(out enumerator, out device, out endpoint)) return false;
                Guid eventContext = Guid.Empty;
                return endpoint.SetMute(muted, ref eventContext) >= 0;
            }
            catch (Exception ex)
            {
                LogFailure(ex);
                return false;
            }
            finally
            {
                ReleaseCom(endpoint);
                ReleaseCom(device);
                ReleaseCom(enumerator);
            }
        }

        private static bool TryCreateEndpoint(out IMMDeviceEnumerator enumerator, out IMMDevice device, out IAudioEndpointVolume endpoint)
        {
            enumerator = null;
            device = null;
            endpoint = null;
            Type enumeratorType = Type.GetTypeFromCLSID(MmDeviceEnumeratorClsid);
            if (enumeratorType == null) return false;

            enumerator = (IMMDeviceEnumerator)Activator.CreateInstance(enumeratorType);
            int hr = enumerator.GetDefaultAudioEndpoint(0, 1, out device);
            if (hr < 0 || device == null) return false;

            Guid iid = AudioEndpointVolumeIid;
            hr = device.Activate(ref iid, 23, IntPtr.Zero, out endpoint);
            return hr >= 0 && endpoint != null;
        }

        private static void LogFailure(Exception ex)
        {
            DateTime now = DateTime.UtcNow;
            lock (FailureLogLock)
            {
                if ((now - lastFailureLogUtc).TotalSeconds < 60) return;
                lastFailureLogUtc = now;
            }
            EntryPoint.LogException("audio.state_read", ex);
        }

        private static void ReleaseCom(object value)
        {
            try
            {
                if (value != null && Marshal.IsComObject(value)) Marshal.FinalReleaseComObject(value);
            }
            catch (Exception ex)
            {
                EntryPoint.LogException("audio.release_com", ex);
            }
        }

        [ComImport]
        [Guid("A95664D2-9614-4F35-A746-DE8DB63617E6")]
        [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        private interface IMMDeviceEnumerator
        {
            [PreserveSig]
            int EnumAudioEndpoints(int dataFlow, int stateMask, [MarshalAs(UnmanagedType.Interface)] out object devices);

            [PreserveSig]
            int GetDefaultAudioEndpoint(int dataFlow, int role, out IMMDevice device);

            [PreserveSig]
            int GetDevice([MarshalAs(UnmanagedType.LPWStr)] string id, out IMMDevice device);

            [PreserveSig]
            int RegisterEndpointNotificationCallback(IntPtr client);

            [PreserveSig]
            int UnregisterEndpointNotificationCallback(IntPtr client);
        }

        [ComImport]
        [Guid("D666063F-1587-4E43-81F1-B948E807363F")]
        [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        private interface IMMDevice
        {
            [PreserveSig]
            int Activate(ref Guid iid, uint clsContext, IntPtr activationParams,
                [MarshalAs(UnmanagedType.Interface)] out IAudioEndpointVolume endpoint);
        }

        [ComImport]
        [Guid("5CDF2C82-841E-4546-9722-0CF74078229A")]
        [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        private interface IAudioEndpointVolume
        {
            [PreserveSig] int RegisterControlChangeNotify(IntPtr notify);
            [PreserveSig] int UnregisterControlChangeNotify(IntPtr notify);
            [PreserveSig] int GetChannelCount(out uint channelCount);
            [PreserveSig] int SetMasterVolumeLevel(float level, ref Guid eventContext);
            [PreserveSig] int SetMasterVolumeLevelScalar(float level, ref Guid eventContext);
            [PreserveSig] int GetMasterVolumeLevel(out float level);
            [PreserveSig] int GetMasterVolumeLevelScalar(out float level);
            [PreserveSig] int SetChannelVolumeLevel(uint channelNumber, float level, ref Guid eventContext);
            [PreserveSig] int SetChannelVolumeLevelScalar(uint channelNumber, float level, ref Guid eventContext);
            [PreserveSig] int GetChannelVolumeLevel(uint channelNumber, out float level);
            [PreserveSig] int GetChannelVolumeLevelScalar(uint channelNumber, out float level);
            [PreserveSig] int SetMute([MarshalAs(UnmanagedType.Bool)] bool mute, ref Guid eventContext);
            [PreserveSig] int GetMute([MarshalAs(UnmanagedType.Bool)] out bool mute);
        }
    }
}
