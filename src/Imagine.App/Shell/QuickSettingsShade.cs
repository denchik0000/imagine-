using System.Runtime.InteropServices;
using System.Runtime.InteropServices.WindowsRuntime;
using Imagine.App.Ui;
using Imagine.Color;
using Imagine.Core.Services;
using Imagine.Core.Shades;
using Imagine.Core.Theming;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Imaging;
using Windows.Storage.Streams;

namespace Imagine.App.Shell;

/// <summary>
/// Правая шторка: плитки (Wi‑Fi, Bluetooth, режимы, палитра), яркость, громкость с микшером приложений,
/// карточка медиа и быстрый выбор цвета палитры с обоев.
/// </summary>
public sealed class QuickSettingsShade : ShadeWindow
{
    private readonly Grid _tiles = new() { RowSpacing = 10, ColumnSpacing = 10 };
    private readonly StackPanel _sliders = new() { Spacing = 10 };
    private readonly StackPanel _media = new();
    private readonly StackPanel _palette = new() { Spacing = 8 };
    private readonly TextBlock _date = Ui.Ui.Text("", 14, 600);
    private readonly TextBlock _battery = Ui.Ui.Text("", 13, 500);
    private readonly Dictionary<QuickTileId, bool> _state = [];
    private bool _suppressSliders;
    private long _lastUserSlide;

    public QuickSettingsShade(AppServices services) : base(services, ShadeKind.QuickSettings)
    {
        Title = "Imagine — быстрые настройки";
        _date.Foreground = Palette.Get(ColorRole.OnSurface);
        _battery.Foreground = Palette.Get(ColorRole.OnSurfaceVariant);

        var gear = new Button { Content = Ui.Ui.Icon(Glyphs.Settings, 16), Padding = new Thickness(8), CornerRadius = new CornerRadius(16), Background = new SolidColorBrush(Microsoft.UI.Colors.Transparent), BorderThickness = new Thickness(0) };
        ToolTipService.SetToolTip(gear, "Настройки Imagine");
        gear.Click += (_, _) => { Close(true); App.Current.ShowSettings(); };
        var header = Ui.Ui.Grid("*,Auto,Auto", _date, _battery, gear);
        _battery.VerticalAlignment = VerticalAlignment.Center;

        var content = Ui.Ui.VStack(14, header, _tiles, _sliders, _media, _palette);
        var scroll = new ScrollViewer { Content = content, VerticalScrollBarVisibility = ScrollBarVisibility.Auto, Padding = new Thickness(0, 0, 6, 0) };
        SetBody(new Border { Padding = new Thickness(18, 20, 12, 0), Child = scroll });

        services.Media.Changed += (_, _) => DispatcherQueue.Post(() => { if (IsOpen) _ = RenderMediaAsync(); });
        // Громкость меняется и от нашего же ползунка: пока пользователь его тянет, панель не пересобираем,
        // иначе ползунок пересоздаётся под пальцем на каждом шаге.
        services.Audio.Changed += (_, _) => DispatcherQueue.Post(() =>
        {
            if (IsOpen && !_suppressSliders && Environment.TickCount64 - _lastUserSlide > 1500) RenderSliders();
        });
    }

    protected override async Task RefreshAsync()
    {
        _date.Text = Imagine.Core.Clock.ClockFormatter.LongDate(DateTime.Now);
        _battery.Text = Services.Power.GetBattery() is { } b ? $"{b.Percent}%{(b.IsCharging ? " ⚡" : "")}" : "";
        await LoadStatesAsync();
        RenderTiles();
        RenderSliders();
        await RenderMediaAsync();
        RenderPalette();
    }

