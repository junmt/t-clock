using System.ComponentModel;

namespace TClock;

public enum TimerKind { Countdown, Alarm }
public enum TimerState { Running, Paused, Done }

/// <summary>
/// 1件のタイマー/アラーム。残り時間は「終了予定の絶対時刻(EndAt)」を基準に計算するため、
/// スリープ復帰後やUIの間引き描画があってもズレない。
/// </summary>
public class TimerItem : INotifyPropertyChanged
{
    public string Id { get; } = Guid.NewGuid().ToString("N");
    public string Label { get; set; } = "";
    public TimerKind Kind { get; set; }

    /// <summary>カウントダウンの元の長さ。リセットで使用。</summary>
    public TimeSpan Duration { get; set; }

    /// <summary>実行中の終了予定 絶対時刻。</summary>
    public DateTime EndAt { get; set; }

    /// <summary>一時停止中の残り時間。</summary>
    public TimeSpan Remaining { get; set; }

    private TimerState _state = TimerState.Running;
    public TimerState State
    {
        get => _state;
        set
        {
            if (_state == value) return;
            _state = value;
            Notify(nameof(State));
            Notify(nameof(PrimaryGlyph));
            Notify(nameof(PrimaryTip));
            Notify(nameof(IsDone));
            Notify(nameof(CanReset));
        }
    }

    /// <summary>現在の残り時間(実行中は現在時刻から算出)。</summary>
    public TimeSpan RemainingNow =>
        State == TimerState.Running ? Max(TimeSpan.Zero, EndAt - DateTime.Now) : Remaining;

    /// <summary>表示ラベル(アイコン付き)。</summary>
    public string DisplayLabel => (Kind == TimerKind.Alarm ? "⏰ " : "⏱ ") + Label;

    /// <summary>残り時間の表示文字列。</summary>
    public string RemainingText
    {
        get
        {
            if (State == TimerState.Done) return "時間です";
            var t = RemainingNow;
            return t.TotalHours >= 1
                ? $"{(int)t.TotalHours:00}:{t.Minutes:00}:{t.Seconds:00}"
                : $"{t.Minutes:00}:{t.Seconds:00}";
        }
    }

    public string PrimaryGlyph => State switch
    {
        TimerState.Running => "⏸",
        TimerState.Paused => "▶",
        _ => "😴",
    };

    public string PrimaryTip => State switch
    {
        TimerState.Running => "一時停止",
        TimerState.Paused => "再開",
        _ => "スヌーズ",
    };

    public bool IsDone => State == TimerState.Done;
    public bool CanReset => State != TimerState.Done;

    /// <summary>毎tick、残り表示を更新するために発火。</summary>
    public void Tick() => Notify(nameof(RemainingText));

    public event PropertyChangedEventHandler? PropertyChanged;
    private void Notify(string name) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));

    private static TimeSpan Max(TimeSpan a, TimeSpan b) => a > b ? a : b;
}
