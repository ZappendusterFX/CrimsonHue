using System.Diagnostics;
using System.Net.Sockets;

namespace CrimsonHue.Core;

public sealed record OpenRgbStreamState(string Status, bool Sending = false, int Devices = 0,
    long Frames = 0, Vec3 Rgb = default);

/// <summary>Private front-position trial, with its own mapper and SDK ownership.</summary>
public sealed class OpenRgbStreamingSession(int port = 6742)
{
    public static EntertainmentArea Front { get; } = new("openrgb-front", "OpenRGB front", false,
        [new(0, new(0, 1, 0), [], "In front of monitor")]);
    private OpenRgbStreamState state = new("OpenRGB · waiting for fresh CDT");
    private int running;
    public OpenRgbStreamState State => Volatile.Read(ref state);

    public async Task RunAsync(TelemetryState telemetry, AmbientState ambient,
        Func<MappingSettings> settings, CancellationToken token)
    {
        if (Interlocked.Exchange(ref running, 1) != 0) throw new InvalidOperationException("OpenRGB session already used.");
        long frames = 0; var acquired = false;
        try
        {
            while (!token.IsCancellationRequested)
            {
                Set("OpenRGB · waiting for fresh CDT");
                while (telemetry.Read(out _) == null) await Task.Delay(50, token);
                await using var client = new OpenRgbClient();
                Set("OpenRGB · connecting to localhost:6742");
                await client.ConnectAsync(port, token);
                var devices = await client.InventoryAsync(token);
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
                    await output.StartAsync(devices);
                    acquired = true;
                    var mapper = new LightMapper();
                    var clock = Stopwatch.StartNew(); var previous = 0.0; var nextCheck = 0.0;
                    double? staleSince = null;
                    using var timer = new PeriodicTimer(TimeSpan.FromMilliseconds(50));
                    while (!token.IsCancellationRequested)
                    {
                        if (clock.Elapsed.TotalSeconds >= nextCheck)
                        {
                            await output.VerifyOwnershipAsync();
                            nextCheck = clock.Elapsed.TotalSeconds + 1;
                        }
                        token.ThrowIfCancellationRequested();
                        var now = clock.Elapsed.TotalSeconds;
                        var frame = telemetry.Read(out _);
                        Vec3 rgb;
                        if (frame == null)
                        {
                            mapper.Reset(); rgb = default; staleSince ??= now;
                        }
                        else
                        {
                            staleSince = null;
                            var mapping = settings();
                            rgb = mapper.Map(frame, Front, mapping with { Brightness = Math.Min(.25, mapping.Brightness) },
                                previous == 0 ? .05 : now - previous, ambient.Read(out _))[0].Rgb;
                        }
                        await output.WriteAsync(rgb);
                        previous = now; frames++;
                        Volatile.Write(ref state, new(frame == null ? "OpenRGB · CDT stale, output cleared" :
                            $"OpenRGB · {output.DeviceCount} devices · front · max 25%", true, output.DeviceCount, frames, rgb));
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
