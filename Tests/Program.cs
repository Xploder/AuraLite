using AuraLite;

static void Expect(bool result, string check)
{
    if (!result) throw new Exception(check);
}

static void Reject<T>(Action run, string check) where T : Exception
{
    try { run(); }
    catch (T) { return; }
    throw new Exception(check);
}

static bool Report(byte[] bytes, string hex)
{
    byte[] expected = new byte[65];
    Convert.FromHexString(hex).CopyTo(expected, 0);
    return bytes.SequenceEqual(expected);
}

var firmware = new byte[65];
Convert.FromHexString("EC0241554C41332D415233322D30323037").CopyTo(firmware, 0);
var config = new byte[65];
Convert.FromHexString("EC3000001E9F03010000").CopyTo(config, 0);
config[31] = 2;
ControllerInfo info = Controller.Parse(firmware, config);
Expect(info is { Firmware: "AULA3-AR32-0207", ArgbChannels: 3, RgbChannels: 1, FixedLeds: 2 }, "Config offsets");

Rgb color = Rgb.Parse("204060").Scale(50);
byte[][] one = Controller.Build(info, 3, LightingMode.Static, color, 1).ToArray();
Expect(one.Length == 2 && Report(one[0], "EC35030000FF") && Report(one[1], "EC40820001102030"), "One LED");
byte[][] boundary = Controller.Build(info, 1, LightingMode.Static, color, 21).ToArray();
Expect(boundary.Length == 3, "21 LED split");
Expect(Report(boundary[1], "EC40000014" + string.Concat(Enumerable.Repeat("102030", 20))), "Full fragment");
Expect(Report(boundary[2], "EC40801401102030"), "Final fragment");
byte[][] maximum = Controller.Build(info, 3, LightingMode.Static, default, 120).ToArray();
Expect(maximum.Length == 7 && Report(maximum[^1], "EC40826414"), "120 LED split");
Expect(Report(Controller.Build(info, 2, LightingMode.ColorCycle, default, 0).Single(), "EC3502000004"), "Cycle");
Expect(Report(Controller.Build(info, 1, LightingMode.Rainbow, default, 0).Single(), "EC3501000005"), "Rainbow");
Reject<ArgumentException>(() => Controller.Build(info, 4, LightingMode.Static, color, 1).ToArray(), "Unknown channel");
Reject<ArgumentException>(() => Controller.Build(info with { FixedLeds = 0 }, 1, LightingMode.Static, color, 1).ToArray(), "Ambiguous effect IDs");
Reject<ArgumentException>(() => Controller.Build(info, 1, (LightingMode)0x3f, color, 1).ToArray(), "Unexpected mode");
Reject<ArgumentOutOfRangeException>(() => Controller.Build(info, 1, LightingMode.Static, color, 121).ToArray(), "Oversized strip");
Reject<IOException>(() => Controller.Parse(firmware, config[..64]), "Short config");
config[0] = 0;
Reject<IOException>(() => Controller.Parse(firmware, config), "Invalid header");
Console.WriteLine("Protokolltests erfolgreich. Kein Hardwarezugriff.");
