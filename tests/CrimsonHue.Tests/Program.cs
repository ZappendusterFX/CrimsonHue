using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Net.WebSockets;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using CrimsonHue.Core;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

var tests = new List<(string Name, Func<Task> Run)>();
void Add(string name, Action run) => tests.Add((name, () => { run(); return Task.CompletedTask; }));
void Async(string name, Func<Task> run) => tests.Add((name, run));
void Assert(bool condition, string message = "Assertion failed") { if (!condition) throw new Exception(message); }
void Reject(Action action) { try { action(); } catch { return; } throw new Exception("Expected rejection"); }
async Task RejectAsync(Func<Task> action) { try { await action(); } catch { return; } throw new Exception("Expected rejection"); }
var area = Fixtures.Area();
Add("Parse current rendered feed without depending on authored availability", () =>
{
    var frame = TelemetryParser.Parse(Fixtures.Snapshot(), out _);
    Assert(frame?.Lights.Count == 2);
    Assert(frame!.Camera.Position == new Vec3(0, 2, 0));
});
Add("Accept additive schema fields and ignore authored RGB", () =>
{
    var node = JsonNode.Parse(Fixtures.Snapshot())!;
    node["schemaVersion"] = "1.9"; node["futureAmbientField"] = 123;
    node["lights"]!["sources"] = new JsonArray(new JsonObject { ["colorLinear"] = new JsonObject { ["x"] = 99999, ["y"] = 99999, ["z"] = 99999 } });
    Assert(TelemetryParser.Parse(Encoding.UTF8.GetBytes(node.ToJsonString()), out _)!.Lights.Count == 2);
});
foreach (var (name, change) in new (string, Action<JsonNode>)[]
{
    ("Unknown schema major", n => n["schemaVersion"] = "2.0"),
    ("Old schema without rendered lights", n => n["schemaVersion"] = "1.1"),
    ("Unavailable game", n => n["game"]!["state"] = "loading"),
    ("Invalid axes", n => n["coordinateSystem"]!["upAxis"] = "z"),
    ("Missing capability", n => n["capabilities"] = new JsonArray()),
    ("Unavailable light feed", n => n["lights"]!["rendered"]!["status"] = "unavailable"),
    ("Producer age beyond 500 ms", n => n["lights"]!["rendered"]!["ageMilliseconds"] = 501),
    ("Invalid camera basis", n => n["lights"]!["rendered"]!["camera"]!["right"]!["x"] = 0),
    ("Negative RGB", n => n["lights"]!["rendered"]!["sources"]![0]!["colorLinear"]!["x"] = -1),
    ("Negative sequence", n => n["sequence"] = -1)
}) Add("Reject: " + name, () =>
{
    var node = JsonNode.Parse(Fixtures.Snapshot())!; change(node);
    Assert(TelemetryParser.Parse(Encoding.UTF8.GetBytes(node.ToJsonString()), out _) == null);
});
Add("Valid empty feed remains available", () =>
{
    var node = JsonNode.Parse(Fixtures.Snapshot())!; node["lights"]!["rendered"]!["sources"] = new JsonArray();
    Assert(TelemetryParser.Parse(Encoding.UTF8.GetBytes(node.ToJsonString()), out _)?.Lights.Count == 0);
});
Add("Malformed JSON is unavailable", () => Assert(TelemetryParser.Parse("{"u8.ToArray(), out _) == null));
Add("Envelope duplicates cannot refresh light freshness", () =>
{
    var state = new TelemetryState(); var first = Fixtures.Snapshot(10);
    state.Accept(first); Assert(state.Read(out _) != null);
    state.Accept(Fixtures.Snapshot(10, DateTimeOffset.UtcNow.AddSeconds(20)));
    Assert(state.Read(out _) != null, "Duplicate replaced valid frame");
    Assert(state.Read(out _, DateTimeOffset.UtcNow.AddSeconds(1)) == null);
});
Add("Unavailable sequence prevents replay of older valid data", () =>
{
    var state = new TelemetryState(); state.Accept(Fixtures.Snapshot(10));
    var unavailable = JsonNode.Parse(Fixtures.Snapshot(12))!; unavailable["game"]!["state"] = "loading";
    state.Accept(Encoding.UTF8.GetBytes(unavailable.ToJsonString())); state.Accept(Fixtures.Snapshot(11));
    Assert(state.Read(out _) == null);
    state.Reset("new host"); state.Accept(Fixtures.Snapshot(0)); Assert(state.Read(out _) != null);
});
Add("Reject stale and future wall timestamps", () =>
{
    foreach (var offset in new[] { -10, 10 })
    {
        var state = new TelemetryState(); state.Accept(Fixtures.Snapshot(1, DateTimeOffset.UtcNow.AddSeconds(offset))); Assert(state.Read(out _) == null);
    }
});
Add("Hue layout preserves channel IDs, segments and height", () =>
{
    Assert(area.Channels.Count == 2 && area.LampCount == 1);
    Assert(area.Channels[1].Id == 5 && area.Channels[1].Members[0].SegmentIndex == 1);
});
Add("Reject duplicate channels and invalid coordinates", () =>
{
    var node = JsonNode.Parse(Fixtures.AreaJson())!;
    node["channels"]![1]!["channel_id"] = 2;
    Reject(() => BridgeClient.ParseArea(JsonSerializer.SerializeToElement(node)));
    node["channels"]![1]!["channel_id"] = 5; node["channels"]![0]!["position"]!["x"] = 100;
    Reject(() => BridgeClient.ParseArea(JsonSerializer.SerializeToElement(node)));
});
Add("Directional mapping keeps left red and right blue", () =>
{
    var frame = TelemetryParser.Parse(Fixtures.Snapshot(), out _)!;
    var colors = new LightMapper().Map(frame, area, new(1, 1, 35, 8, 0), 0.1);
    Assert(colors[0].Rgb.X > colors[0].Rgb.Z * 2);
    Assert(colors[1].Rgb.Z > colors[1].Rgb.X * 2);
});
Add("Camera yaw rotates world lights into room directions", () =>
{
    var frame = TelemetryParser.Parse(Fixtures.Snapshot(), out _)!;
    var reversed = frame with { Camera = frame.Camera with { Right = new(-1, 0, 0), Forward = new(0, 0, -1) } };
    var colors = new LightMapper().Map(reversed, area, new(1, 1, 35, 8, 0), 0.1);
    Assert(colors[0].Rgb.Z > colors[0].Rgb.X);
});
Add("Empty feed, cutoff and brightness cap", () =>
{
    var frame = TelemetryParser.Parse(Fixtures.Snapshot(), out _)!;
    Assert(new LightMapper().Map(frame with { Lights = [] }, area, new(), 1).All(c => c.Rgb.Length == 0));
    Assert(new LightMapper().Map(frame, area, new(Radius: 1), 1).All(c => c.Rgb.Length == 0));
    Assert(new LightMapper().Map(frame, area, new(10, 0.25, 100, 0.1, 0), 1).All(c => Math.Max(c.Rgb.X, Math.Max(c.Rgb.Y, c.Rgb.Z)) <= 0.250001));
});
Add("Mapper reset clears smoothing history", () =>
{
    var mapper = new LightMapper(); var frame = TelemetryParser.Parse(Fixtures.Snapshot(), out _)!;
    mapper.Map(frame, area, new(), 1); mapper.Reset();
    Assert(mapper.Map(frame with { Lights = [] }, area, new(), 0.1).All(c => c.Rgb.Length == 0));
    Reject(() => LightMapper.Validate(new(Gain: double.NaN)));
});
Add("HueStream v2 wire golden: UUID, RGB and noncontiguous channels", () =>
{
    var bytes = EntertainmentPacket.Build(area.Id, 254, [new(2, new(1, 0, 0.5)), new(5, new(0, 1, 0))]);
    Assert(bytes.Length == 66 && Encoding.ASCII.GetString(bytes, 0, 9) == "HueStream");
    Assert(bytes[9] == 2 && bytes[10] == 0 && bytes[11] == 254 && bytes[14] == 0);
    Assert(Encoding.ASCII.GetString(bytes, 16, 36) == area.Id);
    Assert(bytes.AsSpan(52).SequenceEqual(new byte[] { 2, 255, 255, 0, 0, 128, 0, 5, 0, 0, 255, 255, 0, 0 }));
    Reject(() => EntertainmentPacket.Build(area.Id, 0, [new(1, new(double.NaN, 0, 0))]));
    Reject(() => EntertainmentPacket.Build(area.Id, 0, [new(1, default), new(1, default)]));
});
Add("Local endpoint restrictions", () =>
{
    Assert(BridgeClient.NormalizeAddress("192.168.2.109") == "https://192.168.2.109");
    Reject(() => BridgeClient.NormalizeAddress("http://192.168.2.109"));
    Reject(() => BridgeClient.NormalizeAddress("https://example.com"));
    Reject(() => BridgeClient.NormalizeAddress("https://192.168.2.109/path"));
    Reject(() => TelemetryClient.ValidateEndpoint("ws://192.168.2.109/v1/stream"));
    Assert(TelemetryClient.ValidateEndpoint("ws://127.0.0.1:27311/v1/stream").IsLoopback);
});
Async("HTTPS pairing: pin trust, link-button error, client key and channel discovery", async () =>
{
    await using var mock = await MockBridge.StartAsync(true);
    var probe = await BridgeClient.ProbeAsync(mock.Address);
    Assert(probe.CertificateSha256?.Length == 64);
    await RejectAsync(() => BridgeClient.PairAsync(probe));
    mock.LinkButton = true;
    var credentials = await BridgeClient.PairAsync(probe);
    Assert(credentials.ClientKey == MockBridge.ClientKey && mock.RequestedClientKey);
    using var bridge = new BridgeClient(credentials);
    var areas = await bridge.GetAreasAsync(); Assert(areas[0].Channels[0].Label == "Test gradient");
    using var wrong = new BridgeClient(credentials with { CertificateSha256 = new string('0', 64) });
    await RejectAsync(() => wrong.GetAreasAsync());
    using var unauthorized = new BridgeClient(credentials with { ApplicationKey = "incorrect" });
    await RejectAsync(() => unauthorized.GetAreasAsync());
});
Async("Session starts, sends, stops and restores selected light only", async () =>
{
    await using var mock = await MockBridge.StartAsync();
    using var bridge = new BridgeClient(mock.Credentials);
    var transport = new SpyTransport(); var session = new StreamingSession(bridge, () => transport);
    var state = new TelemetryState(); state.Accept(Fixtures.Snapshot());
    using var cancel = new CancellationTokenSource(10000);
    var running = session.RunAsync(area, state, () => new(), cancel.Token);
    await Fixtures.WaitUntil(() => transport.Packets.Count > 1);
    cancel.Cancel(); await running;
    Assert(mock.Starts == 1 && mock.Stops == 1 && mock.Restored == 1);
    Assert(transport.Disposed && transport.Packets.Last().Skip(52).Where((_, i) => i % 7 != 0).All(v => v == 0));
});
Async("Failed DTLS handshake releases REST lease and restores lights", async () =>
{
    await using var mock = await MockBridge.StartAsync(); using var bridge = new BridgeClient(mock.Credentials);
    var state = new TelemetryState(); state.Accept(Fixtures.Snapshot());
    using var cancel = new CancellationTokenSource(10000);
    await RejectAsync(() => new StreamingSession(bridge, () => new SpyTransport { FailConnect = true }).RunAsync(area, state, () => new(), cancel.Token));
    Assert(mock.Starts == 1 && mock.Stops == 1 && mock.Restored == 1);
});
Async("Busy Entertainment area is never taken over", async () =>
{
    await using var mock = await MockBridge.StartAsync(); mock.Active = true; mock.Owner = "other-app";
    using var bridge = new BridgeClient(mock.Credentials); var state = new TelemetryState(); state.Accept(Fixtures.Snapshot());
    await RejectAsync(() => new StreamingSession(bridge, () => new SpyTransport()).RunAsync(area, state, () => new(), default));
    Assert(mock.Starts == 0 && mock.Stops == 0 && mock.Restored == 0);
});
Async("Stop during bridge activation reconciles and releases the pending lease", async () =>
{
    await using var mock = await MockBridge.StartAsync(); mock.StartDelayMs = 250;
    using var bridge = new BridgeClient(mock.Credentials); var state = new TelemetryState(); state.Accept(Fixtures.Snapshot());
    using var cancel = new CancellationTokenSource(10000);
    var run = new StreamingSession(bridge, () => new SpyTransport()).RunAsync(area, state, () => new(), cancel.Token);
    await Fixtures.WaitUntil(() => mock.Starts == 1); cancel.Cancel(); await run;
    Assert(mock.Stops == 1 && mock.Restored == 1 && !mock.Active);
});
Async("Stale telemetry clears colors then releases area", async () =>
{
    await using var mock = await MockBridge.StartAsync(); using var bridge = new BridgeClient(mock.Credentials);
    var state = new TelemetryState(); state.Accept(Fixtures.Snapshot()); var transport = new SpyTransport();
    using var cancel = new CancellationTokenSource(10000);
    var run = new StreamingSession(bridge, () => transport).RunAsync(area, state, () => new(), cancel.Token);
    await Fixtures.WaitUntil(() => mock.Stops > 0, 5000); cancel.Cancel(); await run;
    Assert(mock.Restored == 1 && transport.Disposed);
});
Async("Other app takeover prevents cleanup from overwriting it", async () =>
{
    await using var mock = await MockBridge.StartAsync(); using var bridge = new BridgeClient(mock.Credentials);
    var state = new TelemetryState(); var transport = new SpyTransport();
    using var cancel = new CancellationTokenSource(10000);
    var producer = Fixtures.Produce(state, cancel.Token);
    var session = new StreamingSession(bridge, () => transport);
    var run = session.RunAsync(area, state, () => new(), cancel.Token);
    await Fixtures.WaitUntil(() => transport.Packets.Count > 1); mock.Owner = "other-app";
    await RejectAsync(() => run); cancel.Cancel(); await producer;
    Assert(mock.Stops == 0 && mock.Restored == 0);
});
Async("Fragmented large WebSocket messages and close invalidation", async () =>
{
    var builder = WebApplication.CreateBuilder(); builder.Logging.ClearProviders(); builder.WebHost.UseUrls("http://127.0.0.1:0");
    await using var app = builder.Build(); app.UseWebSockets();
    var sent = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
    var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
    app.Map("/v1/stream", async context =>
    {
        using var ws = await context.WebSockets.AcceptWebSocketAsync();
        var node = JsonNode.Parse(Fixtures.Snapshot())!; node["futureData"] = new string('x', 100000);
        var data = Encoding.UTF8.GetBytes(node.ToJsonString());
        await ws.SendAsync(data.AsMemory(0, 50000), WebSocketMessageType.Text, false, default);
        await ws.SendAsync(data.AsMemory(50000), WebSocketMessageType.Text, true, default); sent.SetResult();
        await release.Task;
        await ws.CloseOutputAsync(WebSocketCloseStatus.NormalClosure, "done", default);
    });
    await app.StartAsync(); using var client = new ClientWebSocket(); using var cancel = new CancellationTokenSource(10000);
    await client.ConnectAsync(new Uri(app.Urls.Single().Replace("http:", "ws:") + "/v1/stream"), cancel.Token);
    var state = new TelemetryState(); var receiving = TelemetryClient.ReceiveAsync(client, state, cancel.Token);
    await sent.Task; await Fixtures.WaitUntil(() => state.Read(out _) != null);
    release.SetResult(); await receiving; Assert(state.Read(out _) == null);
});
Async("Real DTLS-PSK interoperability against OpenSSL", async () =>
{
    var openssl = args.SkipWhile(x => x != "--openssl").Skip(1).FirstOrDefault() ?? @"C:\Program Files\Git\usr\bin\openssl.exe";
    if (!File.Exists(openssl)) throw new Exception("OpenSSL required for DTLS interoperability test; pass --openssl path.");
    using var portSocket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
    portSocket.Bind(new IPEndPoint(IPAddress.Loopback, 0)); var port = ((IPEndPoint)portSocket.LocalEndPoint!).Port; portSocket.Close();
    var start = new ProcessStartInfo(openssl) { UseShellExecute = false, CreateNoWindow = true, RedirectStandardOutput = true, RedirectStandardError = true, RedirectStandardInput = true };
    foreach (var a in new[] { "s_server", "-dtls1_2", "-psk", MockBridge.ClientKey, "-psk_identity", "test-app", "-nocert", "-accept", "127.0.0.1:" + port, "-quiet", "-cipher", "PSK-AES128-GCM-SHA256" }) start.ArgumentList.Add(a);
    using var process = Process.Start(start)!;
    var error = process.StandardError.ReadToEndAsync();
    try
    {
        await Task.Delay(350); using var cancel = new CancellationTokenSource(10000);
        using var transport = new EntertainmentTransport(port);
        await transport.ConnectAsync(new("http://127.0.0.1", "test-app", MockBridge.ClientKey, null), cancel.Token);
        var packet = EntertainmentPacket.Build(area.Id, 8, [new(2, new(1, 0.25, 0)), new(5, new(0, 0, 1))]);
        transport.Send(packet);
        var actual = new byte[packet.Length]; await process.StandardOutput.BaseStream.ReadExactlyAsync(actual, cancel.Token);
        Assert(packet.SequenceEqual(actual), "OpenSSL decrypted bytes differ from packet");
    }
    finally { if (!process.HasExited) process.Kill(); await process.WaitForExitAsync(); var stderr = await error; if (process.ExitCode != 0 && !stderr.Contains("unexpected eof")) { /* server terminated by test */ } }
});
Add("DPAPI secrets roundtrip without plaintext in settings", () =>
{
    var directory = Path.Combine(Path.GetTempPath(), "CrimsonHue-tests-" + Guid.NewGuid().ToString("N"));
    var store = new SettingsStore(directory);
    var credentials = new BridgeCredentials("https://192.168.2.109", "synthetic-app-key", MockBridge.ClientKey, new string('A', 64));
    store.SaveCredentials(credentials); store.SaveSettings(new());
    Assert(store.LoadCredentials() == credentials);
    Assert(!Encoding.UTF8.GetString(File.ReadAllBytes(Path.Combine(directory, "bridge.secrets"))).Contains("synthetic-app-key"));
    Assert(!File.ReadAllText(Path.Combine(directory, "settings.json")).Contains("synthetic-app-key"));
    // Only delete the exact synthetic files created by this test.
    File.Delete(Path.Combine(directory, "bridge.secrets")); File.Delete(Path.Combine(directory, "settings.json")); Directory.Delete(directory);
});
if (args.Contains("--live")) Async("LIVE read-only bridge and progressing game WebSocket", async () =>
{
    var probe = await BridgeClient.ProbeAsync("192.168.2.109");
    Console.WriteLine($"  Bridge: {probe.Name}; HTTPS certificate received: {probe.CertificateSha256?.Length == 64}");
    var state = new TelemetryState(); using var cancel = new CancellationTokenSource(10000);
    var task = new TelemetryClient(state).RunAsync(TelemetryClient.ValidateEndpoint("ws://127.0.0.1:27311/v1/stream"), cancel.Token);
    var captures = new HashSet<long>(); var sequences = new HashSet<long>(); var maxLights = 0;
    for (var i = 0; i < 40; i++)
    {
        await Task.Delay(100); var f = state.Read(out _); if (f == null) continue;
        captures.Add(f.CaptureSequence); sequences.Add(f.Sequence); maxLights = Math.Max(maxLights, f.Lights.Count);
        var mapped = new LightMapper().Map(f, area, new(), 0.1); Assert(mapped.All(c => c.Rgb.IsFinite));
    }
    cancel.Cancel(); await task;
    Console.WriteLine($"  Live control: {sequences.Count} envelopes; {captures.Count} captures; up to {maxLights} contributions");
    Assert(captures.Count > 10 && sequences.Count > 10, "No progressing live control");
});

