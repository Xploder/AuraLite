using Microsoft.UI.Xaml;

namespace AuraLite;

public partial class App : Application
{
    private MainWindow? window;
    private EventWaitHandle? activation;
    private Mutex? instance;
    private RegisteredWaitHandle? activationWait;

    public App() => InitializeComponent();

    protected override void OnLaunched(LaunchActivatedEventArgs args)
    {
        activation = new EventWaitHandle(false, EventResetMode.AutoReset, "Local\\AuraLite.ShowWindow");
        instance = new Mutex(false, "Local\\AuraLite.SingleInstance", out bool first);
        if (!first)
        {
            activation.Set();
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
        main.Activate();
        _ = main.DetectAtStartupAsync();
    }
}
