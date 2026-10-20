using TClock;

namespace TClock.Tests;

public class UpdaterTests
{
    private const string LatestJson = """
        {
          "tag_name": "v0.3.0",
          "html_url": "https://github.com/junmt/t-clock/releases/tag/v0.3.0",
          "assets": [
            { "name": "t-clock-0.3.0-setup.exe", "browser_download_url": "https://example.com/setup.exe" },
            { "name": "t-clock.exe", "browser_download_url": "https://example.com/t-clock.exe" },
            { "name": "t-clock-0.3.0-win-x64.zip", "browser_download_url": "https://example.com/t-clock.zip" }
          ]
        }
        """;

    [Theory]
    [InlineData("v1.2.3", 1, 2, 3)]
    [InlineData("1.2.3", 1, 2, 3)]
    [InlineData(" V0.10.0 ", 0, 10, 0)]
    public void TryParseTag_ParsesVersion(string tag, int major, int minor, int build)
    {
        Assert.True(Updater.TryParseTag(tag, out var v));
        Assert.Equal(new Version(major, minor, build), v);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("latest")]
    public void TryParseTag_InvalidTag_ReturnsFalse(string? tag)
    {
        Assert.False(Updater.TryParseTag(tag, out _));
    }

    [Fact]
    public void ParseLatest_NewerRelease_ReturnsInfoWithAssets()
    {
        var info = Updater.ParseLatest(LatestJson, new Version(0, 2, 0, 0));

        Assert.NotNull(info);
        Assert.Equal(new Version(0, 3, 0), info!.Version);
        Assert.Equal("v0.3.0", info.Tag);
        Assert.Equal("https://example.com/setup.exe", info.InstallerUrl);
        Assert.Equal("https://example.com/t-clock.exe", info.ExeUrl);
        Assert.EndsWith("/v0.3.0", info.PageUrl);
    }

    [Fact]
    public void ParseLatest_SameVersion_ReturnsNull()
    {
        // アセンブリバージョンは 4 桁 (0.3.0.0) なので 3 桁のタグと同一視されること
        Assert.Null(Updater.ParseLatest(LatestJson, new Version(0, 3, 0, 0)));
    }

    [Fact]
    public void ParseLatest_OlderRelease_ReturnsNull()
    {
        Assert.Null(Updater.ParseLatest(LatestJson, new Version(1, 0, 0, 0)));
    }

    [Fact]
    public void ParseLatest_NoAssets_ReturnsInfoWithoutUrls()
    {
        var info = Updater.ParseLatest("""{ "tag_name": "v9.0.0", "html_url": "x" }""", new Version(0, 2, 0));

        Assert.NotNull(info);
        Assert.Null(info!.InstallerUrl);
        Assert.Null(info.ExeUrl);
    }
}
