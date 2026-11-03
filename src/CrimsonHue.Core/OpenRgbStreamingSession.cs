using System.Diagnostics;
using System.Net.Sockets;

namespace CrimsonHue.Core;

public sealed record OpenRgbStreamState(string Status, bool Sending = false, int Devices = 0,
    long Frames = 0, Vec3 Rgb = default, IReadOnlyDictionary<string, Vec3>? DeviceColors = null);

/// <summary>Private front-position trial, with its own mapper and SDK ownership.</summary>
public sealed class OpenRgbStreamingSession(int port = 6742)
{
    public static EntertainmentArea Front { get; } = new("openrgb-front", "OpenRGB front", false,
        [new(0, new(0, 1, 0), [], "In front of monitor")]);
    private OpenRgbStreamState state = new("OpenRGB · waiting for fresh CDT");
    private int running;
    public OpenRgbStreamState State => Volatile.Read(ref state);

    public async Task RunAsync(TelemetryState telemetry, AmbientState ambient,
        Func<MappingSettings> settings, CancellationToken token, Func<OpenRgbLayout>? layout = null)
    {
        if (Interlocked.Exchange(ref running, 1) != 0) throw new InvalidOperationException("OpenRGB session already used.");
        long frames = 0; var acquired = false;
        try
        {
            while (!token.IsCancellationRequested)
            {
                var config = layout?.Invoke() ?? new OpenRgbLayout(Port: port);
                config.Validate();
                if (!config.Enabled || config.Devices is { Count: 0 } || config.Devices?.All(d => !d.Enabled) == true)
                { Set("OpenRGB · disabled or no devices selected"); await Task.Delay(100, token); continue; }
                Set("OpenRGB · waiting for fresh CDT");
                if (telemetry.Read(out _) == null) { await Task.Delay(50, token); continue; }
                await using var client = new OpenRgbClient();
                Set("OpenRGB · connecting to localhost:6742");
                await client.ConnectAsync(config.Port, token);
                var devices = await client.InventoryAsync(token);
                var targets = config.Targets(devices);
                if (targets.Count == 0) { Set("OpenRGB · selected devices not available"); await Task.Delay(1000, token); continue; }
                // Inventory is read-only; recheck freshness before taking control.
                if (telemetry.Read(out _) == null) continue;
                await using var output = new OpenRgbOutputSession(client);
                string? failure = null;
                try
                {
                    token.ThrowIfCancellationRequested();
                    // Finish each bounded SDK operation before observing Stop.
                    // Canceling midway through an ACK would invalidate the socket
                    // needed for restoration. No game/Hue thread waits on this I/O.
                    await output.StartAsync(targets.Select(t => t.Device).ToArray());
                    acquired = true;
                    var mapper = new LightMapper();
                    var clock = Stopwatch.StartNew(); var previous = 0.0; var nextCheck = 0.0;
                    double? staleSince = null;
                    using var timer = new PeriodicTimer(TimeSpan.FromMilliseconds(50));
                    while (!token.IsCancellationRequested)
                    {
                        var current = layout?.Invoke() ?? config;
                        current.Validate();
                        var placed = current.Targets(devices);
                        if (!current.Enabled || current.Port != config.Port ||
                            !placed.Select(t => t.Device.Identity).SequenceEqual(targets.Select(t => t.Device.Identity))) break;
                        if (clock.Elapsed.TotalSeconds >= nextCheck)
                        {
                            await output.VerifyOwnershipAsync();
                            nextCheck = clock.Elapsed.TotalSeconds + 1;
                        }
                        token.ThrowIfCancellationRequested();
                        var now = clock.Elapsed.TotalSeconds;
                        var frame = telemetry.Read(out _);
                        IReadOnlyList<ChannelColor> colors;
                        if (frame == null)
                        {
                            mapper.Reset(); colors = placed.Select((_, i) => new ChannelColor((byte)i, default)).ToArray(); staleSince ??= now;
                        }
                        else
                        {
                            staleSince = null;
                            var mapping = settings();
                            colors = mapper.Map(frame, OpenRgbLayout.Area(placed), mapping with { Brightness = Math.Min(current.Brightness, mapping.Brightness) },
                                previous == 0 ? .05 : now - previous, ambient.Read(out _));
                        }
                        var deviceColors = placed.Select((t, i) => (t.Device.Identity, colors[i].Rgb)).ToDictionary(p => p.Identity, p => p.Rgb);
                        await output.WriteDevicesAsync(deviceColors);
                        previous = now; frames++;
                        Volatile.Write(ref state, new(frame == null ? "OpenRGB · CDT stale, output cleared" :
                            $"OpenRGB · {output.DeviceCount} devices · room layout", true, output.DeviceCount, frames, colors[0].Rgb, deviceColors));
                        if (staleSince is { } since && now - since >= 1) break;
                        await timer.WaitForNextTickAsync(token);
                    }
                }
                catch (OperationCanceledException) when (token.IsCancellationRequested) { }
                catch (Exception ex) when (ex is IOException or SocketException or CrimsonHueException or ArgumentException)
                { failure = ex.Message; }
                finally
                {
                    Set("OpenRGB · stopping and restoring");
                    var restored = await output.RestoreAsync();
                    if (restored.Any(r => !r.Restored))
                        failure = "restoration incomplete: " + string.Join("; ", restored.Where(r => !r.Restored).Select(r => r.Device + ": " + r.Detail));
                }
                if (failure != null) { Set("OpenRGB stopped · " + failure); return; }
                // Missing CDT releases the devices and waits for fresh input;
                // SDK errors or another writer stop this backend until Start.
            }
            Set(acquired ? "OpenRGB stopped · prior states restored" : "OpenRGB stopped");
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested)
        { Set(acquired ? "OpenRGB stopped · prior states restored" : "OpenRGB stopped"); }
        catch (Exception ex) when (ex is IOException or SocketException or CrimsonHueException or ArgumentException)
        { Set("OpenRGB stopped · " + ex.Message); }

        void Set(string message) => Volatile.Write(ref state, new(message, Frames: frames));
    }
}