var failures = 0;
foreach (var test in tests)
{
    try { await test.Run(); Console.WriteLine("PASS " + test.Name); }
    catch (Exception ex) { failures++; Console.WriteLine("FAIL " + test.Name + "\n" + ex); }
}
Console.WriteLine($"{tests.Count - failures}/{tests.Count} passed");
return failures == 0 ? 0 : 1;

static class Fixtures
{
    public const string Id = "12345678-1234-1234-1234-123456789abc";
    private static readonly JsonSerializerOptions Options = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };
    public static byte[] Snapshot(long sequence = 1, DateTimeOffset? at = null)
    {
        var now = at ?? DateTimeOffset.UtcNow;
        var pose = new { position = new Vec3(0, 2, 0), right = new Vec3(1, 0, 0), up = new Vec3(0, 1, 0), forward = new Vec3(0, 0, 1) };
        return JsonSerializer.SerializeToUtf8Bytes(new
        {
            schemaVersion = "1.4", sequence, capturedAt = now, game = new { state = "playing" },
            coordinateSystem = new { unit = "game-unit", handedness = "right", upAxis = "y" },
            capabilities = new[] { "lights.rendered", "player.position", "camera.transform" }, player = new { position = new Vec3(0, 0, 0) },
            camera = new { position = new Vec3(999, 999, 999) },
            lights = new { status = "unavailable", rendered = new { status = "available", source = "filtered-manylights", captureSequence = sequence,
                capturedAt = now, ageMilliseconds = 0, camera = pose,
                sources = new[] { new { position = new Vec3(-5, 2, 5), colorLinear = new Vec3(2, 0, 0) }, new { position = new Vec3(5, 2, 5), colorLinear = new Vec3(0, 0, 2) } } } }
        }, Options);
    }
    public static string AreaJson(bool active = false, string owner = "test-app") => JsonSerializer.Serialize(new
    {
        id = Id, metadata = new { name = "Test area" }, status = active ? "active" : "inactive", active_streamer = new { rid = owner },
        channels = new[] {
            new { channel_id = 2, position = new Vec3(-0.8, 0.8, 0.2), members = new[] { new { index = 0, service = new { rid = "ent-light" } } } },
            new { channel_id = 5, position = new Vec3(0.8, 0.8, 0.2), members = new[] { new { index = 1, service = new { rid = "ent-light" } } } }
        }
    }, Options);
    public static EntertainmentArea Area() { using var doc = JsonDocument.Parse(AreaJson()); return BridgeClient.ParseArea(doc.RootElement); }
    public static async Task WaitUntil(Func<bool> test, int ms = 3000)
    {
        var watch = Stopwatch.StartNew(); while (!test()) { if (watch.ElapsedMilliseconds > ms) throw new Exception("Condition timed out"); await Task.Delay(20); }
    }
    public static async Task Produce(TelemetryState state, CancellationToken token)
    {
        long sequence = 1;
        while (!token.IsCancellationRequested) { state.Accept(Snapshot(sequence++)); try { await Task.Delay(50, token); } catch (OperationCanceledException) { } }
    }
}

