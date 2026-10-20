using System.Collections.ObjectModel;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Interop;
using System.Windows.Threading;
using Microsoft.Win32;
using Drawing = System.Drawing;
using WForms = System.Windows.Forms;

namespace TClock;

public partial class MainWindow : Window
{
    private readonly Settings _settings;
    private readonly ObservableCollection<TimerItem> _timers = new();
    private readonly DispatcherTimer _ticker = new() { Interval = TimeSpan.FromMilliseconds(250) };

    private WForms.NotifyIcon? _tray;
    private SolidColorBrush _chipBrush = new(Color.FromArgb(0x26, 0xFF, 0xFF, 0xFF));
    private readonly SolidColorBrush _activeBrush = new(Color.FromArgb(0x66, 0x4D, 0xA3, 0xFF));
    private bool _hovered;
    private bool _reallyExit;

    public MainWindow()
    {
        // InitializeComponent() 中に XAML の Slider 初期値設定で Opacity_Changed が発火するため、
        // それより前に _settings を読み込んでおく（さもないと NullReferenceException でクラッシュする）。
        _settings = Settings.Load();
        InitializeComponent();
        _chipBrush.Freeze();
        _activeBrush.Freeze();

        TimersControl.ItemsSource = _timers;

        _ticker.Tick += Ticker_Tick;
        _ticker.Start();

        Loaded += OnLoaded;
        SourceInitialized += OnSourceInitialized;
        Closing += OnClosing;
    }

    // ================= 起動処理 =================
    private void OnLoaded(object? sender, RoutedEventArgs e)
    {
        ApplySettingsToUi();
        SetupTray();
        UpdateClock();
        PlaceWindow();
        Updater.CleanupOldFiles();
        _ = CheckForUpdateAsync(manual: false);
    }

    private void OnSourceInitialized(object? sender, EventArgs e)
    {
        // クリックスルーはウィンドウハンドル確定後に適用
        SetClickThrough(_settings.ClickThrough);
    }

    private void PlaceWindow()
    {
        var wa = SystemParameters.WorkArea;
        if (_settings.WindowLeft >= 0 && _settings.WindowTop >= 0)
        {
            Left = Math.Min(Math.Max(_settings.WindowLeft, wa.Left), wa.Right - ActualWidth);
            Top = Math.Min(Math.Max(_settings.WindowTop, wa.Top), wa.Bottom - ActualHeight);
        }
        else
        {
            Left = wa.Right - ActualWidth - 12;
            Top = wa.Bottom - ActualHeight - 12;
        }
    }

    private void ApplySettingsToUi()
    {
        // インストーラーで自動起動を登録した場合もあるため、実際の Run キーの状態を正とする
        _settings.Autostart = IsAutostartRegistered();
        Opacity = Math.Max(20, _settings.Opacity) / 100.0;
        OpacitySlider.Value = _settings.Opacity;
        Topmost = _settings.AlwaysOnTop;
        ApplyTheme();
        ApplyCompact();
        MarkAll();
    }

    private void MarkAll()
    {
        Mark(TopBtn, _settings.AlwaysOnTop);
        Mark(ThroughBtn, _settings.ClickThrough);
        Mark(SecondsBtn, _settings.ShowSeconds);
        Mark(CompactBtn, _settings.Compact);
        Mark(AutostartBtn, _settings.Autostart);
    }

    // ================= 時計 & タイマー tick =================
    private void Ticker_Tick(object? sender, EventArgs e)
    {
        UpdateClock();

        var now = DateTime.Now;
        // 反復中に削除されないようスナップショットで走査
        foreach (var t in _timers.ToArray())
        {
            if (t.State != TimerState.Running) continue;
            if (now >= t.EndAt)
            {
                t.Remaining = TimeSpan.Zero;
                t.State = TimerState.Done;
                OnTimerFire(t);
            }
            else
            {
                t.Tick();
            }
        }
    }

