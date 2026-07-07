using System.IO;
using System.Text.Json;

namespace TClock;

/// <summary>アプリ設定。%APPDATA%\t-clock\settings.json に永続化する。</summary>
public class Settings
{
    public bool ShowSeconds { get; set; } = true;
    public bool ShowDate { get; set; } = true;
    public bool Hour24 { get; set; } = true;
    public bool AlwaysOnTop { get; set; } = true;
    public bool ClickThrough { get; set; } = false;
    public int Opacity { get; set; } = 90; // 20-100
    public bool Compact { get; set; } = false;
    public int SnoozeMinutes { get; set; } = 5;
    public bool ToastEnabled { get; set; } = true;
    public bool FlashEnabled { get; set; } = true;
    public bool Autostart { get; set; } = false;
    public string Theme { get; set; } = "dark"; // 配色テーマ Id (Theme.All 参照)
    public double WindowLeft { get; set; } = -1; // -1 = 未設定（右下に配置）
    public double WindowTop { get; set; } = -1;

    private static readonly JsonSerializerOptions JsonOpts = new() { WriteIndented = true };

    private static string Dir =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "t-clock");

    private static string FilePath => Path.Combine(Dir, "settings.json");

    public static Settings Load()
    {
        try
        {
            if (File.Exists(FilePath))
            {
                var json = File.ReadAllText(FilePath);
                var s = JsonSerializer.Deserialize<Settings>(json);
                if (s != null) return s;
            }
        }
        catch
        {
            // 破損時は既定値
        }
        return new Settings();
    }

    public void Save()
    {
        try
        {
            Directory.CreateDirectory(Dir);
            File.WriteAllText(FilePath, JsonSerializer.Serialize(this, JsonOpts));
        }
        catch
        {
            // 保存失敗は無視（次回起動時に既定値へフォールバック）
        }
    }
}