sealed class SpyTransport : IEntertainmentTransport
{
    public System.Collections.Concurrent.ConcurrentQueue<byte[]> Packets { get; } = new();
    public bool FailConnect { get; init; }
    public bool Disposed { get; private set; }
    public Task ConnectAsync(BridgeCredentials credentials, CancellationToken token) => FailConnect ? Task.FromException(new IOException("Synthetic handshake failure")) : Task.CompletedTask;
    public void Send(byte[] packet) => Packets.Enqueue(packet);
    public void Dispose() => Disposed = true;
}

sealed class MockBridge : IAsyncDisposable
{
    public const string ClientKey = "00112233445566778899AABBCCDDEEFF";
    public bool LinkButton; public bool RequestedClientKey; public bool Active;
    public string Owner = "test-app";
    public int Starts; public int Stops; public int Restored;
    public int StartDelayMs;
    public string Address = "";
    public BridgeCredentials Credentials => new(Address, "test-app", ClientKey, null);
    private WebApplication app = null!;
    private X509Certificate2? certificate;
    public static async Task<MockBridge> StartAsync(bool https = false)
    {
        var mock = new MockBridge(); var builder = WebApplication.CreateBuilder(); builder.Logging.ClearProviders();
        if (https)
        {
            using var rsa = RSA.Create(2048); var request = new CertificateRequest("CN=CrimsonHue-test", rsa, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
            using var generated = request.CreateSelfSigned(DateTimeOffset.UtcNow.AddDays(-1), DateTimeOffset.UtcNow.AddDays(1));
            mock.certificate = new X509Certificate2(generated.Export(X509ContentType.Pfx));
            builder.WebHost.ConfigureKestrel(k => k.Listen(IPAddress.Loopback, 0, l => l.UseHttps(mock.certificate)));
        }
        else builder.WebHost.UseUrls("http://127.0.0.1:0");
        mock.app = builder.Build();
        mock.app.Run(async ctx =>
        {
            ctx.Response.ContentType = "application/json";
            var path = ctx.Request.Path.Value!;
            if (path == "/api/config") { await ctx.Response.WriteAsJsonAsync(new { name = "Test bridge", bridgeid = "test-bridge" }); return; }
            if (path == "/api")
            {
                using var body = await JsonDocument.ParseAsync(ctx.Request.Body);
                mock.RequestedClientKey = body.RootElement.GetProperty("generateclientkey").GetBoolean();
                if (!mock.LinkButton) await ctx.Response.WriteAsync("[{\"error\":{\"type\":101}}]");
                else await ctx.Response.WriteAsJsonAsync(new[] { new { success = new { username = "test-app", clientkey = ClientKey } } });
                return;
            }
            if (ctx.Request.Headers["hue-application-key"] != "test-app") { ctx.Response.StatusCode = 403; return; }
            if (path.EndsWith("entertainment_configuration/" + Fixtures.Id) && ctx.Request.Method == "PUT")
            {
                using var body = await JsonDocument.ParseAsync(ctx.Request.Body);
                mock.Active = body.RootElement.GetProperty("action").GetString() == "start";
                if (mock.Active) { mock.Starts++; mock.Owner = "test-app"; } else mock.Stops++;
                if (mock.Active && mock.StartDelayMs > 0) await Task.Delay(mock.StartDelayMs);
                await ctx.Response.WriteAsync("{\"data\":[],\"errors\":[]}"); return;
            }
            if (path.EndsWith("/light/light-id") && ctx.Request.Method == "PUT")
            {
                using var body = await JsonDocument.ParseAsync(ctx.Request.Body);
                if (body.RootElement.GetProperty("on").GetProperty("on").GetBoolean()) throw new Exception("Incorrect restore state");
                mock.Restored++; await ctx.Response.WriteAsync("{\"data\":[],\"errors\":[]}"); return;
            }
            string data;
            if (path.Contains("entertainment_configuration")) data = Fixtures.AreaJson(mock.Active, mock.Owner);
            else if (path.EndsWith("/entertainment")) data = "{\"id\":\"ent-light\",\"renderer_reference\":{\"rid\":\"light-id\"}}";
            else if (path.EndsWith("/light")) data = "{\"id\":\"light-id\",\"metadata\":{\"name\":\"Test gradient\"},\"on\":{\"on\":false},\"dimming\":{\"brightness\":42},\"color\":{\"xy\":{\"x\":0.3,\"y\":0.3}}}";
            else { ctx.Response.StatusCode = 404; return; }
            await ctx.Response.WriteAsync("{\"errors\":[],\"data\":[" + data + "]}");
        });
        await mock.app.StartAsync(); mock.Address = mock.app.Urls.Single(); return mock;
    }
    public async ValueTask DisposeAsync() { await app.DisposeAsync(); certificate?.Dispose(); }
}
