using System.Globalization;
using System.Text;

namespace AuraLite;

public enum LightingMode { Static = 1, ColorCycle = 4, Rainbow = 5 }

public readonly record struct Rgb(byte Red, byte Green, byte Blue)
{
    public static Rgb Parse(string hex)
    {
        if (hex.StartsWith('#')) hex = hex[1..];
        if (hex.Length != 6 || !uint.TryParse(hex, NumberStyles.AllowHexSpecifier,
                CultureInfo.InvariantCulture, out uint value))
            throw new FormatException("Eine Farbe muss aus sechs Hex-Ziffern bestehen, z. B. 2050C0.");
        return new((byte)(value >> 16), (byte)(value >> 8), (byte)value);
    }

    public Rgb Scale(int percent)
    {
        if (percent is < 0 or > 100) throw new ArgumentOutOfRangeException(nameof(percent));
        return new((byte)((Red * percent + 50) / 100), (byte)((Green * percent + 50) / 100),
            (byte)((Blue * percent + 50) / 100));
    }
}

public sealed record ControllerInfo(string Firmware, int ArgbChannels, int RgbChannels, int FixedLeds,
    string ConfigHex)
{
    public bool CanControl(int channel) => channel >= 1 && channel <= ArgbChannels && channel <= 4 && FixedLeds > 0;
}

public sealed class Controller : IAsyncDisposable
{
    private readonly FileStream stream;
    public ControllerInfo Info { get; }
    public HidDevice Device { get; }

    private Controller(FileStream stream, HidDevice device, ControllerInfo info)
        => (this.stream, Device, Info) = (stream, device, info);

    public static async Task<Controller> OpenAsync()
    {
        HidDevice[] matches = Hid.List(includeRelated: false).Where(device => device.Supported).ToArray();
        if (matches.Length != 1)
            throw new IOException($"Genau eine passende Aura-HID-Schnittstelle erwartet. Gefunden: {matches.Length}.");

        FileStream stream = Hid.Connect(matches[0]);
        try
        {
            byte[] firmware = await QueryAsync(stream, 0x82, 0x02);
            byte[] config = await QueryAsync(stream, 0xb0, 0x30);
            return new Controller(stream, matches[0], Parse(firmware, config));
        }
        catch
        {
            await stream.DisposeAsync();
            throw;
        }
    }

    private static async Task<byte[]> QueryAsync(FileStream stream, byte request, byte response)
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(2));
        try
        {
            await stream.WriteAsync(Frame(request), timeout.Token);
            var buffer = new byte[65];
            for (int i = 0; i < 8; i++)
            {
                int length = await stream.ReadAsync(buffer.AsMemory(), timeout.Token);
                if (length == buffer.Length && buffer[0] == 0xec && buffer[1] == response)
                    return buffer;
                if (length == 0) break;
            }
            throw new IOException($"Kein gültiger Aura-Report EC {response:X2} für EC {request:X2}.");
        }
        catch (OperationCanceledException)
        {
            throw new TimeoutException($"Zeitlimit bei Aura-Abfrage EC {request:X2}, erwartet EC {response:X2}.");
        }
    }

    internal static ControllerInfo Parse(byte[] firmware, byte[] config)
    {
        if (!HasHeader(firmware, 0x02) || !HasHeader(config, 0x30))
            throw new IOException("Ungültige Aura-Antwort.");
        string version = Encoding.ASCII.GetString(firmware, 2, 16).Split('\0')[0].Trim();
        if (version.Length == 0 || version.Any(c => c < ' ' || c > '~' || c == '?'))
            throw new IOException("Die Firmwarekennung ist ungültig.");
        if (config[6] > 9)
            throw new IOException("Die Konfiguration meldet eine unerwartete Anzahl ARGB-Kanäle.");
        return new ControllerInfo(version, config[6], config[7], config[31],
            Convert.ToHexString(config.AsSpan(4, 60)));
    }

    private static bool HasHeader(byte[] frame, byte opcode)
        => frame.Length == 65 && frame[0] == 0xec && frame[1] == opcode;

    private static byte[] Frame(byte opcode)
    {
        var frame = new byte[65];
        frame[0] = 0xec;
        frame[1] = opcode;
        return frame;
    }

    internal static IEnumerable<byte[]> Build(ControllerInfo info, int channel, LightingMode mode, Rgb color, int count)
    {
        if (!info.CanControl(channel))
            throw new ArgumentException("Für diesen ARGB-Kanal ist die Effekt-ID nicht eindeutig belegt.");
        if (!Enum.IsDefined(mode))
            throw new ArgumentException("Unbekannter Lichtmodus.");
        if (mode == LightingMode.Static && count is < 1 or > 120)
            throw new ArgumentOutOfRangeException(nameof(count), "Erlaubt sind 1 bis 120 LEDs.");

        var select = Frame(0x35);
        select[2] = (byte)channel;
        select[5] = mode == LightingMode.Static ? (byte)0xff : (byte)mode;
        yield return select;

        if (mode != LightingMode.Static) yield break;

        for (int offset = 0; offset < count; offset += 20)
        {
            int length = Math.Min(20, count - offset);
            var paint = Frame(0x40);
            paint[2] = (byte)(channel - 1);
            if (offset + length == count) paint[2] |= 0x80;
            paint[3] = (byte)offset;
            paint[4] = (byte)length;
            for (int led = 0; led < length; led++)
            {
                int start = 5 + led * 3;
                paint[start] = color.Red;
                paint[start + 1] = color.Green;
                paint[start + 2] = color.Blue;
            }
            yield return paint;
        }
    }

    public async Task<bool> ApplyAsync(int channel, LightingMode mode, Rgb color, int count,
        Func<string, Task<bool>>? confirm = null)
    {
        byte[][] frames = Build(Info, channel, mode, color, count).ToArray();
        if (confirm is not null)
        {
            string preview = $"Firmware {Info.Firmware} · ARGB {channel} · {mode}\n"
                + string.Join('\n', frames.Select(frame => Convert.ToHexString(frame)));
            if (!await confirm(preview)) return false;
        }

        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(2));
        int sent = 0;
        try
        {
            foreach (byte[] frame in frames)
            {
                await stream.WriteAsync(frame, timeout.Token);
                sent++;
            }
            return true;
        }
        catch (Exception error) when (error is IOException or OperationCanceledException)
        {
            throw new IOException($"Übertragung nach {sent} von {frames.Length} Reports abgebrochen. "
                + "Der Beleuchtungszustand kann sich teilweise geändert haben. " + error.Message, error);
        }
    }

    public ValueTask DisposeAsync() => stream.DisposeAsync();
}
