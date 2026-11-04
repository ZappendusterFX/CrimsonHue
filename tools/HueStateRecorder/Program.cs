using System.Diagnostics;
using System.Text.Json;
using Microsoft.Diagnostics.Runtime;

// Passive, non-suspending reads of the existing immutable credential-free UI state.
// No heap walk, injected calls, debugger events, protected settings or memory writes.
// Usage: HueStateRecorder.exe PID output.jsonl [seconds=30 | --probe]
if (args.Length is < 2 or > 3) throw new ArgumentException("Expected PID output.jsonl [1..30 seconds | --probe]");
var processId = int.Parse(args[0]);
var probe = args.Length == 3 && args[2] == "--probe";
var seconds = probe ? 0 : args.Length == 3 ? int.Parse(args[2]) : 30;
if (processId <= 0 || (!probe && seconds is < 1 or > 30)) throw new ArgumentException("Invalid PID or duration");
var path = Path.GetFullPath(args[1]);
Directory.CreateDirectory(Path.GetDirectoryName(path)!);
using var file = new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.Read);
using var writer = new StreamWriter(file) { AutoFlush = true };
using var target = DataTarget.AttachToProcess(processId, suspend: false);
using var runtime = target.ClrVersions.Single().CreateRuntime();
var allowed = new[] { "ready", "demo", "smoke", "telemetry", "capture", "ambient", "stream", "canStart", "canStop",
    "rawAmbient", "controls", "channels", "lighting", "menuLighting", "wallWashers", "openRgb", "openRgbRoom" };
var clock = Stopwatch.StartNew();
var samples = 0;
var rejected = 0;
do
{
    var began = clock.Elapsed.TotalMilliseconds;
    try
    {
        runtime.FlushCachedData();
        var module = runtime.EnumerateModules().Single(m => Path.GetFileName(m.Name) == "CrimsonHue.dll");
        var type = module.GetTypeByName("CrimsonHue.App.AsiEntry") ?? throw new InvalidDataException("Controller type unavailable");
        var field = type.GetStaticFieldByName("state") ?? throw new InvalidDataException("Snapshot field unavailable");
        Dictionary<string, JsonElement>? state = null;
        foreach (var domain in runtime.AppDomains)
        {
            var value = field.ReadObject(domain);
            if (!value.IsValid || !value.IsArray) continue;
            var array = value.AsArray();
            if (array.Length is < 2 or > 262144 || array.Type.ComponentType?.Name != "System.Byte") continue;
            var bytes = array.ReadValues<byte>(0, array.Length) ?? throw new InvalidDataException("Snapshot unreadable");
            var repeat = array.ReadValues<byte>(0, array.Length) ?? throw new InvalidDataException("Snapshot unreadable");
            if (!bytes.SequenceEqual(repeat)) throw new InvalidDataException("Concurrent collection");
            using var json = JsonDocument.Parse(bytes);
            var root = json.RootElement;
            if (!root.TryGetProperty("ready", out _)) throw new InvalidDataException("Snapshot not ready");
            state = allowed.Where(name => root.TryGetProperty(name, out _))
                .ToDictionary(name => name, name => root.GetProperty(name).Clone());
            break;
        }
        if (state is null) throw new InvalidDataException("No readable immutable snapshot");
        writer.WriteLine(JsonSerializer.Serialize(new { at = DateTimeOffset.UtcNow, elapsedMs = clock.Elapsed.TotalMilliseconds,
            readMs = clock.Elapsed.TotalMilliseconds - began, processId, state }));
        samples++;
    }
    catch (Exception exception)
    {
        // Exception types only: never persist arbitrary runtime messages or memory.
        writer.WriteLine(JsonSerializer.Serialize(new { at = DateTimeOffset.UtcNow, elapsedMs = clock.Elapsed.TotalMilliseconds,
            errorType = exception.GetType().Name }));
        rejected++;
    }
    if (file.Position > 64L * 1024 * 1024) throw new IOException("UI recording size bound exceeded");
    if (!probe)
    {
        var remaining = seconds * 1000 - clock.Elapsed.TotalMilliseconds;
        if (remaining > 0) await Task.Delay((int)Math.Min(remaining, Math.Max(1, 200 - (clock.Elapsed.TotalMilliseconds - began))));
    }
} while (!probe && clock.Elapsed.TotalSeconds < seconds);
var summary = new { processId, seconds, probe, samples, rejected, elapsedMs = clock.Elapsed.TotalMilliseconds,
    evidence = "Passive immutable UI reads; process was not suspended; no physical-output acceptance" };
File.WriteAllText(path + ".summary.json", JsonSerializer.Serialize(summary));
Console.WriteLine(JsonSerializer.Serialize(summary));
return samples > 0 ? 0 : 1;
