using System.Diagnostics;
using System.Text.Json;
using CrimsonHue.Core;

internal static class OpenRgbDiagnostic
{
    // This is an explicitly requested live development test, never the app's Demo.
    // It needs no Hue credentials and does not alter saved settings or installed files.
    public static readonly EntertainmentArea Front = new("openrgb-development-front", "In front of monitor", false,
        [new(0, new(0, 1, 0), [], "OpenRGB front")]);

    public static async Task<int> RunAsync(bool physical, string? reportPath, bool identify = false)
    {
        using var cancel = new CancellationTokenSource();
        ConsoleCancelEventHandler handler = (_, e) => { e.Cancel = true; cancel.Cancel(); };
        Console.CancelKeyPress += handler;
        try
        {
            await using var client = new OpenRgbClient();
            await client.ConnectAsync(token: cancel.Token);
            var devices = await client.InventoryAsync(cancel.Token);
            Console.WriteLine($"OpenRGB protocol {client.ServerVersion}; negotiated {client.ProtocolVersion}; {devices.Count} devices");
            foreach (var d in devices)
                Console.WriteLine($"  {d.Name}: {d.Colors.Length} logical slots, {d.PreviewLeds().Length} candidate LEDs; mode {d.Modes.ElementAtOrDefault(d.ActiveMode)?.Name ?? "per-zone"}; Direct {d.DirectMode >= 0 && !d.HasZoneModes}");
            if (!physical)
            {
                if (reportPath != null)
                {
                    // SDK cache evidence only: no mode/color writes and no physical measurement.
                    var report = new { At = DateTimeOffset.UtcNow, Kind = "Read-only SDK state; not optical readback",
                        client.ServerVersion, client.ProtocolVersion, Devices = devices.Select(d => Inspect(d, client.ProtocolVersion)) };
                    await using var file = new FileStream(Path.GetFullPath(reportPath), FileMode.CreateNew, FileAccess.Write);
                    await JsonSerializer.SerializeAsync(file, report, new JsonSerializerOptions { WriteIndented = true }, cancel.Token);
                    Console.WriteLine("Read-only SDK state report: " + Path.GetFullPath(reportPath));
                }
                return 0;
            }
            if (identify) return await IdentifyAsync(client, devices, cancel.Token, reportPath);

            var saved = new SettingsStore().LoadSettings();
            var manual = saved.Mapping ?? LightingProfile.Defaults;
            var preferences = saved.Lighting ?? (saved.Mapping is null ? new() : LightingProfile.FromLegacy(manual));
            var mapping = LightingProfile.Resolve(preferences, manual);
            mapping = (saved.OpenRgb ?? new OpenRgbLayout()).Mapping(mapping);
            mapping = mapping with { Brightness = Math.Min(.25, mapping.Brightness) };
            var endpoint = TelemetryClient.ValidateEndpoint(saved.TelemetryAddress);
            var telemetry = new TelemetryState(); var ambient = new AmbientState();
            var readers = new[] { new TelemetryClient(telemetry).RunAsync(endpoint, cancel.Token),
                new AmbientClient(ambient).RunAsync(AmbientClient.FromTelemetryEndpoint(endpoint), cancel.Token) };
            await using var output = new OpenRgbOutputSession(client);
            var samples = new List<object>();
            string? failure = null; var startedAt = DateTimeOffset.UtcNow;
            try
            {
                var preflight = Stopwatch.StartNew();
                while (telemetry.Read(out _) == null && preflight.Elapsed.TotalSeconds < 3)
                    await Task.Delay(50, cancel.Token);
                if (telemetry.Read(out var status) == null) throw new IOException("No fresh CDT data: " + status);
                Console.WriteLine("LIVE HARDWARE TEST: 20 seconds, one front position, maximum RGB level 25%. Previous states will be restored. Ctrl+C stops.");
                await output.StartAsync(devices, cancel.Token);
                var clock = Stopwatch.StartNew(); var previous = clock.Elapsed.TotalSeconds; var nextCheck = 0.0;
                var mapper = new LightMapper(); using var timer = new PeriodicTimer(TimeSpan.FromMilliseconds(50));
                while (clock.Elapsed.TotalSeconds < 20)
                {
                    var frame = telemetry.Read(out status);
                    if (frame == null) throw new IOException("CDT data became unavailable: " + status);
                    var elapsed = clock.Elapsed.TotalSeconds;
                    if (elapsed >= nextCheck)
                    { await output.VerifyOwnershipAsync(cancel.Token); nextCheck = elapsed + 1; }
                    // Recheck after SDK I/O so an old snapshot cannot be mapped as fresh.
                    frame = telemetry.Read(out status) ?? throw new IOException("CDT data became unavailable: " + status);
                    var sky = ambient.Read(out _);
                    var color = mapper.Map(frame, Front, mapping, elapsed - previous, sky)[0].Rgb;
                    await output.WriteAsync(color, cancel.Token);
                    samples.Add(new { Seconds = elapsed, frame.CaptureSequence, Sources = frame.Lights.Count,
                        Ambient = sky?.WorkingLevel, Rgb = color });
                    previous = elapsed;
                    await timer.WaitForNextTickAsync(cancel.Token);
                }
            }
            catch (Exception ex) when (ex is IOException or OperationCanceledException or CrimsonHueException)
            { failure = ex.Message; Console.WriteLine("Stopped: " + failure); }
            finally
            {
                await output.RestoreAsync(); cancel.Cancel(); await Task.WhenAll(readers);
            }
            foreach (var r in output.Restoration) Console.WriteLine($"  Restore {r.Device}: {r.Restored} — {r.Detail}");
            Console.WriteLine($"Mapped {samples.Count} frames; sent {output.ColorWrites} device color updates.");
            if (reportPath != null)
            {
                var report = JsonSerializer.Serialize(new { StartedAt = startedAt, DurationLimitSeconds = 20,
                    client.ServerVersion, client.ProtocolVersion, OutputDevices = output.DeviceCount,
                    Position = Front.Channels[0].Position, BrightnessCap = mapping.Brightness,
                    Devices = devices.Select(d => new { d.Name, CandidateLeds = d.PreviewLeds().Length }),
                    Failure = failure, output.ColorWrites, output.Restoration, Samples = samples }, new JsonSerializerOptions { WriteIndented = true });
                await File.WriteAllTextAsync(reportPath, report);
            }
            return failure is null && output.Restoration.All(r => r.Restored) ? 0 : 2;
        }
        finally { Console.CancelKeyPress -= handler; }
    }