    private async Task LoadStatesAsync()
    {
        var s = Services.Current;
        _state[QuickTileId.Wifi] = await Services.Radios.GetWifiStateAsync() ?? false;
        _state[QuickTileId.Bluetooth] = await Services.Radios.GetBluetoothStateAsync() ?? false;
        _state[QuickTileId.DarkMode] = Palette.Current?.IsDark ?? false;
        _state[QuickTileId.Focus] = s.Notifications.Focus.IsActive(DateTimeOffset.Now);
        _state[QuickTileId.BatterySaver] = Services.Power.GetBattery()?.BatterySaver ?? false;
        _state[QuickTileId.MicMute] = SafeGet(() => Services.Audio.IsMicrophoneMuted);
        _state[QuickTileId.Mute] = SafeGet(() => Services.Audio.IsMuted);
        _state[QuickTileId.KeepAwake] = Services.Power.IsKeepAwake;
        _state.TryAdd(QuickTileId.Airplane, false);
    }

    private static bool SafeGet(Func<bool> f)
    {
        try { return f(); }
        catch (COMException) { return false; }
        catch (InvalidOperationException) { return false; }
    }

    private void RenderTiles()
    {
        var s = Services.Current.Shades;
        var layout = new QuickTileLayout(s.Tiles, s.TileColumns);
        _tiles.Children.Clear();
        _tiles.ColumnDefinitions.Clear();
        _tiles.RowDefinitions.Clear();
        for (int c = 0; c < layout.Columns; c++) _tiles.ColumnDefinitions.Add(new ColumnDefinition());
        for (int r = 0; r < layout.RowCount; r++) _tiles.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        foreach (var (slot, row, col, span) in layout.Arrange())
        {
            var tile = Tile(slot);
            Grid.SetRow(tile, row);
            Grid.SetColumn(tile, col);
            Grid.SetColumnSpan(tile, span);
            _tiles.Children.Add(tile);
        }
    }

    private FrameworkElement Tile(QuickTileSlot slot)
    {
        var info = QuickTileCatalog.Get(slot.Id);
        bool on = info.IsToggle && _state.TryGetValue(slot.Id, out var v) && v;
        var bg = on ? Palette.Get(ColorRole.Primary) : Palette.Get(ColorRole.SurfaceContainerHighest, 0.6);
        var fg = on ? Palette.Get(ColorRole.OnPrimary) : Palette.Get(ColorRole.OnSurface);
        var sub = Subtitle(slot.Id, on);

        if (slot.Size == QuickTileSize.Wide)
        {
            var button = new Button
            {
                HorizontalAlignment = HorizontalAlignment.Stretch,
                HorizontalContentAlignment = HorizontalAlignment.Left,
                Height = 64,
                CornerRadius = new CornerRadius(32),
                Padding = new Thickness(8, 0, 14, 0),
                Background = bg,
                BorderThickness = new Thickness(0),
                Content = Ui.Ui.HStack(10,
                    new Border
                    {
                        Width = 46,
                        Height = 46,
                        CornerRadius = new CornerRadius(23),
                        Background = on ? Palette.Get(ColorRole.OnPrimary, 0.18) : Palette.Get(ColorRole.SurfaceContainerHigh),
                        Child = Ui.Ui.Icon(info.Glyph, 18, fg).With(i => { i.HorizontalAlignment = HorizontalAlignment.Center; i.VerticalAlignment = VerticalAlignment.Center; }),
                    },
                    Ui.Ui.VStack(0, Ui.Ui.Text(info.Label, 14, 600, fg), Ui.Ui.Text(sub, 12, 400, fg, 0.8)).With(p => p.VerticalAlignment = VerticalAlignment.Center)),
            };
            button.Click += async (_, _) => await ActivateAsync(slot.Id);
            ToolTipService.SetToolTip(button, info.Label);
            return button;
        }

        var circle = new Button
        {
            Width = 58,
            Height = 58,
            CornerRadius = new CornerRadius(29),
            Padding = new Thickness(0),
            Background = bg,
            BorderThickness = new Thickness(0),
            HorizontalAlignment = HorizontalAlignment.Center,
            Content = Ui.Ui.Icon(info.Glyph, 20, fg),
        };
        circle.Click += async (_, _) => await ActivateAsync(slot.Id);
        ToolTipService.SetToolTip(circle, string.IsNullOrEmpty(sub) ? info.Label : $"{info.Label}: {sub}");
        var label = Ui.Ui.Text(info.Label, 11, 400, Palette.Get(ColorRole.OnSurfaceVariant)).With(t =>
        {
            t.HorizontalAlignment = HorizontalAlignment.Center;
            t.TextAlignment = TextAlignment.Center;
            t.MaxLines = 2;
            t.TextTrimming = TextTrimming.CharacterEllipsis;
        });
        return Ui.Ui.VStack(6, circle, label);
    }

