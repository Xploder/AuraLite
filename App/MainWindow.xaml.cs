using System.Runtime.InteropServices;
using System.Text.Json;
using Microsoft.UI.Dispatching;
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
    private static readonly string LightingPath = Path.Combine(Path.GetDirectoryName(ProfilePath)!, "lighting.json");
    private LightingSettings lighting = new(false, null, []);
    private int resumeAttempts;
    private long sequence;
    private string languageChoice = "auto";
    private UiLanguage language = new("auto");
    private Phrase statusTitle = Phrase.Controller;
    private Phrase? statusMessage = Phrase.Ready;
    private string statusDetail = "";
    private const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private readonly SortedDictionary<string, LightingProfile> profiles = new(StringComparer.CurrentCultureIgnoreCase);
    private ControllerInfo? info;
    private string? devicePath;
    private readonly DispatcherQueueTimer applyTimer;
    private readonly DispatcherQueueTimer resumeTimer;
    private readonly NativeTray tray;
    private readonly Dictionary<int, PendingLighting> pending = [];
    private readonly Dictionary<int, LightingChange> applied = [];
    private bool scanning;
    private bool sending;
    private bool closing;
    private bool ready;
    private int preferredChannel = 1;
    private LightingProfile? startupRetry;

    public MainWindow()
    {
        InitializeComponent();
        applyTimer = DispatcherQueue.CreateTimer();
        applyTimer.Interval = TimeSpan.FromMilliseconds(120);
        applyTimer.IsRepeating = false;
        applyTimer.Tick += async (_, _) => await ApplyPendingAsync();
        resumeTimer = DispatcherQueue.CreateTimer();
        resumeTimer.IsRepeating = false;
        resumeTimer.Tick += async (_, _) => await ResumeAsync();
        ExtendsContentIntoTitleBar = true;
        SetTitleBar(WindowTitleBar);
        string iconPath = Path.Combine(AppContext.BaseDirectory, "Assets", "AuraLite.ico");
        AppWindow.SetIcon(iconPath);
        tray = new NativeTray(WinRT.Interop.WindowNative.GetWindowHandle(this), iconPath,
            RestoreWindow, Close, ScheduleResume, () => language[Phrase.TrayOpen], () => language[Phrase.TrayExit]);
        Closed += (_, _) =>
        {
            closing = true;
            applyTimer.Stop();
            resumeTimer.Stop();
            tray.Dispose();
        };
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
            if (File.Exists(LightingPath))
            {
                lighting = JsonSerializer.Deserialize(File.ReadAllText(LightingPath),
                    ProfileJsonContext.Default.LightingSettings) ?? lighting;
                RestoreLighting.IsChecked = lighting.Restore;
            }
            if (File.Exists(ProfilePath))
            {
                var stored = JsonSerializer.Deserialize(File.ReadAllText(ProfilePath),
                    ProfileJsonContext.Default.DictionaryStringLightingProfile);
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

    internal void RestoreWindow()
    {
        tray.Restore();
        Activate();
    }

    [DllImport("user32.dll")]
    private static extern uint GetDpiForWindow(nint hwnd);

    private void ApplyLanguage()
    {
        // Localizing the effect menu raises SelectionChanged.
        bool wasReady = ready;
        ready = false;
        language = new UiLanguage(languageChoice);
        ControllerHeading.Text = language[Phrase.Controller];
        LightingHeading.Text = language[Phrase.Lighting];
        ScanButton.Content = language[Phrase.Detect];
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
        StartupProfile.Header = language[Phrase.StartupProfile];
        RefreshStartupProfiles();
        Autostart.Header = language[Phrase.Autostart];
        RestoreLightingLabel.Text = language[Phrase.RestoreLighting];
        Autostart.OnContent = language[Phrase.On];
        Autostart.OffContent = language[Phrase.Off];
        LanguagePicker.Header = language[Phrase.Language];

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

    private void UpdateScanButton() =>
        ScanButton.IsEnabled = !scanning && !sending && pending.Count == 0;

    private async void ScanClicked(object sender, RoutedEventArgs e) => await DetectAsync();

    public async Task DetectAsync(bool restore = false, LightingProfile? startupProfile = null)
    {
        if (scanning || sending || pending.Count > 0) return;
        if (Channels.SelectedIndex >= 0) preferredChannel = Channels.SelectedIndex + 1;
        info = null;
        applied.Clear();
        scanning = true;
        UpdateScanButton();
        SetStatus(Phrase.Controller, Phrase.Detecting, InfoBarSeverity.Informational);
        try
        {
            await using Controller controller = await Controller.OpenAsync();
            info = controller.Info;
            devicePath = controller.Device.Path;
            ShowDetected();
        }
        catch (Exception error)
        {
            ShowError(Phrase.Unavailable, error);
        }
        finally
        {
            UpdateChannels();
            scanning = false;
            UpdateScanButton();
        }

        if (!restore || closing || !lighting.Restore || info is not { } detected) return;
        if (startupProfile is not null)
        {
            if (detected.CanControl(startupProfile.Channel)) Enqueue(startupProfile, automatic: true);
            else SetStatus(Phrase.SendFailed, Phrase.MissingChannel, InfoBarSeverity.Error);
        }
        else if (detected == lighting.Controller)
        {
            LightingProfile[] saved = (lighting.Channels ?? [])
                .Where(profile => profile is { Count: >= 1 and <= 120, Brightness: >= 0 and <= 100,
                    ModeIndex: >= 0 and <= 2 } && detected.CanControl(profile.Channel))
                .ToArray();
            if (saved.Length > 0)
            {
                LoadEditor(saved[^1]);
                foreach (LightingProfile profile in saved) Enqueue(profile, automatic: true);
            }
        }
    }

    internal async Task DetectAtStartupAsync()
    {
        LightingProfile? profile = LoadStartupProfile();
        await DetectAsync(restore: true, startupProfile: profile);
        if (info is null)
        {
            startupRetry = profile;
            ScheduleResume();
        }
    }

    private void ScheduleResume()
    {
        applied.Clear();
        if (!lighting.Restore || closing) return;
        resumeAttempts = 3;
        resumeTimer.Interval = TimeSpan.FromSeconds(1);
        resumeTimer.Stop();
        resumeTimer.Start();
    }

    private async Task ResumeAsync()
    {
        if (!lighting.Restore || closing) return;
        if (scanning || sending || pending.Count > 0)
        {
            resumeTimer.Start();
            return;
        }

        await DetectAsync(restore: true, startupProfile: startupRetry);
        if (info is not null) startupRetry = null;
        if (info is null && --resumeAttempts > 0)
        {
            resumeTimer.Interval = TimeSpan.FromSeconds(2);
            resumeTimer.Start();
        }
    }

    private void RestoreLightingChanged(object sender, RoutedEventArgs e)
    {
        if (!ready) return;
        lighting = lighting with { Restore = RestoreLighting.IsChecked == true };
        if (!lighting.Restore)
        {
            startupRetry = null;
            resumeTimer.Stop();
        }
        SaveLighting();
    }

    private void SaveLighting()
    {
        try { SaveText(LightingPath, JsonSerializer.Serialize(lighting, ProfileJsonContext.Default.LightingSettings)); }
        catch (Exception error) { ShowError(Phrase.Settings, error); }
    }

    private void ShowDetected()
    {
        if (info is { } detected)
            SetStatus(Phrase.Detected, FormatStatus(detected), InfoBarSeverity.Success);
    }

    private static string FormatStatus(ControllerInfo detected) =>
        $"{detected.Firmware} · {detected.ArgbChannels} ARGB · {detected.RgbChannels} RGB";

    private void QueueLighting()
    {
        if (!ready || closing || scanning) return;
        int channel = Channels.SelectedIndex + 1;
        if (info?.CanControl(channel) != true || Mode.SelectedIndex < 0)
            return;

        if (!double.IsFinite(LedCount.Value) || LedCount.Value != Math.Truncate(LedCount.Value))
            return;
        Enqueue(ReadEditor());
    }

    private void Enqueue(LightingProfile profile, bool automatic = false)
    {
        pending[profile.Channel] = new PendingLighting(profile, automatic, ++sequence);
        applyTimer.Stop();
        applyTimer.Start();
        UpdateScanButton();
    }

    private async Task ApplyPendingAsync()
    {
        if (sending || closing || pending.Count == 0) return;
        sending = true;
        UpdateScanButton();
        PendingLighting[] changes = pending.Values.OrderBy(change => change.Sequence).ToArray();
        pending.Clear();
        ControllerInfo? target = info;
        bool settingsChanged = false;

        try
        {
            if (target is null) return;
            await using Controller controller = await Controller.OpenAsync();
            if (controller.Info != target || !StringComparer.OrdinalIgnoreCase.Equals(controller.Device.Path, devicePath))
                throw new UiException(Phrase.ConfigChanged);

            foreach (PendingLighting item in changes)
            {
                if (closing) break;
                if (item.Automatic && !lighting.Restore) continue;
                LightingProfile profile = item.Profile;
                LightingChange change = ToChange(profile);
                int channel = profile.Channel;
                if (!applied.TryGetValue(channel, out LightingChange current) || current != change)
                {
                    await controller.ApplyAsync(channel, change.Mode, change.Color, change.Count);
                    applied[channel] = change;
                }
                settingsChanged |= RememberLighting(target, profile);
            }
            if (!closing)
                SetStatus(Phrase.LightingUpdated, FormatStatus(target), InfoBarSeverity.Success);
        }
        catch (Exception error)
        {
            pending.Clear();
            applyTimer.Stop();
            info = null;
            UpdateChannels();
            ShowError(Phrase.SendFailed, error);
        }
        finally
        {
            if (settingsChanged) SaveLighting();
            sending = false;
            if (!closing && pending.Count > 0 && !applyTimer.IsRunning) applyTimer.Start();
            UpdateScanButton();
        }
    }

    private bool RememberLighting(ControllerInfo controller, LightingProfile profile)
    {
        List<LightingProfile> channels = lighting.Controller == controller && lighting.Channels is not null
            ? lighting.Channels
            : [];
        if (channels.LastOrDefault() == profile) return false;
        channels.RemoveAll(saved => saved is null || saved.Channel == profile.Channel);
        channels.Add(profile);
        lighting = lighting with { Controller = controller, Channels = channels };
        return true;
    }

    private static LightingChange ToChange(LightingProfile profile) => profile.ModeIndex switch
    {
        1 => new LightingChange(LightingMode.ColorCycle, default, 0),
        2 => new LightingChange(LightingMode.Rainbow, default, 0),
        _ => new LightingChange(LightingMode.Static,
            new Rgb(profile.Red, profile.Green, profile.Blue).Scale(profile.Brightness), profile.Count)
    };

    private readonly record struct PendingLighting(LightingProfile Profile, bool Automatic, long Sequence);

    private readonly record struct LightingChange(LightingMode Mode, Rgb Color, int Count);

    private void ColorChanged(ColorPicker sender, ColorChangedEventArgs e) => QueueLighting();

    private void BrightnessChanged(object sender, Microsoft.UI.Xaml.Controls.Primitives.RangeBaseValueChangedEventArgs e) =>
        QueueLighting();

    private void LedCountChanged(NumberBox sender, NumberBoxValueChangedEventArgs e) => QueueLighting();

    private void ModeChanged(object sender, SelectionChangedEventArgs e)
    {
        UpdateEffectControls();
        QueueLighting();
    }


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
            SetActiveProfile(name);
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
            lighting = lighting with
            {
                ActiveProfile = SameProfile(lighting.ActiveProfile, name) ? null : lighting.ActiveProfile,
                StartupProfile = SameProfile(lighting.StartupProfile, name) ? null : lighting.StartupProfile
            };
            RefreshProfiles();
            SaveLighting();
            ProfileName.Text = "";
            SetStatus(Phrase.Deleted, name, InfoBarSeverity.Informational);
        }
        catch (Exception error) { ShowError(Phrase.Profiles, error); }
    }

    private void SaveProfiles(SortedDictionary<string, LightingProfile> updated)
    {
        SaveText(ProfilePath, JsonSerializer.Serialize(updated, ProfileJsonContext.Default.SortedDictionaryStringLightingProfile));

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
        bool wasReady = ready;
        ready = false;
        Profiles.ItemsSource = profiles.Keys.ToArray();
        Profiles.SelectedItem = CanonicalProfileName(lighting.ActiveProfile);
        RefreshStartupProfiles();
        ready = wasReady;
    }

    private void RefreshStartupProfiles()
    {
        string[] names = profiles.Keys.ToArray();
        StartupProfile.ItemsSource = new[] { language[Phrase.LastProfile] }.Concat(names).ToArray();
        string? preferred = CanonicalProfileName(lighting.StartupProfile);
        StartupProfile.SelectedIndex = preferred is null ? 0 : Array.IndexOf(names, preferred) + 1;
    }

    private string? CanonicalProfileName(string? name) =>
        name is null ? null : profiles.Keys.FirstOrDefault(key => SameProfile(key, name));

    private bool SameProfile(string? first, string second) =>
        first is not null && profiles.Comparer.Compare(first, second) == 0;

    private LightingProfile? LoadStartupProfile()
    {
        string? name = CanonicalProfileName(lighting.StartupProfile) ?? CanonicalProfileName(lighting.ActiveProfile)
            ?? (profiles.Count == 1 ? profiles.Keys.First() : null);
        if (name is null || !profiles.TryGetValue(name, out LightingProfile? profile)) return null;
        bool wasReady = ready;
        ready = false;
        Profiles.SelectedItem = name;
        ProfileName.Text = name;
        LoadEditor(profile);
        ready = wasReady;
        return profile;
    }

    private void StartupProfileChanged(object sender, SelectionChangedEventArgs e)
    {
        if (!ready || StartupProfile.SelectedIndex < 0) return;
        lighting = lighting with
        {
            StartupProfile = StartupProfile.SelectedIndex == 0 ? null : StartupProfile.SelectedItem as string
        };
        SaveLighting();
    }

    private void SetActiveProfile(string name)
    {
        lighting = lighting with { ActiveProfile = name };
        bool wasReady = ready;
        ready = false;
        Profiles.SelectedItem = CanonicalProfileName(name);
        ready = wasReady;
        SaveLighting();
    }

    private void ProfileSelected(object sender, SelectionChangedEventArgs e)
    {
        if (!ready || Profiles.SelectedItem is not string name || !profiles.TryGetValue(name, out LightingProfile? profile)) return;
        SetActiveProfile(name);
        ProfileName.Text = name;
        LoadEditor(profile);
        QueueLighting();
    }

    private void LoadEditor(LightingProfile profile)
    {
        bool wasReady = ready;
        ready = false;
        preferredChannel = profile.Channel;
        UpdateChannels();
        Mode.SelectedIndex = profile.ModeIndex;
        Picker.Color = Windows.UI.Color.FromArgb(255, profile.Red, profile.Green, profile.Blue);
        Brightness.Value = profile.Brightness;
        LedCount.Value = profile.Count;
        ready = wasReady;
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

internal sealed record LightingSettings(bool Restore, ControllerInfo? Controller, List<LightingProfile>? Channels,
    string? ActiveProfile = null, string? StartupProfile = null);
