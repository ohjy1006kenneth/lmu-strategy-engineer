using System.Windows;

namespace LmuStrategy.Windows;

public partial class App : Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        // Foundation only: no window, session service or game connection exists yet.
        Shutdown();
    }
}