    private string Subtitle(QuickTileId id, bool on) => id switch
    {
        QuickTileId.PaletteStyle => QuickTileCatalog.VariantName(Services.Current.Theme.Variant),
        QuickTileId.PaletteShuffle => Palette.Current is { } t ? ColorUtils.ToHex(t.Seed) : "",
        QuickTileId.NightLight or QuickTileId.Hotspot => "Параметры",
        _ when QuickTileCatalog.Get(id).IsToggle => on ? "Вкл." : "Выкл.",
        _ => "",
    };

    private async Task ActivateAsync(QuickTileId id)
    {
        var a = Services.Actions;
        bool on = _state.TryGetValue(id, out var v) && v;
        try
        {
            switch (id)
            {
                case QuickTileId.Wifi:
                    if (await Services.Radios.SetWifiAsync(!on)) _state[id] = !on;
                    else a.OpenUri("ms-settings:network-wifi");
                    break;
                case QuickTileId.Bluetooth:
                    if (await Services.Radios.SetBluetoothAsync(!on)) _state[id] = !on;
                    else a.OpenUri("ms-settings:bluetooth");
                    break;
                case QuickTileId.Airplane:
                    if (await Services.Radios.SetAirplaneModeAsync(!on)) _state[id] = !on;
                    else a.OpenUri("ms-settings:network-airplanemode");
                    break;
                case QuickTileId.DarkMode:
                    _state[id] = !on;
                    Services.ThemeSettingsChanged(s => s with { Theme = s.Theme with { Mode = on ? ThemeModePreference.Light : ThemeModePreference.Dark } });
                    break;
                case QuickTileId.NightLight: Close(true); a.OpenUri("ms-settings:nightlight"); return;
                case QuickTileId.Hotspot: Close(true); a.OpenUri("ms-settings:network-mobilehotspot"); return;
                case QuickTileId.Focus:
                    _state[id] = !on;
                    Services.UpdateSettings(s => s with { Notifications = s.Notifications with { Focus = s.Notifications.Focus with { ManualOn = !on } } });
                    break;
                case QuickTileId.BatterySaver:
                    if (Services.Power.SetBatterySaver(!on)) _state[id] = !on;
                    else a.OpenUri("ms-settings:batterysaver");
                    break;
                case QuickTileId.MicMute:
                    Services.Audio.IsMicrophoneMuted = !on;
                    _state[id] = !on;
                    break;
                case QuickTileId.Mute:
                    Services.Audio.IsMuted = !on;
                    _state[id] = !on;
                    break;
                case QuickTileId.KeepAwake:
                    Services.Power.SetKeepAwake(!on);
                    _state[id] = !on;
                    break;
                case QuickTileId.Screenshot:
                    Close(false);
                    await Task.Delay(250);
                    a.TakeScreenshot();
                    return;
                case QuickTileId.ColorPicker:
                    Close(false);
                    App.Current.Shell.BeginColorPick();
                    return;
                case QuickTileId.Calculator: Close(true); a.OpenUri("calculator:"); return;
                case QuickTileId.Projection: Close(true); a.OpenUri("ms-settings-displays-topology:"); return;
                case QuickTileId.Settings: Close(true); App.Current.ShowSettings(); return;
                case QuickTileId.PaletteStyle:
                {
                    var all = Enum.GetValues<Variant>();
                    var next = all[(Array.IndexOf(all, Services.Current.Theme.Variant) + 1) % all.Length];
                    Services.ThemeSettingsChanged(s => s with { Theme = s.Theme with { Variant = next } });
                    App.Current.Shell.Island.ShowMessage(Glyphs.Palette, "Стиль палитры: " + QuickTileCatalog.VariantName(next));
                    break;
                }
                case QuickTileId.PaletteShuffle:
                {
                    int count = Services.Engine.CurrentAnalysis?.SeedCandidates.Count ?? 1;
                    Services.ThemeSettingsChanged(s => s with { Theme = s.Theme with { SeedSource = SeedSource.Wallpaper, WallpaperSeedIndex = (s.Theme.WallpaperSeedIndex + 1) % Math.Max(1, count) } });
                    break;
                }
                case QuickTileId.NextWallpaper: await Services.NextWallpaperAsync(); break;
                case QuickTileId.ClipboardHistory: Close(false); await Task.Delay(200); a.OpenClipboardHistory(); return;
                case QuickTileId.EmojiPanel: Close(false); await Task.Delay(200); a.OpenEmojiPanel(); return;
                case QuickTileId.LockScreen: Close(false); a.LockWorkstation(); return;
            }
        }
        catch (Exception ex)
        {
            Log.Error("Плитка " + id, ex);
        }
        RenderTiles();
    }

