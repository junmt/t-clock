using System.ComponentModel;
using TClock;

namespace TClock.Tests;

public class TimerItemTests
{
    [Fact]
    public void NewItem_HasUniqueId_AndRunningState()
    {
        var a = new TimerItem();
        var b = new TimerItem();

        Assert.False(string.IsNullOrEmpty(a.Id));
        Assert.NotEqual(a.Id, b.Id);
        Assert.Equal(TimerState.Running, a.State);
    }

    [Theory]
    [InlineData(TimerKind.Countdown, "作業", "⏱ 作業")]
    [InlineData(TimerKind.Alarm, "起床", "⏰ 起床")]
    public void DisplayLabel_PrefixesIconByKind(TimerKind kind, string label, string expected)
    {
        var item = new TimerItem { Kind = kind, Label = label };
        Assert.Equal(expected, item.DisplayLabel);
    }

    [Fact]
    public void RemainingNow_WhenPaused_ReturnsStoredRemaining()
    {
        var item = new TimerItem
        {
            State = TimerState.Paused,
            Remaining = TimeSpan.FromMinutes(3),
            EndAt = DateTime.Now.AddHours(99), // 無視されるべき
        };

        Assert.Equal(TimeSpan.FromMinutes(3), item.RemainingNow);
    }

    [Fact]
    public void RemainingNow_WhenRunning_NeverNegative()
    {
        var item = new TimerItem
        {
            State = TimerState.Running,
            EndAt = DateTime.Now.AddSeconds(-10), // 既に過ぎた終了時刻
        };

        Assert.Equal(TimeSpan.Zero, item.RemainingNow);
    }

    [Fact]
    public void RemainingNow_WhenRunning_ReflectsEndAt()
    {
        var item = new TimerItem
        {
            State = TimerState.Running,
            EndAt = DateTime.Now.AddMinutes(5),
        };

        // 5分先。計算誤差を許容して概ね5分であることを確認。
        Assert.InRange(item.RemainingNow.TotalSeconds, 290, 300);
    }

    [Fact]
    public void RemainingText_WhenDone_ShowsMessage()
    {
        var item = new TimerItem { State = TimerState.Done };
        Assert.Equal("時間です", item.RemainingText);
    }

    [Fact]
    public void RemainingText_UnderOneHour_UsesMinuteSecondFormat()
    {
        var item = new TimerItem
        {
            State = TimerState.Paused,
            Remaining = new TimeSpan(0, 5, 9),
        };

        Assert.Equal("05:09", item.RemainingText);
    }

    [Fact]
    public void RemainingText_OverOneHour_UsesHourFormat()
    {
        var item = new TimerItem
        {
            State = TimerState.Paused,
            Remaining = new TimeSpan(2, 3, 4),
        };

        Assert.Equal("02:03:04", item.RemainingText);
    }

    [Theory]
    [InlineData(TimerState.Running, "⏸", "一時停止")]
    [InlineData(TimerState.Paused, "▶", "再開")]
    [InlineData(TimerState.Done, "😴", "スヌーズ")]
    public void PrimaryGlyphAndTip_MatchState(TimerState state, string glyph, string tip)
    {
        var item = new TimerItem { State = state };
        Assert.Equal(glyph, item.PrimaryGlyph);
        Assert.Equal(tip, item.PrimaryTip);
    }

    [Theory]
    [InlineData(TimerState.Running, false, true)]
    [InlineData(TimerState.Paused, false, true)]
    [InlineData(TimerState.Done, true, false)]
    public void IsDoneAndCanReset_MatchState(TimerState state, bool isDone, bool canReset)
    {
        var item = new TimerItem { State = state };
        Assert.Equal(isDone, item.IsDone);
        Assert.Equal(canReset, item.CanReset);
    }

    [Fact]
    public void StateChange_RaisesExpectedPropertyChanged()
    {
        var item = new TimerItem { State = TimerState.Running };
        var changed = new List<string>();
        item.PropertyChanged += (_, e) => changed.Add(e.PropertyName!);

        item.State = TimerState.Paused;

        Assert.Contains(nameof(TimerItem.State), changed);
        Assert.Contains(nameof(TimerItem.PrimaryGlyph), changed);
        Assert.Contains(nameof(TimerItem.PrimaryTip), changed);
        Assert.Contains(nameof(TimerItem.IsDone), changed);
        Assert.Contains(nameof(TimerItem.CanReset), changed);
    }

    [Fact]
    public void StateChange_ToSameValue_DoesNotRaise()
    {
        var item = new TimerItem { State = TimerState.Running };
        var raised = false;
        item.PropertyChanged += (_, _) => raised = true;

        item.State = TimerState.Running;

        Assert.False(raised);
    }

    [Fact]
    public void Tick_RaisesRemainingText()
    {
        var item = new TimerItem();
        string? changed = null;
        item.PropertyChanged += (_, e) => changed = e.PropertyName;

        item.Tick();

        Assert.Equal(nameof(TimerItem.RemainingText), changed);
    }
}
