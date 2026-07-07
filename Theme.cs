namespace TClock;

/// <summary>
/// 配色テーマ。背景色と文字色などをひとまとめにする。
/// 色は WPF の #AARRGGBB / #RRGGBB 形式の文字列。
/// </summary>
public sealed record Theme(
    string Id,
    string Name,
    string Background, // 本体カードの背景
    string Foreground, // 主要テキスト
    string Muted,      // 補助テキスト(日付・ラベル)
    string Chip,       // 行/ボタンの下地
    string Border)     // 枠線・区切り線
{
    /// <summary>選択可能なテーマ一覧（クリックでこの順に巡回）。先頭が既定。</summary>
    public static readonly IReadOnlyList<Theme> All = new[]
    {
        //          Id          表示名          背景         文字       補助       下地         枠線
        new Theme("dark",     "ダーク",       "#D914161C", "#F2F4F8", "#9AA3B2", "#26FFFFFF", "#1FFFFFFF"),
        new Theme("light",    "ライト",       "#F2FFFFFF", "#14161C", "#5B6472", "#14000000", "#22000000"),
        new Theme("midnight", "ミッドナイト", "#E60B1E3A", "#DCE6FF", "#8FA0C8", "#24FFFFFF", "#1FFFFFFF"),
        new Theme("sepia",    "セピア",       "#F0F1E9D6", "#4A3B29", "#8A7A63", "#18000000", "#24000000"),
        new Theme("forest",   "フォレスト",   "#E60E1F17", "#D5EAD8", "#86A98E", "#22FFFFFF", "#1FFFFFFF"),
    };

    /// <summary>Idからテーマを取得。見つからなければ既定(先頭)。</summary>
    public static Theme ById(string? id) =>
        All.FirstOrDefault(t => t.Id == id) ?? All[0];

    /// <summary>指定Idの次のテーマ（末尾なら先頭へ循環）。</summary>
    public static Theme Next(string? id)
    {
        int index = 0;
        for (int i = 0; i < All.Count; i++)
        {
            if (All[i].Id == id) { index = i; break; }
        }
        return All[(index + 1) % All.Count];
    }
}