    private void UpdateClock()
    {
        var now = DateTime.Now;
        string time;
        if (_settings.Hour24)
        {
            time = _settings.ShowSeconds ? now.ToString("HH:mm:ss") : now.ToString("HH:mm");
        }
        else
        {
            time = _settings.ShowSeconds ? now.ToString("h:mm:ss tt") : now.ToString("h:mm tt");
        }
        TimeText.Text = time;

        if (_settings.ShowDate)
        {
            string[] wd = { "日", "月", "火", "水", "木", "金", "土" };
            DateText.Text = $"{now:yyyy/MM/dd} ({wd[(int)now.DayOfWeek]})";
        }
    }

    // ================= 通知(発火) =================
    private void OnTimerFire(TimerItem t)
    {
        if (_settings.FlashEnabled) Flash();
        if (_settings.ToastEnabled)
            _tray?.ShowBalloonTip(5000, "t-clock ⏰", $"{t.Label} — 時間です", WForms.ToolTipIcon.Info);

        // 最前面へポップして目立たせる
        Show();
        WindowState = WindowState.Normal;
        Topmost = true;
        Activate();
        if (!_settings.AlwaysOnTop) Topmost = false; // 元がOFFなら戻す
    }

    private void Flash()
    {
        var anim = new DoubleAnimation(0.0, 0.5, TimeSpan.FromMilliseconds(300))
        {
            AutoReverse = true,
            RepeatBehavior = new RepeatBehavior(6),
        };
        FlashOverlay.BeginAnimation(OpacityProperty, anim);
    }

    // ================= タイマー生成 =================
    private void AddCountdown(int minutes, int seconds, string label)
    {
        var dur = TimeSpan.FromSeconds(minutes * 60 + seconds);
        if (dur <= TimeSpan.Zero) return;
        _timers.Add(new TimerItem
        {
            Kind = TimerKind.Countdown,
            Label = string.IsNullOrWhiteSpace(label)
                ? $"{minutes}分{(seconds > 0 ? seconds + "秒" : "")}"
                : label.Trim(),
            Duration = dur,
            EndAt = DateTime.Now + dur,
            Remaining = dur,
            State = TimerState.Running,
        });
    }

    private void AddAlarm(int hour, int minute, string label)
    {
        var endAt = NextOccurrence(hour, minute);
        _timers.Add(new TimerItem
        {
            Kind = TimerKind.Alarm,
            Label = string.IsNullOrWhiteSpace(label) ? $"{hour:00}:{minute:00}" : label.Trim(),
            Duration = TimeSpan.Zero,
            EndAt = endAt,
            Remaining = endAt - DateTime.Now,
            State = TimerState.Running,
        });
    }

    private static DateTime NextOccurrence(int hour, int minute)
    {
        var now = DateTime.Now;
        var target = new DateTime(now.Year, now.Month, now.Day, hour, minute, 0);
        if (target <= now) target = target.AddDays(1);
        return target;
    }

