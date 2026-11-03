namespace CrimsonHue.Core;

public sealed record OpenRgbRestoreResult(string Device, bool Restored, string Detail);

/// <summary>
/// Explicit live hardware output, independent of Hue stream ownership. Callers
/// serialize operations and restore in finally. The SDK has no exclusive lease:
/// observed external edits stop output and must not be overwritten by cleanup.
/// </summary>
public sealed class OpenRgbOutputSession(OpenRgbClient client) : IAsyncDisposable
{
    private sealed class Target(OpenRgbDevice original, int[] leds)
    {
        public OpenRgbDevice Original { get; } = original;
        public int[] Leds { get; } = leds;
        public uint[] ExpectedColors { get; set; } = original.Colors.ToArray();
        public bool Touched { get; set; }
        public bool Lost { get; set; }
    }

    private readonly List<Target> targets = [];
    private bool started, stopped;
    public IReadOnlyList<OpenRgbRestoreResult> Restoration { get; private set; } = [];
    public int DeviceCount => targets.Count;
    public int ColorWrites { get; private set; }

    public async Task StartAsync(IReadOnlyList<OpenRgbDevice> devices, CancellationToken token = default)
    {
        if (started || stopped) throw new InvalidOperationException("OpenRGB session cannot be restarted.");
        started = true;
        if (devices.Select(d => d.Id).Distinct().Count() != devices.Count)
            throw new ArgumentException("Duplicate OpenRGB device selection.");
        foreach (var device in devices)
        {
            var leds = device.PreviewLeds();
            if (device.HasZoneModes || device.DirectMode < 0 || device.Modes[device.ActiveMode].AutomaticSave || leds.Length == 0) continue;
            // Revalidate the snapshot immediately before taking control.
            var current = await client.DeviceAsync(device.Id, token);
            if (current.Identity != device.Identity || current.ActiveMode != device.ActiveMode ||
                !current.Colors.SequenceEqual(device.Colors) ||
                !current.Modes[current.ActiveMode].WireData.SequenceEqual(device.Modes[device.ActiveMode].WireData))
                throw new IOException($"OpenRGB state changed before start: {device.Name}.");
            var target = new Target(device, leds); targets.Add(target);
            if (device.ActiveMode != device.DirectMode)
            {
                target.Touched = true;
                await client.SetModeAsync(device, device.DirectMode, token);
                await VerifyAsync(target, token);
            }
        }
        if (targets.Count == 0) throw new IOException("No eligible Direct RGB zones for this live test.");
    }

    public async Task WriteAsync(Vec3 color, CancellationToken token = default)
    {
        if (!started || stopped) throw new InvalidOperationException("OpenRGB session is not running.");
        if (targets.Any(t => t.Lost)) throw new IOException("OpenRGB ownership was lost.");
        var encoded = OpenRgbProtocol.Encode(color);
        foreach (var target in targets)
        {
            if (target.Lost) throw new IOException("OpenRGB ownership was lost.");
            var colors = target.Original.Colors.ToArray();
            foreach (var index in target.Leds) colors[index] = encoded;
            if (colors.SequenceEqual(target.ExpectedColors)) continue;
            target.Touched = true;
            await client.SetColorsAsync(target.Original.Id, colors, token);
            target.ExpectedColors = colors; ColorWrites++;
        }
    }

    public async Task VerifyOwnershipAsync(CancellationToken token = default)
    {
        if (!started || stopped) throw new InvalidOperationException("OpenRGB session is not running.");
        foreach (var target in targets) await VerifyAsync(target, token);
    }

    private async Task VerifyAsync(Target target, CancellationToken token)
    {
        var current = await client.DeviceAsync(target.Original.Id, token);
        var original = target.Original;
        if (target.Lost || current.Identity != original.Identity || current.ActiveMode != original.DirectMode ||
            !current.Colors.SequenceEqual(target.ExpectedColors) ||
            !current.Modes[current.ActiveMode].WireData.SequenceEqual(original.Modes[original.DirectMode].WireData))
        {
            target.Lost = true;
            throw new IOException($"OpenRGB device state changed externally: {original.Name}. Output stopped.");
        }
    }

    public async Task<IReadOnlyList<OpenRgbRestoreResult>> RestoreAsync()
    {
        if (stopped) return Restoration;
        stopped = true;
        var results = new List<OpenRgbRestoreResult>();
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        foreach (var target in targets)
        {
            var original = target.Original;
            if (!target.Touched) { results.Add(new(original.Name, true, "Unchanged")); continue; }
            if (target.Lost) { results.Add(new(original.Name, false, "External edit preserved")); continue; }
            if (!client.Healthy || timeout.IsCancellationRequested)
            { results.Add(new(original.Name, false, "Connection unavailable; no stale-index writes")); continue; }
            try
            {
                await VerifyAsync(target, timeout.Token);
                await client.SetColorsAsync(original.Id, original.Colors, timeout.Token);
                if (original.ActiveMode != original.DirectMode)
                    await client.SetModeAsync(original, original.ActiveMode, timeout.Token);
                var restored = await client.DeviceAsync(original.Id, timeout.Token);
                var matches = restored.Identity == original.Identity && restored.ActiveMode == original.ActiveMode &&
                    restored.Colors.SequenceEqual(original.Colors) &&
                    restored.Modes[restored.ActiveMode].WireData.SequenceEqual(original.Modes[original.ActiveMode].WireData);
                results.Add(new(original.Name, matches, matches ? "Mode and colors verified" : "Readback differs after restoration"));
            }
            catch (Exception ex) when (ex is IOException or OperationCanceledException)
            { results.Add(new(original.Name, false, target.Lost ? "External edit preserved" : "Restore failed: " + ex.GetType().Name)); }
        }
        return Restoration = results;
    }

    public async ValueTask DisposeAsync() => await RestoreAsync();
}
