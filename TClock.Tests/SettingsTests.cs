using System.Text.Json;
using TClock;

namespace TClock.Tests;

public class SettingsTests
{
    [Fact]
    public void Defaults_AreExpected()
    {
        var s = new Settings();

        Assert.True(s.ShowSeconds);
        Assert.True(s.ShowDate);
        Assert.True(s.Hour24);
        Assert.True(s.AlwaysOnTop);
        Assert.False(s.ClickThrough);
        Assert.Equal(90, s.Opacity);
        Assert.False(s.Compact);
        Assert.Equal(5, s.SnoozeMinutes);
        Assert.True(s.ToastEnabled);
        Assert.True(s.FlashEnabled);
        Assert.False(s.Autostart);
        Assert.Equal("dark", s.Theme);
        Assert.Equal(-1, s.WindowLeft);
        Assert.Equal(-1, s.WindowTop);
    }

    [Fact]
    public void JsonRoundTrip_PreservesAllProperties()
    {
        var original = new Settings
        {
            ShowSeconds = false,
            ShowDate = false,
            Hour24 = false,
            AlwaysOnTop = false,
            ClickThrough = true,
            Opacity = 55,
            Compact = true,
            SnoozeMinutes = 10,
            ToastEnabled = false,
            FlashEnabled = false,
            Autostart = true,
            Theme = "light",
            WindowLeft = 123.5,
            WindowTop = 678.25,
        };

        var json = JsonSerializer.Serialize(original);
        var restored = JsonSerializer.Deserialize<Settings>(json);

        Assert.NotNull(restored);
        Assert.Equal(original.ShowSeconds, restored!.ShowSeconds);
        Assert.Equal(original.ShowDate, restored.ShowDate);
        Assert.Equal(original.Hour24, restored.Hour24);
        Assert.Equal(original.AlwaysOnTop, restored.AlwaysOnTop);
        Assert.Equal(original.ClickThrough, restored.ClickThrough);
        Assert.Equal(original.Opacity, restored.Opacity);
        Assert.Equal(original.Compact, restored.Compact);
        Assert.Equal(original.SnoozeMinutes, restored.SnoozeMinutes);
        Assert.Equal(original.ToastEnabled, restored.ToastEnabled);
        Assert.Equal(original.FlashEnabled, restored.FlashEnabled);
        Assert.Equal(original.Autostart, restored.Autostart);
        Assert.Equal(original.Theme, restored.Theme);
        Assert.Equal(original.WindowLeft, restored.WindowLeft);
        Assert.Equal(original.WindowTop, restored.WindowTop);
    }

    [Fact]
    public void Deserialize_MissingProperties_FallBackToDefaults()
    {
        // 一部プロパティのみ含むJSON（旧バージョン設定ファイルを模擬）
        var restored = JsonSerializer.Deserialize<Settings>("{\"Opacity\":40}");

        Assert.NotNull(restored);
        Assert.Equal(40, restored!.Opacity);
        // 未指定は既定値
        Assert.True(restored.ShowSeconds);
        Assert.Equal(5, restored.SnoozeMinutes);
    }
}