    private void RenderSliders()
    {
        var s = Services.Current.Shades;
        _sliders.Children.Clear();
        _suppressSliders = true;
        try
        {
            if (s.ShowBrightness)
            {
                foreach (var d in Services.Brightness.GetDisplays())
                {
                    var id = d.MonitorId;
                    _sliders.Children.Add(PillSlider(Glyphs.Brightness, d.IsInternal ? "Яркость" : d.Name, d.Percent, v => Services.Brightness.SetBrightness(id, (int)v)));
                }
            }
            if (s.ShowVolume)
            {
                float volume = 0;
                try { volume = Services.Audio.MasterVolume; }
                catch (COMException) { }
                _sliders.Children.Add(PillSlider(Glyphs.Volume, "Громкость", volume * 100, v => Services.Audio.MasterVolume = (float)(v / 100)));

                var devices = Services.Audio.GetOutputDevices();
                if (devices.Count > 1)
                {
                    var combo = Ui.Ui.Combo(devices.Select(d => (d.Id, d.Name)), devices.FirstOrDefault(d => d.IsDefault)?.Id ?? devices[0].Id, id =>
                    {
                        if (!Services.Audio.TrySetDefaultOutput(id)) Services.Actions.OpenUri("ms-settings:sound");
                    }, 0);
                    combo.HorizontalAlignment = HorizontalAlignment.Stretch;
                    combo.CornerRadius = new CornerRadius(14);
                    _sliders.Children.Add(combo);
                }

                if (s.ShowPerAppVolume)
                {
                    var sessions = Services.Audio.GetSessions();
                    if (sessions.Count > 0)
                    {
                        var mixer = new StackPanel { Spacing = 6 };
                        foreach (var session in sessions)
                        {
                            var sid = session.Id;
                            mixer.Children.Add(Ui.Ui.Grid("110,*",
                                Ui.Ui.Text(session.DisplayName, 12, 400, Palette.Get(ColorRole.OnSurfaceVariant)).With(t => { t.TextTrimming = TextTrimming.CharacterEllipsis; t.TextWrapping = TextWrapping.NoWrap; t.VerticalAlignment = VerticalAlignment.Center; }),
                                Ui.Ui.Slider(0, 100, session.Volume * 100, v => Services.Audio.SetSessionVolume(sid, (float)(v / 100)), width: double.NaN).With(sl => sl.HorizontalAlignment = HorizontalAlignment.Stretch)));
                        }
                        _sliders.Children.Add(new Expander
                        {
                            Header = Ui.Ui.HStack(8, Ui.Ui.Icon(Glyphs.Music, 14), Ui.Ui.Text($"Приложения ({sessions.Count})", 13)),
                            Content = mixer,
                            HorizontalAlignment = HorizontalAlignment.Stretch,
                            HorizontalContentAlignment = HorizontalAlignment.Stretch,
                        });
                    }
                }
            }
        }
        catch (Exception ex)
        {
            Log.Error("Ползунки шторки", ex);
        }
        finally
        {
            _suppressSliders = false;
        }
    }

