using TClock;
using Color = System.Windows.Media.Color;
using ColorConverter = System.Windows.Media.ColorConverter;

namespace TClock.Tests;

public class ThemeTests
{
    [Fact]
    public void All_ContainsRequiredDefaultThemes()
    {
        var ids = Theme.All.Select(t => t.Id).ToList();
        Assert.Contains("dark", ids);   // 黒背景×白文字
        Assert.Contains("light", ids);  // 白背景×黒文字
    }

    [Fact]
    public void All_FirstIsDark_AndIsDefault()
    {
        Assert.Equal("dark", Theme.All[0].Id);
        Assert.Equal(Theme.All[0], Theme.ById("dark"));
    }

    [Fact]
    public void ById_UnknownId_FallsBackToDefault()
    {
        Assert.Equal(Theme.All[0], Theme.ById("no-such-theme"));
        Assert.Equal(Theme.All[0], Theme.ById(null));
    }

    [Fact]
    public void Next_CyclesThroughAllAndWrapsAround()
    {
        var seen = new List<string>();
        var id = Theme.All[0].Id;
        for (int i = 0; i < Theme.All.Count; i++)
        {
            id = Theme.Next(id).Id;
            seen.Add(id);
        }

        // 全テーマを1周し、最後は先頭へ戻る
        Assert.Equal(Theme.All.Count, seen.Distinct().Count());
        Assert.Equal(Theme.All[0].Id, id);
    }

    [Fact]
    public void Next_UnknownId_ReturnsSecondTheme()
    {
        // 不明Idは索引0扱い → 次は索引1
        Assert.Equal(Theme.All[1], Theme.Next("no-such-theme"));
    }

    [Theory]
    [InlineData("dark")]
    [InlineData("light")]
    [InlineData("midnight")]
    [InlineData("sepia")]
    [InlineData("forest")]
    public void EveryTheme_HasParseableColors(string id)
    {
        var t = Theme.ById(id);
        foreach (var hex in new[] { t.Background, t.Foreground, t.Muted, t.Chip, t.Border })
        {
            var parsed = ColorConverter.ConvertFromString(hex);
            Assert.IsType<Color>(parsed);
        }
    }

    [Fact]
    public void DarkAndLight_HaveContrastingForegrounds()
    {
        var dark = Theme.ById("dark");
        var light = Theme.ById("light");

        // ダークは明るい文字、ライトは暗い文字
        Assert.True(Luminance(dark.Foreground) > 0.6);
        Assert.True(Luminance(light.Foreground) < 0.4);
    }

    private static double Luminance(string hex)
    {
        var c = (Color)ColorConverter.ConvertFromString(hex)!;
        return (0.299 * c.R + 0.587 * c.G + 0.114 * c.B) / 255.0;
    }
}
