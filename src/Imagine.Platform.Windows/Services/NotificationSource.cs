using Imagine.Core.Notifications;
using Imagine.Core.Services;
using Windows.Foundation;
using Windows.UI.Notifications;
using Windows.UI.Notifications.Management;

namespace Imagine.Platform.Services;

/// <summary>
/// Чтение уведомлений Windows через UserNotificationListener. Нужна идентичность пакета (MSIX)
/// и одно разрешение пользователя. Кнопки внутри чужих уведомлений нажать нельзя — так устроен API.
/// </summary>
public sealed class NotificationSource : INotificationSource, IDisposable
{
    private readonly string _iconCache;
    private readonly Timer _poll;
    private UserNotificationListener? _listener;
    private bool _eventsWork;
    private HashSet<uint> _lastIds = [];

    public NotificationSource(string iconCacheDirectory)
    {
        _iconCache = iconCacheDirectory;
        Directory.CreateDirectory(_iconCache);
        // Событие NotificationChanged работает не во всех конфигурациях, поэтому есть запасной опрос.
        _poll = new Timer(async _ => await PollAsync().ConfigureAwait(false), null, Timeout.Infinite, Timeout.Infinite);
    }

    public event EventHandler? Changed;

    public async Task<NotificationAccess> RequestAccessAsync()
    {
        if (!Interop.NativeMethods.HasPackageIdentity()) return NotificationAccess.Unsupported;
        try
        {
            _listener = UserNotificationListener.Current;
            var status = await _listener.RequestAccessAsync();
            if (status != UserNotificationListenerAccessStatus.Allowed) return NotificationAccess.Denied;
            try
            {
                _listener.NotificationChanged += (_, _) => Changed?.Invoke(this, EventArgs.Empty);
                _eventsWork = true;
            }
            catch (Exception ex) when (ex is System.Runtime.InteropServices.COMException or UnauthorizedAccessException)
            {
                _eventsWork = false;
            }
            _poll.Change(TimeSpan.FromSeconds(_eventsWork ? 10 : 2), TimeSpan.FromSeconds(_eventsWork ? 10 : 2));
            return NotificationAccess.Allowed;
        }
        catch (Exception ex) when (ex is System.Runtime.InteropServices.COMException or UnauthorizedAccessException or TypeLoadException)
        {
            return NotificationAccess.Unsupported;
        }
    }

    private async Task PollAsync()
    {
        try
        {
            var items = await GetAllAsync().ConfigureAwait(false);
            var ids = items.Select(i => i.Id).ToHashSet();
            if (!ids.SetEquals(_lastIds))
            {
                _lastIds = ids;
                Changed?.Invoke(this, EventArgs.Empty);
            }
        }
        catch (Exception ex) when (ex is System.Runtime.InteropServices.COMException or InvalidOperationException) { }
    }

    public async Task<IReadOnlyList<NotificationItem>> GetAllAsync()
    {
        if (_listener is null) return [];
        var raw = await _listener.GetNotificationsAsync(NotificationKinds.Toast);
        var result = new List<NotificationItem>(raw.Count);
        foreach (var n in raw)
        {
            string appName = "Приложение", appId = "unknown";
            string? icon = null;
            try
            {
                appName = n.AppInfo?.DisplayInfo?.DisplayName ?? appName;
                appId = n.AppInfo?.AppUserModelId ?? n.AppInfo?.Id ?? appName;
                icon = await CacheIconAsync(n, appId).ConfigureAwait(false);
            }
            catch (Exception ex) when (ex is System.Runtime.InteropServices.COMException or UnauthorizedAccessException or IOException) { }

            var binding = n.Notification?.Visual?.GetBinding(KnownNotificationBindings.ToastGeneric);
            var texts = binding?.GetTextElements().Select(t => t.Text).Where(t => !string.IsNullOrWhiteSpace(t)).ToList() ?? [];
            result.Add(new NotificationItem
            {
                Id = n.Id,
                AppId = appId,
                AppName = appName,
                Title = texts.FirstOrDefault() ?? "",
                Body = string.Join("\n", texts.Skip(1)),
                Timestamp = n.CreationTime,
                AppIconPath = icon,
            });
        }
        return result;
    }

    private async Task<string?> CacheIconAsync(UserNotification n, string appId)
    {
        var safe = string.Concat(appId.Select(c => char.IsLetterOrDigit(c) ? c : '_'));
        var path = Path.Combine(_iconCache, safe + ".png");
        if (File.Exists(path)) return path;
        var logo = n.AppInfo?.DisplayInfo?.GetLogo(new Size(64, 64));
        if (logo is null) return null;
        using var stream = await logo.OpenReadAsync();
        using var file = File.Create(path);
        await stream.AsStreamForRead().CopyToAsync(file).ConfigureAwait(false);
        return path;
    }

    public void Remove(uint id) => _listener?.RemoveNotification(id);

    public void ClearAll() => _listener?.ClearNotifications();

    public void Dispose() => _poll.Dispose();
}
