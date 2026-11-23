using AuraLite;

try
{
    string command = args.FirstOrDefault() ?? "list";
    if (command == "list" && args.Length <= 1)
    {
        IReadOnlyList<HidDevice> devices = Hid.List();
        foreach (HidDevice device in devices)
            Console.WriteLine($"{device}\nProtokollkandidat: {device.Supported}");
        if (devices.Count == 0) Console.WriteLine("Kein HID-Gerät 0B05:19AF gefunden.");
        return devices.Any(device => device.Supported) ? 0 : 1;
    }

    if (command == "status" && args.Length == 1)
    {
        await using Controller controller = await Controller.OpenAsync();
        Console.WriteLine(controller.Device);
        Console.WriteLine(controller.Info);
        for (int channel = 1; channel <= controller.Info.ArgbChannels; channel++)
            Console.WriteLine($"ARGB {channel}");
        return 0;
    }

    if ((command == "static" && args.Length == 4) ||
        (command == "effect" && args.Length == 3))
    {
        int channel = int.Parse(args[1]);
        LightingMode mode = command == "static" ? LightingMode.Static : args[2] switch
        {
            "cycle" => LightingMode.ColorCycle,
            "rainbow" => LightingMode.Rainbow,
            _ => throw new FormatException("Erlaubte Effekte: cycle, rainbow.")
        };
        Rgb color = mode == LightingMode.Static ? Rgb.Parse(args[2]) : default;
        int count = mode == LightingMode.Static ? int.Parse(args[3]) : 0;

        await using Controller controller = await Controller.OpenAsync();
        bool sent = await controller.ApplyAsync(channel, mode, color, count, preview =>
        {
            Console.WriteLine(preview);
            if (Console.IsInputRedirected) return Task.FromResult(false);
            Console.Write("Reports nach Prüfung mit SENDEN freigeben: ");
            return Task.FromResult(Console.ReadLine() == "SENDEN");
        });
        Console.WriteLine(sent ? "Reports übertragen. Beleuchtung am Mainboard prüfen." : "Abgebrochen.");
        return sent ? 0 : 2;
    }

    Console.WriteLine("AuraLite.Probe list | status | static <ARGB-Kanal> <RRGGBB> <LED-Anzahl> | effect <ARGB-Kanal> <cycle|rainbow>");
    return args is ["--help"] ? 0 : 1;
}
catch (Exception error)
{
    Console.Error.WriteLine(error.Message);
    return 1;
}