    /// <summary>Толстый ползунок-«пилюля», как в ColorOS: заливка цветом акцента, значок внутри.</summary>
    private FrameworkElement PillSlider(string glyph, string label, double value, Action<double> onChanged)
    {
        var fill = new Border { CornerRadius = new CornerRadius(22), Background = Palette.Get(ColorRole.Primary), HorizontalAlignment = HorizontalAlignment.Left };
        var track = new Grid { Height = 44, CornerRadius = new CornerRadius(22), Background = Palette.Get(ColorRole.SurfaceContainerHighest, 0.6) };
        var icon = Ui.Ui.Icon(glyph, 16, Palette.Get(ColorRole.OnPrimary)).With(i => { i.HorizontalAlignment = HorizontalAlignment.Left; i.Margin = new Thickness(16, 0, 0, 0); i.VerticalAlignment = VerticalAlignment.Center; });
        var text = Ui.Ui.Text(label, 12, 500, Palette.Get(ColorRole.OnSurfaceVariant)).With(t => { t.HorizontalAlignment = HorizontalAlignment.Right; t.VerticalAlignment = VerticalAlignment.Center; t.Margin = new Thickness(0, 0, 16, 0); });
        track.Children.Add(fill);
        track.Children.Add(icon);
        track.Children.Add(text);
        double current = Math.Clamp(value, 0, 100);
        void Layout() => fill.Width = Math.Max(44, track.ActualWidth * current / 100);
        track.SizeChanged += (_, _) => Layout();

        void SetFromX(double x)
        {
            _lastUserSlide = Environment.TickCount64;
            current = Math.Clamp(x / Math.Max(1, track.ActualWidth) * 100, 0, 100);
            Layout();
            onChanged(current);
        }
        bool down = false;
        track.PointerPressed += (_, e) => { down = true; track.CapturePointer(e.Pointer); SetFromX(e.GetCurrentPoint(track).Position.X); };
        track.PointerMoved += (_, e) => { if (down) SetFromX(e.GetCurrentPoint(track).Position.X); };
        track.PointerReleased += (_, e) => { down = false; track.ReleasePointerCapture(e.Pointer); };
        track.PointerWheelChanged += (_, e) => { current = Math.Clamp(current + Math.Sign(e.GetCurrentPoint(track).Properties.MouseWheelDelta) * 5, 0, 100); Layout(); onChanged(current); };
        return track;
    }

