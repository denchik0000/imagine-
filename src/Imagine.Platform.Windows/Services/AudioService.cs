using System.Diagnostics;
using System.Runtime.InteropServices;
using Imagine.Core.Services;
using NAudio.CoreAudioApi;

namespace Imagine.Platform.Services;

/// <summary>Громкость, mute, микрофон, громкость по приложениям (Core Audio через NAudio.Wasapi).</summary>
public sealed class AudioService : IAudioService, IDisposable
{
    private readonly MMDeviceEnumerator _enumerator = new();
    private MMDevice? _render;
    private MMDevice? _capture;

    public event EventHandler? Changed;

    private MMDevice? Render
    {
        get
        {
            if (_render is not null) return _render;
            try
            {
                _render = _enumerator.GetDefaultAudioEndpoint(DataFlow.Render, Role.Multimedia);
                _render.AudioEndpointVolume.OnVolumeNotification += _ => Changed?.Invoke(this, EventArgs.Empty);
            }
            catch (COMException) { _render = null; }
            return _render;
        }
    }

    private MMDevice? Capture
    {
        get
        {
            if (_capture is not null) return _capture;
            try { _capture = _enumerator.GetDefaultAudioEndpoint(DataFlow.Capture, Role.Communications); }
            catch (COMException) { _capture = null; }
            return _capture;
        }
    }

    public float MasterVolume
    {
        get => Render?.AudioEndpointVolume.MasterVolumeLevelScalar ?? 0;
        set { if (Render is { } d) d.AudioEndpointVolume.MasterVolumeLevelScalar = Math.Clamp(value, 0f, 1f); }
    }

    public bool IsMuted
    {
        get => Render?.AudioEndpointVolume.Mute ?? false;
        set { if (Render is { } d) d.AudioEndpointVolume.Mute = value; }
    }

    public bool IsMicrophoneMuted
    {
        get => Capture?.AudioEndpointVolume.Mute ?? false;
        set { if (Capture is { } d) d.AudioEndpointVolume.Mute = value; }
    }

    public IReadOnlyList<AudioSessionInfo> GetSessions()
    {
        var device = Render;
        if (device is null) return [];
        var result = new List<AudioSessionInfo>();
        var sessions = device.AudioSessionManager.Sessions;
        for (int i = 0; i < sessions.Count; i++)
        {
            var s = sessions[i];
            if (s.IsSystemSoundsSession || s.State == NAudio.CoreAudioApi.Interfaces.AudioSessionState.AudioSessionStateExpired) continue;
            string name = s.DisplayName;
            string? icon = string.IsNullOrEmpty(s.IconPath) ? null : s.IconPath;
            try
            {
                using var p = Process.GetProcessById((int)s.GetProcessID);
                if (string.IsNullOrEmpty(name) || name.StartsWith('@')) name = p.MainModule?.FileVersionInfo.FileDescription is { Length: > 0 } d ? d : p.ProcessName;
                icon ??= p.MainModule?.FileName;
            }
            catch (Exception ex) when (ex is ArgumentException or InvalidOperationException or System.ComponentModel.Win32Exception) { }
            result.Add(new AudioSessionInfo(s.GetSessionInstanceIdentifier, name, icon, s.SimpleAudioVolume.Volume, s.SimpleAudioVolume.Mute));
        }
        return result;
    }

    public void SetSessionVolume(string sessionId, float volume)
    {
        var sessions = Render?.AudioSessionManager.Sessions;
        if (sessions is null) return;
        for (int i = 0; i < sessions.Count; i++)
        {
            if (sessions[i].GetSessionInstanceIdentifier == sessionId)
            {
                sessions[i].SimpleAudioVolume.Volume = Math.Clamp(volume, 0f, 1f);
                return;
            }
        }
    }

    public IReadOnlyList<AudioDeviceInfo> GetOutputDevices()
    {
        string? defaultId = Render?.ID;
        return _enumerator.EnumerateAudioEndPoints(DataFlow.Render, DeviceState.Active)
            .Select(d => new AudioDeviceInfo(d.ID, d.FriendlyName, d.ID == defaultId))
            .ToList();
    }

    public bool TrySetDefaultOutput(string deviceId)
    {
        try
        {
            PolicyConfig.SetDefaultEndpoint(deviceId);
            _render = null;
            Changed?.Invoke(this, EventArgs.Empty);
            return true;
        }
        catch (Exception ex) when (ex is COMException or InvalidCastException)
        {
            return false;
        }
    }

    public void Dispose()
    {
        _render?.Dispose();
        _capture?.Dispose();
        _enumerator.Dispose();
    }

    /// <summary>
    /// Недокументированный, но стабильный с Windows 7 интерфейс IPolicyConfig (им пользуются EarTrumpet, SoundSwitch).
    /// </summary>
    private static class PolicyConfig
    {
        [ComImport, Guid("870af99c-171d-4f9e-af0d-e63df40c2bc9")]
        private class PolicyConfigClient { }

        [ComImport, Guid("f8679f50-850a-41cf-9c72-430f290290c8"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        private interface IPolicyConfig
        {
            [PreserveSig] int GetMixFormat(string a, IntPtr b);
            [PreserveSig] int GetDeviceFormat(string a, int b, IntPtr c);
            [PreserveSig] int ResetDeviceFormat(string a);
            [PreserveSig] int SetDeviceFormat(string a, IntPtr b, IntPtr c);
            [PreserveSig] int GetProcessingPeriod(string a, int b, IntPtr c, IntPtr d);
            [PreserveSig] int SetProcessingPeriod(string a, IntPtr b);
            [PreserveSig] int GetShareMode(string a, IntPtr b);
            [PreserveSig] int SetShareMode(string a, IntPtr b);
            [PreserveSig] int GetPropertyValue(string a, IntPtr b, IntPtr c);
            [PreserveSig] int SetPropertyValue(string a, IntPtr b, IntPtr c);
            [PreserveSig] int SetDefaultEndpoint([MarshalAs(UnmanagedType.LPWStr)] string deviceId, int role);
            [PreserveSig] int SetEndpointVisibility(string a, int b);
        }

        public static void SetDefaultEndpoint(string deviceId)
        {
            var config = (IPolicyConfig)new PolicyConfigClient();
            try
            {
                for (int role = 0; role < 3; role++) Marshal.ThrowExceptionForHR(config.SetDefaultEndpoint(deviceId, role));
            }
            finally
            {
                Marshal.ReleaseComObject(config);
            }
        }
    }
}
