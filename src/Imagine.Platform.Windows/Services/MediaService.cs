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
        _manager.CurrentSessionChanged += async (_, _) =>
        {
            try { await AttachAsync().ConfigureAwait(false); }
            catch (Exception) { /* событие из потока WinRT: исключение здесь уронило бы приложение */ }
        };
        await AttachAsync().ConfigureAwait(false);
    }

    private async Task AttachAsync()
    {
        if (_session is not null)
        {
            _session.MediaPropertiesChanged -= OnChanged;
            _session.PlaybackInfoChanged -= OnChanged;
            _session.TimelinePropertiesChanged -= OnTimeline;
        }
        _session = _manager?.GetCurrentSession();
        if (_session is not null)
        {
            _session.MediaPropertiesChanged += OnChanged;
            _session.PlaybackInfoChanged += OnChanged;
            _session.TimelinePropertiesChanged += OnTimeline;
        }
        await RefreshAsync().ConfigureAwait(false);
    }

    private async void OnChanged(GlobalSystemMediaTransportControlsSession sender, object args)
    {
        try { await RefreshAsync().ConfigureAwait(false); }
        catch (Exception) { /* то же: обработчик события WinRT не должен ронять процесс */ }
    }

    /// <summary>
    /// Позиция трека меняется каждую секунду: только обновляем время, без перечитывания обложки
    /// и без события (иначе шторка, островок и палитра пересчитывались бы ежесекундно).
    /// </summary>
    private void OnTimeline(GlobalSystemMediaTransportControlsSession sender, object args)
    {
        try
        {
            if (Current is not { } c || !ReferenceEquals(sender, _session)) return;
            var t = sender.GetTimelineProperties();
            Current = c with { Position = t.Position, Duration = t.EndTime - t.StartTime };
        }
        catch (System.Runtime.InteropServices.COMException)
        {
        }
    }

    private readonly SemaphoreSlim _refreshGate = new(1, 1);
    private volatile bool _pending;

    private async Task RefreshAsync()
    {
        // Сессии сыплют событиями пачками; обрабатываем по одному, лишние схлопываются.
        _pending = true;
        if (!await _refreshGate.WaitAsync(0).ConfigureAwait(false)) return;
        try
        {
            while (_pending)
            {
                _pending = false;
                await Task.Delay(120).ConfigureAwait(false);
                await RefreshCoreAsync().ConfigureAwait(false);
            }
        }
        finally
        {
            _refreshGate.Release();
        }
    }

    private async Task RefreshCoreAsync()
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
        var previous = Current;
        var next = new MediaState(
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
        Current = next;
        // Событие — только если поменялось что-то видимое, а не позиция.
        if (previous is null || previous.Title != next.Title || previous.Artist != next.Artist || previous.IsPlaying != next.IsPlaying
            || !ReferenceEquals(previous.Thumbnail, next.Thumbnail) || previous.CanNext != next.CanNext || previous.CanPrevious != next.CanPrevious
            || previous.SourceAppId != next.SourceAppId)
            Changed?.Invoke(this, next);
    }

    public async Task PlayPauseAsync() { if (_session is { } s) await s.TryTogglePlayPauseAsync(); }
    public async Task NextAsync() { if (_session is { } s) await s.TrySkipNextAsync(); }
    public async Task PreviousAsync() { if (_session is { } s) await s.TrySkipPreviousAsync(); }
    public async Task SeekAsync(TimeSpan position) { if (_session is { } s) await s.TryChangePlaybackPositionAsync(position.Ticks); }
}
