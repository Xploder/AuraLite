using System.Runtime.InteropServices;
using System.Text.Json;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.Win32;

namespace AuraLite;

public sealed partial class MainWindow : Window
{
    private static readonly string ProfilePath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "AuraLite", "profiles.json");
    private static readonly string LanguagePath = Path.Combine(Path.GetDirectoryName(ProfilePath)!, "language.txt");
    private string languageChoice = "auto";
    private UiLanguage language = new("auto");
    private Phrase statusTitle = Phrase.Controller;
    private Phrase? statusMessage = Phrase.Ready;
    private string statusDetail = "";
    private const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private readonly SortedDictionary<string, LightingProfile> profiles = new(StringComparer.CurrentCultureIgnoreCase);
    private ControllerInfo? info;
    private bool busy;
    private bool ready;
    private int preferredChannel = 1;

    public MainWindow()
    {
        InitializeComponent();
        ExtendsContentIntoTitleBar = true;
        SetTitleBar(WindowTitleBar);
        AppWindow.SetIcon(Path.Combine(AppContext.BaseDirectory, "Assets", "AuraLite.ico"));
        var titleBar = AppWindow.TitleBar;
        titleBar.ButtonBackgroundColor = Microsoft.UI.Colors.Transparent;
        titleBar.ButtonInactiveBackgroundColor = Microsoft.UI.Colors.Transparent;
        titleBar.ButtonForegroundColor = Microsoft.UI.Colors.White;
        titleBar.ButtonInactiveForegroundColor = Microsoft.UI.Colors.LightGray;
        titleBar.ButtonHoverBackgroundColor = Windows.UI.Color.FromArgb(255, 56, 58, 68);
        titleBar.ButtonPressedBackgroundColor = Windows.UI.Color.FromArgb(255, 69, 72, 86);

        double scale = Math.Max(96u, GetDpiForWindow(WinRT.Interop.WindowNative.GetWindowHandle(this))) / 96d;
        var area = DisplayArea.GetFromWindowId(AppWindow.Id, DisplayAreaFallback.Primary).WorkArea;
        AppWindow.Resize(new Windows.Graphics.SizeInt32(
            Math.Min((int)Math.Round(840 * scale), area.Width),
            Math.Min((int)Math.Round(870 * scale), area.Height)));

        try
        {
            if (File.Exists(LanguagePath))
            {
                string persisted = File.ReadAllText(LanguagePath).Trim();
                if (Array.IndexOf(UiLanguage.Codes, persisted) >= 0) languageChoice = persisted;
            }
            if (File.Exists(ProfilePath))
            {
                var stored = JsonSerializer.Deserialize<Dictionary<string, LightingProfile>>(File.ReadAllText(ProfilePath));
                if (stored is not null)
                    foreach (var item in stored)
                        if (item.Value is { Channel: >= 1 and <= 9, Count: >= 1 and <= 120,
                            Brightness: >= 0 and <= 100, ModeIndex: >= 0 and <= 2 })
                            profiles[item.Key] = item.Value;
            }
            RefreshProfiles();
            using RegistryKey? key = Registry.CurrentUser.OpenSubKey(RunKey);
            Autostart.IsOn = key?.GetValue("AuraLite") is string;
        }
        catch (Exception error)
        {
            ShowError(Phrase.Settings, error);
        }
        ApplyLanguage();
        ready = true;
        UpdateEffectControls();
    }

    [DllImport("user32.dll")]
    private static extern uint GetDpiForWindow(nint hwnd);

    private void ApplyLanguage()
    {
        language = new UiLanguage(languageChoice);
        ControllerHeading.Text = language[Phrase.Controller];
        LightingHeading.Text = language[Phrase.Lighting];
        ScanButton.Content = language[Phrase.Detect];
        ApplyButton.Content = language[Phrase.Review];
        Channels.Header = language[Phrase.Channel];
        Channels.PlaceholderText = language[Phrase.ChooseChannel];
        Mode.Header = language[Phrase.Mode];
        int selectedMode = Math.Max(0, Mode.SelectedIndex);
        Mode.ItemsSource = new[] { language[Phrase.Static], language[Phrase.Cycle], language[Phrase.Rainbow] };
        Mode.SelectedIndex = selectedMode;
        Brightness.Header = language[Phrase.Brightness];
        LedCount.Header = language[Phrase.LedCount];
        ProfilesHeading.Text = language[Phrase.Profiles];
        ProfileName.PlaceholderText = language[Phrase.ProfileName];
        SaveProfileButton.Content = language[Phrase.Save];
        DeleteProfileButton.Content = language[Phrase.Delete];
        Profiles.PlaceholderText = language[Phrase.Load];
        Autostart.Header = language[Phrase.Autostart];
        Autostart.OnContent = language[Phrase.On];
        Autostart.OffContent = language[Phrase.Off];
        LanguagePicker.Header = language[Phrase.Language];

        // Changing the selected label can raise SelectionChanged.
        bool wasReady = ready;
        ready = false;
        LanguagePicker.Items.Clear();
        foreach (string name in UiLanguage.NativeNames)
            LanguagePicker.Items.Add(name.Length == 0 ? language[Phrase.WindowsDefault] : name);
        LanguagePicker.SelectedIndex = Array.IndexOf(UiLanguage.Codes, languageChoice);
        ready = wasReady;
        RefreshStatus();
    }

    private void LanguageChanged(object sender, SelectionChangedEventArgs args)
    {
        if (!ready || LanguagePicker.SelectedIndex < 0) return;
        languageChoice = UiLanguage.Codes[LanguagePicker.SelectedIndex];
        ApplyLanguage();
        try
        {
            SaveText(LanguagePath, languageChoice);
        }
        catch (Exception error) { ShowError(Phrase.Settings, error); }
    }

    private void SetStatus(Phrase title, Phrase message, InfoBarSeverity severity)
    {
        statusTitle = title;
        statusMessage = message;
        statusDetail = "";
        Status.Severity = severity;
        RefreshStatus();
    }

    private void SetStatus(Phrase title, string detail, InfoBarSeverity severity)
    {
        statusTitle = title;
        statusMessage = null;
        statusDetail = detail;
        Status.Severity = severity;
        RefreshStatus();
    }

    private void ShowError(Phrase title, Exception error)
    {
        if (error is UiException localized)
        {
            SetStatus(title, localized.Phrase, InfoBarSeverity.Error);
            return;
        }
        statusTitle = title;
        statusMessage = Phrase.ErrorDetails;
        statusDetail = error.Message;
        Status.Severity = InfoBarSeverity.Error;
        RefreshStatus();
    }

    private void RefreshStatus()
    {
        Status.Title = language[statusTitle];
        Status.Message = statusMessage is Phrase message
            ? string.Join(" · ", new[] { language[message], statusDetail }.Where(text => text.Length > 0))
            : statusDetail;
    }

    private sealed class UiException(Phrase phrase) : Exception
    {
        public Phrase Phrase { get; } = phrase;
    }

    private void UpdateChannels()
    {
        Channels.Items.Clear();
        for (int channel = 1; channel <= (info?.ArgbChannels ?? 0); channel++)
            Channels.Items.Add($"ARGB {channel}");
        Channels.SelectedIndex = preferredChannel <= Channels.Items.Count ? preferredChannel - 1 : -1;
    }

    private void SetBusy(bool value)
    {
        busy = value;
        ScanButton.IsEnabled = !value;
        LanguagePicker.IsEnabled = !value;
        ApplyButton.IsEnabled = !value && info is { ArgbChannels: > 0, FixedLeds: > 0 };
    }

    private async void ScanClicked(object sender, RoutedEventArgs e)
    {
        if (busy) return;
        if (Channels.SelectedIndex >= 0) preferredChannel = Channels.SelectedIndex + 1;
        info = null;
        SetBusy(true);
        SetStatus(Phrase.Controller, Phrase.Detecting, InfoBarSeverity.Informational);
        try
        {
            await using Controller controller = await Controller.OpenAsync();
            info = controller.Info;
            SetStatus(Phrase.Detected,
                $"{info.Firmware} · {info.ArgbChannels} ARGB · {info.RgbChannels} RGB", InfoBarSeverity.Success);
        }
        catch (Exception error)
        {
            ShowError(Phrase.Unavailable, error);
        }
        finally
        {
            UpdateChannels();
            SetBusy(false);
        }
    }

    private async void ApplyClicked(object sender, RoutedEventArgs e)
    {
        if (busy || info is null) return;
        SetBusy(true);
        try
        {
            LightingProfile profile = ReadEditor();
            var color = new Rgb(profile.Red, profile.Green, profile.Blue).Scale(profile.Brightness);
            LightingMode mode = profile.ModeIndex switch
            {
                1 => LightingMode.ColorCycle,
                2 => LightingMode.Rainbow,
                _ => LightingMode.Static
            };
            await using Controller controller = await Controller.OpenAsync();
            if (controller.Info != info)
                throw new UiException(Phrase.ConfigChanged);
            bool sent = await controller.ApplyAsync(profile.Channel, mode, color, profile.Count, ConfirmAsync);
            SetStatus(sent ? Phrase.Sent : Phrase.Canceled,
                sent ? Phrase.CheckLighting : Phrase.NoChange,
                sent ? InfoBarSeverity.Success : InfoBarSeverity.Informational);
        }
        catch (Exception error)
        {
            ShowError(Phrase.SendFailed, error);
        }
        finally { SetBusy(false); }
    }

    private async Task<bool> ConfirmAsync(string preview)
    {
        var dialog = new ContentDialog
        {
            Title = language[Phrase.Confirm],
            Content = new ScrollViewer
            {
                MaxHeight = 340,
                Content = new TextBlock
                {
                    Text = preview, TextWrapping = TextWrapping.Wrap, IsTextSelectionEnabled = true,
                    FontFamily = new Microsoft.UI.Xaml.Media.FontFamily("Consolas")
                }
            },
            PrimaryButtonText = language[Phrase.Send],
            CloseButtonText = language[Phrase.Cancel],
            DefaultButton = ContentDialogButton.Close,
            XamlRoot = Root.XamlRoot,
            RequestedTheme = ElementTheme.Dark
        };
        return await dialog.ShowAsync() == ContentDialogResult.Primary;
    }

    private void ModeChanged(object sender, SelectionChangedEventArgs e) => UpdateEffectControls();

    private void UpdateEffectControls()
    {
        if (LedCount is null || Brightness is null || Picker is null) return;
        bool staticMode = Mode.SelectedIndex == 0;
        LedCount.IsEnabled = Brightness.IsEnabled = Picker.IsEnabled = staticMode;
    }

    private LightingProfile ReadEditor()
    {
        if (Channels.Items.Count > 0 && Channels.SelectedIndex < 0)
            throw new UiException(Phrase.MissingChannel);
        if (!double.IsFinite(LedCount.Value) || LedCount.Value != Math.Truncate(LedCount.Value))
            throw new UiException(Phrase.InvalidCount);
        int channel = Channels.SelectedIndex >= 0 ? Channels.SelectedIndex + 1 : preferredChannel;
        var color = Picker.Color;
        return new LightingProfile(channel, Mode.SelectedIndex, color.R, color.G, color.B,
            (int)Brightness.Value, (int)LedCount.Value);
    }

    private void SaveClicked(object sender, RoutedEventArgs e)
    {
        try
        {
            string name = ProfileName.Text.Trim();
            if (name.Length == 0) throw new UiException(Phrase.MissingName);
            var updated = new SortedDictionary<string, LightingProfile>(profiles, profiles.Comparer) { [name] = ReadEditor() };
            SaveProfiles(updated);
            SetStatus(Phrase.Saved, name, InfoBarSeverity.Success);
        }
        catch (Exception error) { ShowError(Phrase.Profiles, error); }
    }

    private void DeleteClicked(object sender, RoutedEventArgs e)
    {
        try
        {
            if (ProfileName.Text.Trim() is not { Length: > 0 } name || !profiles.ContainsKey(name)) return;
            var updated = new SortedDictionary<string, LightingProfile>(profiles, profiles.Comparer);
            updated.Remove(name);
            SaveProfiles(updated);
            ProfileName.Text = "";
            SetStatus(Phrase.Deleted, name, InfoBarSeverity.Informational);
        }
        catch (Exception error) { ShowError(Phrase.Profiles, error); }
    }

    private void SaveProfiles(SortedDictionary<string, LightingProfile> updated)
    {
        SaveText(ProfilePath, JsonSerializer.Serialize(updated));

        profiles.Clear();
        foreach (var item in updated) profiles.Add(item.Key, item.Value);
        RefreshProfiles();
    }

    private static void SaveText(string path, string content)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        string temporary = $"{path}.{Guid.NewGuid():N}.tmp";
        try
        {
            File.WriteAllText(temporary, content);
            File.Move(temporary, path, overwrite: true);
        }
        finally { if (File.Exists(temporary)) File.Delete(temporary); }
    }

    private void RefreshProfiles()
    {
        Profiles.ItemsSource = profiles.Keys.ToArray();
        Profiles.SelectedIndex = -1;
    }

    private void ProfileSelected(object sender, SelectionChangedEventArgs e)
    {
        if (Profiles.SelectedItem is not string name || !profiles.TryGetValue(name, out LightingProfile? profile)) return;
        ProfileName.Text = name;
        preferredChannel = profile.Channel;
        UpdateChannels();
        Mode.SelectedIndex = profile.ModeIndex;
        Picker.Color = Windows.UI.Color.FromArgb(255, profile.Red, profile.Green, profile.Blue);
        Brightness.Value = profile.Brightness;
        LedCount.Value = profile.Count;
    }

    private void AutostartChanged(object sender, RoutedEventArgs e)
    {
        if (!ready) return;
        try
        {
            using RegistryKey key = Registry.CurrentUser.CreateSubKey(RunKey);
            if (Autostart.IsOn)
            {
                string executable = Environment.ProcessPath ?? throw new UiException(Phrase.MissingPath);
                key.SetValue("AuraLite", $"\"{executable}\"");
            }
            else key.DeleteValue("AuraLite", throwOnMissingValue: false);
        }
        catch (Exception error)
        {
            ready = false;
            Autostart.IsOn = !Autostart.IsOn;
            ready = true;
            ShowError(Phrase.Autostart, error);
        }
    }
}

public sealed record LightingProfile(int Channel, int ModeIndex, byte Red, byte Green, byte Blue, int Brightness, int Count);