    // ================= タイマー操作 =================
    private void Quick_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button b && int.TryParse(b.Tag?.ToString(), out var min))
            AddCountdown(min, 0, "");
    }

    private void StartCountdown_Click(object sender, RoutedEventArgs e)
    {
        int min = ClampInt(MinInput.Text, 0, 999);
        int sec = ClampInt(SecInput.Text, 0, 59);
        MinInput.Text = min.ToString();
        SecInput.Text = sec.ToString();
        AddCountdown(min, sec, LabelInput.Text);
        LabelInput.Clear();
    }

    private void StartAlarm_Click(object sender, RoutedEventArgs e)
    {
        var parts = AlarmInput.Text.Split(':');
        if (parts.Length == 2 && int.TryParse(parts[0], out var h) && int.TryParse(parts[1], out var m)
            && h is >= 0 and <= 23 && m is >= 0 and <= 59)
        {
            AddAlarm(h, m, LabelInput.Text);
            LabelInput.Clear();
        }
    }

    private void Primary_Click(object sender, RoutedEventArgs e)
    {
        if (((FrameworkElement)sender).DataContext is not TimerItem t) return;
        switch (t.State)
        {
            case TimerState.Running:
                t.Remaining = t.EndAt - DateTime.Now;
                if (t.Remaining < TimeSpan.Zero) t.Remaining = TimeSpan.Zero;
                t.State = TimerState.Paused;
                break;
            case TimerState.Paused:
                t.EndAt = DateTime.Now + t.Remaining;
                t.State = TimerState.Running;
                break;
            case TimerState.Done:
                _timers.Remove(t);
                AddCountdown(_settings.SnoozeMinutes, 0, t.Label);
                return;
        }
        t.Tick();
    }

    private void Reset_Click(object sender, RoutedEventArgs e)
    {
        if (((FrameworkElement)sender).DataContext is not TimerItem t) return;
        if (t.Kind == TimerKind.Countdown)
        {
            t.EndAt = DateTime.Now + t.Duration;
        }
        else
        {
            t.EndAt = NextOccurrence(t.EndAt.Hour, t.EndAt.Minute);
        }
        t.State = TimerState.Running;
        t.Tick();
    }

    private void Remove_Click(object sender, RoutedEventArgs e)
    {
        if (((FrameworkElement)sender).DataContext is TimerItem t)
            _timers.Remove(t);
    }

    // ================= トグル操作 =================
    private void ToggleTop_Click(object sender, RoutedEventArgs e)
    {
        _settings.AlwaysOnTop = !_settings.AlwaysOnTop;
        Topmost = _settings.AlwaysOnTop;
        Mark(TopBtn, _settings.AlwaysOnTop);
        _settings.Save();
    }

    private void ToggleThrough_Click(object sender, RoutedEventArgs e)
    {
        _settings.ClickThrough = !_settings.ClickThrough;
        SetClickThrough(_settings.ClickThrough);
        Mark(ThroughBtn, _settings.ClickThrough);
        _settings.Save();
    }

    private void ToggleSeconds_Click(object sender, RoutedEventArgs e)
    {
        _settings.ShowSeconds = !_settings.ShowSeconds;
        Mark(SecondsBtn, _settings.ShowSeconds);
        UpdateClock();
        _settings.Save();
    }

    private void ToggleCompact_Click(object sender, RoutedEventArgs e)
    {
        _settings.Compact = !_settings.Compact;
        Mark(CompactBtn, _settings.Compact);
        ApplyCompact();
        _settings.Save();
    }

    private void ToggleAutostart_Click(object sender, RoutedEventArgs e)
    {
        _settings.Autostart = !_settings.Autostart;
        SetAutostart(_settings.Autostart);
        Mark(AutostartBtn, _settings.Autostart);
        _settings.Save();
    }

    private void Opacity_Changed(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        // 初期化中(XAML の Value 既定値設定)にも発火する。読み込み完了前は
        // 保存済み設定を XAML 既定値で上書きしないよう無視する。
        if (!IsLoaded) return;
        _settings.Opacity = (int)e.NewValue;
        Opacity = Math.Max(20, _settings.Opacity) / 100.0;
        _settings.Save();
    }

    // ================= 配色テーマ =================
    /// <summary>設定のテーマ(背景色・文字色など)を UI 全体に適用する。</summary>
    private void ApplyTheme()
    {
        var t = Theme.ById(_settings.Theme);

        CardBorder.Background = new SolidColorBrush(ParseColor(t.Background));
        SetThemeBrush("Fg", t.Foreground);
        SetThemeBrush("Muted", t.Muted);
        SetThemeBrush("Chip", t.Chip);
        SetThemeBrush("Border", t.Border);

        // Mark() のオフ状態で使う下地ブラシもテーマに合わせて更新。
        _chipBrush = new SolidColorBrush(ParseColor(t.Chip));
        _chipBrush.Freeze();

        ThemeBtn.ToolTip = $"配色: {t.Name}（クリックで切替）";
    }

    private void CycleTheme_Click(object sender, RoutedEventArgs e)
    {
        _settings.Theme = Theme.Next(_settings.Theme).Id;
        ApplyTheme();
        MarkAll(); // 更新した _chipBrush をボタンへ反映
        _settings.Save();
    }

    /// <summary>
    /// DynamicResource で共有しているブラシを丸ごと差し替える。
    /// XAML のリソースブラシはフリーズ(読み取り専用)されるため Color 変更は不可。
    /// 差し替えることで DynamicResource 参照側すべてに反映される。
    /// </summary>
    private void SetThemeBrush(string key, string hex)
    {
        Resources[key] = new SolidColorBrush(ParseColor(hex));
    }

    /// <summary>"#AARRGGBB" / "#RRGGBB" などを Color に変換。失敗時は既定の暗色。</summary>
    private static Color ParseColor(string? hex)
    {
        try
        {
            if (!string.IsNullOrWhiteSpace(hex) &&
                ColorConverter.ConvertFromString(hex) is Color c)
                return c;
        }
        catch
        {
            // 不正な文字列は既定色へ
        }
        return Color.FromArgb(0xD9, 0x14, 0x16, 0x1C);
    }

    private void Quit_Click(object sender, RoutedEventArgs e) => ExitApp();

    // ================= 表示制御 =================
    private void ApplyCompact()
    {
        bool showExtra = !_settings.Compact || _hovered;
        DateText.Visibility = _settings.ShowDate && showExtra ? Visibility.Visible : Visibility.Collapsed;
        TimersControl.Visibility = showExtra ? Visibility.Visible : Visibility.Collapsed;
        TimeText.FontSize = _settings.Compact && !_hovered ? 28 : 40;
    }

    private void Window_MouseEnter(object sender, MouseEventArgs e)
    {
        _hovered = true;
        PanelRoot.Visibility = Visibility.Visible;
        ApplyCompact();
    }

    private void Window_MouseLeave(object sender, MouseEventArgs e)
    {
        _hovered = false;
        PanelRoot.Visibility = Visibility.Collapsed;
        ApplyCompact();
    }

    private void Window_DragMove(object sender, MouseButtonEventArgs e)
    {
        if (e.ButtonState != MouseButtonState.Pressed) return;
        if (IsInteractive(e.OriginalSource as DependencyObject)) return;
        DragMove(); // マウスを離すまでブロック
        _settings.WindowLeft = Left;
        _settings.WindowTop = Top;
        _settings.Save();
    }

    private static bool IsInteractive(DependencyObject? d)
    {
        while (d != null)
        {
            if (d is ButtonBase or TextBoxBase or Slider or Thumb) return true;
            d = VisualTreeHelper.GetParent(d);
        }
        return false;
    }

    // ================= システムトレイ =================
    private void SetupTray()
    {
        var menu = new WForms.ContextMenuStrip();
        menu.Items.Add("表示", null, (_, _) => ShowFromTray());
        menu.Items.Add("クリックスルー切替", null, (_, _) => ToggleThrough_Click(this, new RoutedEventArgs()));
        menu.Items.Add(new WForms.ToolStripSeparator());
        menu.Items.Add($"更新を確認 (v{Updater.CurrentVersion.ToString(3)})", null, async (_, _) => await CheckForUpdateAsync(manual: true));
        menu.Items.Add(new WForms.ToolStripSeparator());
        menu.Items.Add("終了", null, (_, _) => ExitApp());

        _tray = new WForms.NotifyIcon
        {
            Text = "t-clock",
            Visible = true,
            ContextMenuStrip = menu,
            Icon = LoadTrayIcon(),
        };
        _tray.DoubleClick += (_, _) => ShowFromTray();
        _tray.BalloonTipClicked += async (_, _) =>
        {
            if (_pendingUpdate is { } u) await PromptUpdateAsync(u);
        };
    }

    // ================= アップデート =================
    private UpdateInfo? _pendingUpdate;
    private bool _updating;

    private async Task CheckForUpdateAsync(bool manual)
    {
        if (_updating) return;
        var info = await Updater.CheckAsync();
        if (info == null)
        {
            if (manual)
                MessageBox.Show($"お使いのバージョン (v{Updater.CurrentVersion.ToString(3)}) は最新です。\n" +
                                "（確認できなかった場合はネットワーク接続をご確認ください）",
                                "t-clock", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        _pendingUpdate = info;
        if (manual)
            await PromptUpdateAsync(info);
        else
            _tray?.ShowBalloonTip(8000, "t-clock の更新",
                $"新しいバージョン {info.Tag} があります。クリックして更新します。", WForms.ToolTipIcon.Info);
    }

    private async Task PromptUpdateAsync(UpdateInfo info)
    {
        if (_updating) return;
        if (!Updater.CanApply(info))
        {
            // 自動適用できない (アセット未添付等) 場合はリリースページを開く
            if (MessageBox.Show($"新しいバージョン {info.Tag} があります。リリースページを開きますか？",
                    "t-clock の更新", MessageBoxButton.YesNo, MessageBoxImage.Information) == MessageBoxResult.Yes)
                Updater.OpenPage(info.PageUrl);
            return;
        }

        if (MessageBox.Show($"新しいバージョン {info.Tag} があります（現在 v{Updater.CurrentVersion.ToString(3)}）。\n" +
                            "今すぐ更新しますか？ 更新後に t-clock は自動で再起動します。",
                "t-clock の更新", MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes)
            return;

        _updating = true;
        try
        {
            _tray?.ShowBalloonTip(3000, "t-clock の更新", $"{info.Tag} をダウンロードしています…", WForms.ToolTipIcon.Info);
            var launch = await Updater.PrepareAsync(info);
            ExitApp();      // 設定を保存してから
            launch();       // インストーラー / 新しい exe を起動
        }
        catch (Exception ex)
        {
            _updating = false;
            if (MessageBox.Show($"更新に失敗しました。\n{ex.Message}\n\nリリースページを開きますか？",
                    "t-clock の更新", MessageBoxButton.YesNo, MessageBoxImage.Warning) == MessageBoxResult.Yes)
                Updater.OpenPage(info.PageUrl);
        }
    }

    private static Drawing.Icon LoadTrayIcon()
    {
        try
        {
            var path = Environment.ProcessPath;
            if (path != null)
            {
                var ico = Drawing.Icon.ExtractAssociatedIcon(path);
                if (ico != null) return ico;
            }
        }
        catch { /* fallback */ }
        return Drawing.SystemIcons.Application;
    }

    private void ShowFromTray()
    {
        Show();
        WindowState = WindowState.Normal;
        Topmost = _settings.AlwaysOnTop;
        Activate();
    }

    // ================= クリックスルー / 自動起動 (Win32) =================
    private const int GWL_EXSTYLE = -20;
    private const int WS_EX_TRANSPARENT = 0x20;
    private const int WS_EX_LAYERED = 0x80000;

    [DllImport("user32.dll")] private static extern int GetWindowLong(IntPtr hWnd, int nIndex);
    [DllImport("user32.dll")] private static extern int SetWindowLong(IntPtr hWnd, int nIndex, int dwNewLong);

    private void SetClickThrough(bool on)
    {
        var h = new WindowInteropHelper(this).Handle;
        if (h == IntPtr.Zero) return;
        int ex = GetWindowLong(h, GWL_EXSTYLE);
        ex = on ? ex | WS_EX_TRANSPARENT | WS_EX_LAYERED : ex & ~WS_EX_TRANSPARENT;
        SetWindowLong(h, GWL_EXSTYLE, ex);
    }

    private const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string AppRegName = "t-clock";

    private static bool IsAutostartRegistered()
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(RunKey);
            return key?.GetValue(AppRegName) != null;
        }
        catch { return false; }
    }

    private static void SetAutostart(bool on)
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(RunKey, writable: true);
            if (key == null) return;
            if (on)
                key.SetValue(AppRegName, $"\"{Environment.ProcessPath}\"");
            else if (key.GetValue(AppRegName) != null)
                key.DeleteValue(AppRegName, throwOnMissingValue: false);
        }
        catch { /* 権限等で失敗しても無視 */ }
    }

    // ================= 終了 / util =================
    private void ExitApp()
    {
        _reallyExit = true;
        Close();
    }

    private void OnClosing(object? sender, System.ComponentModel.CancelEventArgs e)
    {
        if (!_reallyExit)
        {
            // 閉じるボタンではトレイへ格納
            e.Cancel = true;
            Hide();
            return;
        }
        _settings.Save();
        _ticker.Stop();
        if (_tray != null)
        {
            _tray.Visible = false;
            _tray.Dispose();
        }
        Application.Current.Shutdown();
    }

    private void Mark(Button b, bool on) => b.Background = on ? _activeBrush : _chipBrush;

    private static int ClampInt(string text, int min, int max)
    {
        if (!int.TryParse(text, out var v)) v = min;
        return Math.Min(max, Math.Max(min, v));
    }
}
