# t-clock

Windows 向けの、**常に最前面に表示できる邪魔にならない常駐時計 + タイマー**アプリ。
C# / WPF（.NET 8）製。仕様は [`docs/specification.md`](docs/specification.md) を参照。

## 主な機能

- 🕒 現在時刻の常時表示（秒 / 日付 / 12・24時間 切替）
- 📌 常に最前面表示（Always on Top）トグル
- 🪟 邪魔にならないUI：フレームレス・半透明（不透明度調整）・**クリックスルー**・ドラッグ移動・コンパクト表示・パネルはホバー時のみ表示
- ⏱ カウントダウンタイマー / ⏰ 指定時刻アラーム / クイックプリセット（複数同時・ラベル対応・一時停止/再開/リセット/スヌーズ）
- 🔔 通知：トースト通知（トレイバルーン）+ 画面フラッシュ + 終了時に最前面へポップ
- 💾 設定・ウィンドウ位置の永続化・システムトレイ常駐・⚡ 自動起動

## なぜ WPF か

Windows 専用のため、**追加ツールチェーンは .NET SDK ひとつだけ**で済みます
（Rust や C++ Build Tools、Node、WebView2 は不要）。最前面表示・トレイ・通知・
透過・クリックスルーはすべて Windows ネイティブ API で実装しており、NuGet 依存も
ゼロ（WPF + WindowsForms + System.Text.Json はすべて .NET 同梱）。

## 必要環境

- Windows 10 / 11 (x64)
- ビルド時: [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0)
  - `winget install Microsoft.DotNet.SDK.8`
- 実行時: self-contained publish なら **何も不要**（ランタイム同梱）

## 開発起動

```powershell
dotnet run
```

## リリースビルド（単一 .exe / ランタイム同梱）

```powershell
dotnet publish -c Release -r win-x64 --self-contained true `
  -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true
```

生成物:

```
bin\Release\net8.0-windows\win-x64\publish\t-clock.exe
```

この `t-clock.exe` 1ファイルを任意の場所に置いてダブルクリックで起動できます
（配布先PCに .NET のインストールは不要）。

### GitHub Release による自動ビルド

GitHub の **Releases → Draft a new release** で `v1.2.3` 形式のタグを付けて
**Publish release** すると、GitHub Actions（[`.github/workflows/release.yml`](.github/workflows/release.yml)）が
テスト・ビルドを行い、次のファイルをその Release に自動添付します。

| ファイル | 内容 |
| --- | --- |
| `t-clock-<version>-setup.exe` | インストーラー（Inno Setup / 既定はユーザー単位インストールで管理者権限不要） |
| `t-clock.exe` | 単一 exe（ランタイム同梱・インストール不要） |
| `t-clock-<version>-win-x64.zip` | 上記 exe の zip |

インストーラーの定義は [`installer/t-clock.iss`](installer/t-clock.iss)。
インストール時に「Windows の起動時に t-clock を自動起動する」を選べます（既定 ON、後からツールバーの ⚡ で切替可）。

### アップデート

起動時に GitHub の最新 Release を確認し、新しいバージョンがあればトレイ通知が出ます
（通知クリック、またはトレイ右クリック →「更新を確認」で更新）。

- インストーラーで入れた場合: 最新の `*-setup.exe` をダウンロードして無人インストールし、完了後に再起動
- 単体 exe の場合: 最新の `t-clock.exe` をダウンロードしてその場で差し替え、再起動

### ランタイム依存の軽量ビルド（配布先に .NET 8 がある場合）

```powershell
dotnet publish -c Release -r win-x64 --self-contained false -p:PublishSingleFile=true
```

## 操作

- ウィンドウ本体（時計）を**ドラッグ**して移動（位置は保存される）
- マウスを乗せると操作パネルが表示される
- ツールバー：📌最前面 / 🖱クリックスルー / 秒 表示 / ▭コンパクト / ⚡自動起動 / 不透明度スライダー / ✕終了
- クリックスルーON中は時計を貫通して背後を操作できる（トレイアイコン右クリック →「クリックスルー切替」で解除）
- Windows 起動時の自動起動は、ツールバーの ⚡ またはトレイ右クリック →「Windows 起動時に自動起動」で切替
- 閉じる（Alt+F4）でトレイに格納。トレイをダブルクリックで復帰、右クリックメニューから終了

## 設定の保存先

```
%APPDATA%\t-clock\settings.json
```

## 構成

```
t-clock/
├─ TClock.csproj          # プロジェクト定義（net8.0-windows / WPF+WinForms）
├─ App.xaml(.cs)          # アプリ起動（明示終了モード）
├─ MainWindow.xaml(.cs)   # UI + 時計/タイマー/トレイ/通知/ウィンドウ制御
├─ TimerItem.cs           # タイマー1件のモデル（絶対時刻ベース）
├─ Settings.cs            # 設定の JSON 永続化
├─ Updater.cs             # GitHub Release からのアップデート
├─ installer/t-clock.iss  # インストーラー定義 (Inno Setup)
├─ app.manifest           # 高DPI対応
├─ app.ico                # アプリ/トレイアイコン
└─ docs/specification.md  # 仕様書
```
