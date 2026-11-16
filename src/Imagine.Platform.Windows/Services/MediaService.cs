using Imagine.Core.Services;
using Windows.Media.Control;
using Windows.Storage.Streams;

namespace Imagine.Platform.Services;

/// <summary>Медиаплеер любого приложения (Spotify, браузер, Яндекс Музыка…) через GlobalSystemMediaTransportControls.</summary>
public sealed class MediaService : IMediaService
{
    private GlobalSystemMediaTransportControlsSessionManager? _manager;
    private GlobalSystemMediaTransportControlsSession? _session;

    public MediaState? Current { get; private set; }

    public event EventHandler<MediaState?>? Changed;

    public async Task InitializeAsync()
    {
        _manager = await GlobalSystemMediaTransportControlsSessionManager.RequestAsync();
        _manager.CurrentSessionChanged += async (_, _) => await AttachAsync().ConfigureAwait(false);
        await AttachAsync().ConfigureAwait(false);
    }

    private async Task AttachAsync()
    {
        if (_session is not null)
        {
            _session.MediaPropertiesChanged -= OnChanged;
            _session.PlaybackInfoChanged -= OnChanged;
            _session.TimelinePropertiesChanged -= OnChanged;
        }
        _session = _manager?.GetCurrentSession();
        if (_session is not null)
        {
            _session.MediaPropertiesChanged += OnChanged;
            _session.PlaybackInfoChanged += OnChanged;
            _session.TimelinePropertiesChanged += OnChanged;
        }
        await RefreshAsync().ConfigureAwait(false);
    }

    private async void OnChanged(GlobalSystemMediaTransportControlsSession sender, object args)
    {
        try { await RefreshAsync().ConfigureAwait(false); }
        catch (Exception ex) when (ex is System.Runtime.InteropServices.COMException or InvalidOperationException) { }
    }

    private async Task RefreshAsync()
    {
        var s = _session;
        if (s is null)
        {
            Current = null;
            Changed?.Invoke(this, null);
            return;
        }
        var props = await s.TryGetMediaPropertiesAsync();
        var playback = s.GetPlaybackInfo();
        var timeline = s.GetTimelineProperties();
        byte[]? thumb = null;
        if (props.Thumbnail is { } reference)
        {
            using var stream = await reference.OpenReadAsync();
            thumb = new byte[stream.Size];
            using var reader = new DataReader(stream);
            await reader.LoadAsync((uint)stream.Size);
            reader.ReadBytes(thumb);
        }
        // Если обложка не поменялась, отдаём прежний массив, чтобы не пересчитывать палитру.
        if (thumb is not null && Current?.Thumbnail is { } old && old.AsSpan().SequenceEqual(thumb)) thumb = old;
        Current = new MediaState(
            s.SourceAppUserModelId,
            props.Title ?? "",
            props.Artist ?? "",
            props.AlbumTitle,
            thumb,
            playback.PlaybackStatus == GlobalSystemMediaTransportControlsSessionPlaybackStatus.Playing,
            timeline.Position,
            timeline.EndTime - timeline.StartTime,
            playback.Controls.IsPreviousEnabled,
            playback.Controls.IsNextEnabled);
        Changed?.Invoke(this, Current);
    }

    public async Task PlayPauseAsync() { if (_session is { } s) await s.TryTogglePlayPauseAsync(); }
    public async Task NextAsync() { if (_session is { } s) await s.TrySkipNextAsync(); }
    public async Task PreviousAsync() { if (_session is { } s) await s.TrySkipPreviousAsync(); }
    public async Task SeekAsync(TimeSpan position) { if (_session is { } s) await s.TryChangePlaybackPositionAsync(position.Ticks); }
}