    private async Task RenderMediaAsync()
    {
        _media.Children.Clear();
        if (!Services.Current.Shades.ShowMediaCard || Services.Media.Current is not { } m) return;

        var art = new Border { Width = 72, Height = 72, CornerRadius = new CornerRadius(16), Background = Palette.Get(ColorRole.TertiaryContainer) };
        if (m.Thumbnail is { Length: > 0 } bytes)
        {
            try
            {
                var bmp = new BitmapImage();
                using var stream = new InMemoryRandomAccessStream();
                await stream.WriteAsync(bytes.AsBuffer());
                stream.Seek(0);
                await bmp.SetSourceAsync(stream);
                art.Child = new Image { Source = bmp, Stretch = Stretch.UniformToFill };
            }
            catch (Exception ex) when (ex is COMException or ArgumentException)
            {
                art.Child = Ui.Ui.Icon(Glyphs.Music, 26, Palette.Get(ColorRole.OnTertiaryContainer));
            }
        }
        else art.Child = Ui.Ui.Icon(Glyphs.Music, 26, Palette.Get(ColorRole.OnTertiaryContainer));

        Button Ctl(string glyph, Func<Task> action, bool primary = false, bool enabled = true)
        {
            var b = new Button
            {
                Width = primary ? 48 : 38,
                Height = primary ? 48 : 38,
                CornerRadius = new CornerRadius(primary ? 24 : 19),
                Padding = new Thickness(0),
                IsEnabled = enabled,
                BorderThickness = new Thickness(0),
                Background = primary ? Palette.Get(ColorRole.Primary) : new SolidColorBrush(Microsoft.UI.Colors.Transparent),
                Content = Ui.Ui.Icon(glyph, primary ? 18 : 15, primary ? Palette.Get(ColorRole.OnPrimary) : Palette.Get(ColorRole.OnSurface)),
            };
            b.Click += async (_, _) => { await action(); };
            return b;
        }

        var progress = new ProgressBar
        {
            Minimum = 0,
            Maximum = Math.Max(1, m.Duration.TotalSeconds),
            Value = m.Position.TotalSeconds,
            Foreground = Palette.Get(ColorRole.Primary),
            Visibility = m.Duration > TimeSpan.Zero ? Visibility.Visible : Visibility.Collapsed,
        };
        var info = Ui.Ui.VStack(2,
            Ui.Ui.Text(m.Title, 15, 600, Palette.Get(ColorRole.OnSurface)).With(t => { t.TextTrimming = TextTrimming.CharacterEllipsis; t.TextWrapping = TextWrapping.NoWrap; }),
            Ui.Ui.Text(m.Artist, 13, 400, Palette.Get(ColorRole.OnSurfaceVariant)).With(t => { t.TextTrimming = TextTrimming.CharacterEllipsis; t.TextWrapping = TextWrapping.NoWrap; }),
            Ui.Ui.HStack(6,
                Ctl(Glyphs.Previous, Services.Media.PreviousAsync, enabled: m.CanPrevious),
                Ctl(m.IsPlaying ? Glyphs.Pause : Glyphs.Play, Services.Media.PlayPauseAsync, primary: true),
                Ctl(Glyphs.Next, Services.Media.NextAsync, enabled: m.CanNext)).With(p => p.Margin = new Thickness(0, 6, 0, 0)));
        var row = Ui.Ui.Grid("Auto,*", art, info);
        _media.Children.Add(SectionTitle("Сейчас играет"));
        _media.Children.Add(Card(Ui.Ui.VStack(10, row, progress), 24, 14));
    }

    private void RenderPalette()
    {
        _palette.Children.Clear();
        var analysis = Services.Engine.CurrentAnalysis;
        if (analysis is null || analysis.SeedCandidates.Count == 0) return;
        _palette.Children.Add(SectionTitle("Цвет с обоев"));
        var row = Ui.Ui.HStack(10);
        var theme = Services.Current.Theme;
        for (int i = 0; i < analysis.SeedCandidates.Count; i++)
        {
            int index = i;
            bool selected = theme.SeedSource == SeedSource.Wallpaper && theme.WallpaperSeedIndex == i;
            var dot = Swatches.Dot(analysis.SeedCandidates[i], 36, selected);
            var b = new Button { Content = dot, Padding = new Thickness(0), Background = new SolidColorBrush(Microsoft.UI.Colors.Transparent), BorderThickness = new Thickness(0), CornerRadius = new CornerRadius(18) };
            ToolTipService.SetToolTip(b, ColorUtils.ToHex(analysis.SeedCandidates[i]));
            b.Click += (_, _) =>
            {
                Services.ThemeSettingsChanged(s => s with { Theme = s.Theme with { SeedSource = SeedSource.Wallpaper, WallpaperSeedIndex = index } });
                RenderPalette();
            };
            row.Children.Add(b);
        }
        _palette.Children.Add(row);
    }
}