    private static object Inspect(OpenRgbDevice device, uint protocol)
    {
        var mode = device.Modes.ElementAtOrDefault(device.ActiveMode);
        object? brightness = null;
        if (mode != null)
        {
            using var reader = new BinaryReader(new MemoryStream(mode.WireData, false));
            reader.ReadBytes(reader.ReadUInt16()); // Mode name, not hardware identity.
            if (protocol < 6) reader.ReadUInt32();
            reader.ReadUInt32(); // Flags; mode data was already bounded/validated by the SDK parser.
            var fields = Enumerable.Range(0, 9).Select(_ => reader.ReadUInt32()).ToArray();
            brightness = new { Minimum = fields[2], Maximum = fields[3], Current = fields[7] };
        }
        var controlled = device.PreviewLeds();
        string Hex(uint c) => $"#{(c & 255):X2}{((c >> 8) & 255):X2}{((c >> 16) & 255):X2}";
        return new { device.Name, device.Identity, Mode = mode?.Name, ModeFlags = mode?.Flags,
            ModeBrightness = brightness, ControlledLeds = controlled.Length, TotalLeds = device.Colors.Length,
            ControlledColors = controlled.Select(i => device.Colors[i]).GroupBy(c => c)
                .Select(g => new { Hex = Hex(g.Key), Count = g.Count() }),
            Zones = device.Zones.Select(z => new { z.Name, z.LedCount }) };
    }

    // Explicit physical identification, separate from both live CDT mapping and
    // the app's synthetic Demo. Fixed colors make an otherwise-black scene's
    // hardware path observable, using the same bounded ownership/cleanup session.
    private static async Task<int> IdentifyAsync(OpenRgbClient client, IReadOnlyList<OpenRgbDevice> devices,
        CancellationToken token, string? reportPath)
    {
        await using var output = new OpenRgbOutputSession(client);
        var startedAt = DateTimeOffset.UtcNow; string? failure = null;
        var stages = new List<object>();
        try
        {
            Console.WriteLine("PHYSICAL IDENTIFICATION: Red, Green, Blue, White; four seconds each; RGB level 25%. Ctrl+C stops.");
            await output.StartAsync(devices, token);
            foreach (var (name, rgb) in new (string, Vec3)[] {
                ("Red", new(.25,0,0)), ("Green", new(0,.25,0)),
                ("Blue", new(0,0,.25)), ("White", new(.25,.25,.25)) })
            {
                await output.VerifyOwnershipAsync(token);
                await output.WriteAsync(rgb, token);
                Console.WriteLine("  " + name);
                stages.Add(new { Color = name, Rgb = rgb, At = DateTimeOffset.UtcNow });
                var phase = Stopwatch.StartNew();
                while (phase.Elapsed < TimeSpan.FromSeconds(4))
                {
                    await Task.Delay(TimeSpan.FromMilliseconds(500), token);
                    await output.VerifyOwnershipAsync(token);
                }
            }
        }
        catch (Exception ex) when (ex is IOException or OperationCanceledException)
        { failure = ex.Message; Console.WriteLine("Stopped: " + failure); }
        finally { await output.RestoreAsync(); }
        foreach (var r in output.Restoration) Console.WriteLine($"  Restore {r.Device}: {r.Restored} — {r.Detail}");
        if (reportPath != null)
            await File.WriteAllTextAsync(reportPath, JsonSerializer.Serialize(new {
                StartedAt = startedAt, Kind = "Physical identification (no CDT mapping)",
                client.ServerVersion, client.ProtocolVersion, output.DeviceCount, output.ColorWrites,
                Failure = failure, Stages = stages, output.Restoration }, new JsonSerializerOptions { WriteIndented = true }));
        return failure is null && output.Restoration.All(r => r.Restored) ? 0 : 2;
    }
}
