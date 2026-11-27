using Microsoft.UI.Xaml;

namespace AuraLite;

public partial class App : Application
{
    internal const string TrayArgument = "--tray";

    private MainWindow? window;
    private EventWaitHandle? activation;
    private Mutex? instance;
    private RegisteredWaitHandle? activationWait;

    public App() => InitializeComponent();

    protected override void OnLaunched(LaunchActivatedEventArgs args)
    {
        bool startInTray = Environment.GetCommandLineArgs().Skip(1)
            .Contains(TrayArgument, StringComparer.OrdinalIgnoreCase);

        activation = new EventWaitHandle(false, EventResetMode.AutoReset, "Local\\AuraLite.ShowWindow");
        instance = new Mutex(false, "Local\\AuraLite.SingleInstance", out bool first);
        if (!first)
        {
            if (!startInTray) activation.Set();
            instance.Dispose();
            activation.Dispose();
            Exit();
            return;
        }

        var main = new MainWindow();
        window = main;
        activationWait = ThreadPool.RegisterWaitForSingleObject(activation,
            (_, _) => main.DispatcherQueue.TryEnqueue(main.RestoreWindow),
            null, Timeout.Infinite, executeOnlyOnce: false);
        main.Closed += (_, _) =>
        {
            activationWait?.Unregister(null);
            activation.Dispose();
            instance.Dispose();
            window = null;
        };
        if (!startInTray) main.Activate();
        _ = main.DetectAtStartupAsync();
    }
}
