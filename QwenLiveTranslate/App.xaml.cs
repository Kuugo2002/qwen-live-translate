using System.Windows;

namespace QwenLiveTranslate;

public partial class App : Application
{
    protected override async void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        if (e.Args.Contains("--self-test") || e.Args.Contains("--probe"))
        {
            var code = await Diagnostics.RunAsync(e.Args);
            Shutdown(code);
            return;
        }
        new MainWindow().Show();
    }
}
