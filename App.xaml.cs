using System.Windows;

namespace TClock;

public partial class App : Application
{
    // ShutdownMode=OnExplicitShutdown のため、終了はトレイ/終了ボタンから
    // Application.Current.Shutdown() を明示的に呼ぶ。
}
