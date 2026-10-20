using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Reflection;
using System.Text.Json;

namespace TClock;

/// <summary>新しいバージョンの情報 (GitHub Release 1件分)。</summary>
public sealed record UpdateInfo(Version Version, string Tag, string PageUrl, string? InstallerUrl, string? ExeUrl);

/// <summary>
/// GitHub Releases の最新版を確認し、更新を適用する。
/// インストーラーで入れた環境ではインストーラーを無人実行し、
/// 単体 exe で使っている場合は exe を差し替えて再起動する。
/// </summary>
public static class Updater
{
    private const string Repo = "junmt/t-clock";
    private const string LatestApi = $"https://api.github.com/repos/{Repo}/releases/latest";

    public static Version CurrentVersion =>
        Assembly.GetExecutingAssembly().GetName().Version ?? new Version(0, 0, 0);

    private static string? ExePath => Environment.ProcessPath;

    /// <summary>インストーラー経由で導入されたか (Inno Setup のアンインストーラーが隣にあるか)。</summary>
    public static bool IsInstalled =>
        ExePath != null && File.Exists(Path.Combine(Path.GetDirectoryName(ExePath)!, "unins000.exe"));

    private static readonly HttpClient Http = CreateClient();

    private static HttpClient CreateClient()
    {
        var c = new HttpClient { Timeout = TimeSpan.FromMinutes(5) };
        c.DefaultRequestHeaders.UserAgent.ParseAdd($"t-clock/{CurrentVersion}");
        return c;
    }

    /// <summary>"v1.2.3" / "1.2.3" をバージョンに変換する。</summary>
    public static bool TryParseTag(string? tag, out Version version)
    {
        version = new Version(0, 0, 0);
        if (string.IsNullOrWhiteSpace(tag)) return false;
        var s = tag.Trim().TrimStart('v', 'V');
        if (!Version.TryParse(s, out var v)) return false;
        version = v;
        return true;
    }

    /// <summary>
    /// releases/latest の JSON を解釈し、current より新しければ情報を返す (古い/同じなら null)。
    /// </summary>
    public static UpdateInfo? ParseLatest(string json, Version current)
    {
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;
        var tag = root.TryGetProperty("tag_name", out var t) ? t.GetString() : null;
        if (!TryParseTag(tag, out var latest) || Normalize(latest) <= Normalize(current)) return null;

        string? installer = null, exe = null;
        if (root.TryGetProperty("assets", out var assets))
        {
            foreach (var a in assets.EnumerateArray())
            {
                var name = a.GetProperty("name").GetString() ?? "";
                var url = a.GetProperty("browser_download_url").GetString();
                if (name.EndsWith("-setup.exe", StringComparison.OrdinalIgnoreCase)) installer = url;
                else if (name.Equals("t-clock.exe", StringComparison.OrdinalIgnoreCase)) exe = url;
            }
        }
        var page = root.TryGetProperty("html_url", out var h) ? h.GetString() ?? "" : "";
        return new UpdateInfo(latest, tag!, page, installer, exe);
    }

    // 1.2 と 1.2.0 や 1.2.0.0 を同一視するため、未指定部分を 0 で埋める
    private static Version Normalize(Version v) =>
        new(v.Major, Math.Max(0, v.Minor), Math.Max(0, v.Build), Math.Max(0, v.Revision));

    /// <summary>最新リリースを確認する。通信失敗時は null。</summary>
    public static async Task<UpdateInfo?> CheckAsync()
    {
        try
        {
            var json = await Http.GetStringAsync(LatestApi);
            return ParseLatest(json, CurrentVersion);
        }
        catch { return null; }
    }

    /// <summary>この環境で自動適用できるか (必要なアセットが添付されているか)。</summary>
    public static bool CanApply(UpdateInfo info) =>
        ExePath != null && (IsInstalled ? info.InstallerUrl != null : info.ExeUrl != null);

    /// <summary>
    /// 更新ファイルをダウンロードして適用の準備をする。
    /// 呼び出し側はアプリを終了 (設定保存) した直後に、戻り値の処理 (インストーラー起動 / 新 exe 起動) を実行すること。
    /// </summary>
    public static async Task<Action> PrepareAsync(UpdateInfo info)
    {
        var exe = ExePath ?? throw new InvalidOperationException("実行ファイルのパスを取得できません。");

        if (IsInstalled)
        {
            var setup = Path.Combine(Path.GetTempPath(), $"t-clock-{info.Version}-setup.exe");
            await DownloadAsync(info.InstallerUrl!, setup);
            // /UPDATE を付けるとインストーラーが完了後に t-clock を起動する (installer/t-clock.iss)
            return () => Process.Start(new ProcessStartInfo(setup, "/SILENT /SUPPRESSMSGBOXES /NORESTART /UPDATE")
            {
                UseShellExecute = true,
            });
        }

        // 実行中の exe は削除できないがリネームはできるので、旧 exe を .old に退避して入れ替える
        var newFile = exe + ".new";
        await DownloadAsync(info.ExeUrl!, newFile);
        var oldFile = exe + ".old";
        if (File.Exists(oldFile)) File.Delete(oldFile);
        File.Move(exe, oldFile);
        File.Move(newFile, exe);
        return () => Process.Start(new ProcessStartInfo(exe) { UseShellExecute = true });
    }

    private static async Task DownloadAsync(string url, string dest)
    {
        var tmp = dest + ".part";
        using (var res = await Http.GetAsync(url, HttpCompletionOption.ResponseHeadersRead))
        {
            res.EnsureSuccessStatusCode();
            await using var fs = File.Create(tmp);
            await res.Content.CopyToAsync(fs);
        }
        File.Move(tmp, dest, overwrite: true);
    }

    /// <summary>前回の exe 差し替えで残った旧ファイルを掃除する。</summary>
    public static void CleanupOldFiles()
    {
        try
        {
            if (ExePath is { } exe && File.Exists(exe + ".old")) File.Delete(exe + ".old");
        }
        catch { /* 旧プロセスがまだ残っていれば次回に回す */ }
    }

    public static void OpenPage(string url)
    {
        try { Process.Start(new ProcessStartInfo(url) { UseShellExecute = true }); }
        catch { /* ブラウザが無い等は無視 */ }
    }
}
