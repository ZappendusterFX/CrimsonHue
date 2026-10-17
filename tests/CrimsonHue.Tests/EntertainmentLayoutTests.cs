using System.Text.Json;
using System.Text.Json.Nodes;
using CrimsonHue.Core;

static class EntertainmentLayoutTests
{
    private static readonly Vec3 Low = new(-1, .18, -.54), High = new(-1, .18, 1);
    private static readonly JsonSerializerOptions Options = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };
    private static void Check(bool value, string message) { if (!value) throw new Exception(message); }
    private static string Locations(Vec3 high) => JsonSerializer.Serialize(new
    {
        service_locations = new[] { new { service = new { rid = "ent-light" }, positions = new[] { Low, high }, equalization_factor = 1 } }
    }, Options);
    private static JsonNode Node(Vec3 high)
    {
        var node = JsonNode.Parse(Fixtures.AreaJson())!;
        node["locations"] = JsonNode.Parse(Locations(high));
        return node;
    }
    private static EntertainmentArea Parse(JsonNode node) => BridgeClient.ParseArea(JsonSerializer.SerializeToElement(node));

    internal static void Register(Action<string, Action> add, Action<string, Func<Task>> async)
    {
        add("Hue service placement preserves both positions independently of channel geometry", () =>
        {
            var imported = Parse(Node(High));
            Check(imported.ServiceLocations is [{ ServiceId: "ent-light" }], "Service placement missing");
            Check(imported.ServiceLocations![0].Positions.SequenceEqual(new[] { Low, High }), "Service positions changed");
            Check(imported.Channels.Select(c => c.Position).SequenceEqual(Fixtures.Area().Channels.Select(c => c.Position)),
                "Service placement silently replaced bridge channel positions");
            Check(imported.Channels.Select(c => c.Id).SequenceEqual(new byte[] { 2, 5 }) && imported.LampCount == 1,
                "Wire identities changed");
        });
        add("Layout comparison detects service-only edits and ignores ordering and display state", () =>
        {
            var original = Parse(Node(High));
            var changed = Parse(Node(High with { Y = -.5 }));
            Check(!original.HasSameLayout(changed), "Service-only edit was invisible");
            Check(!original.HasSameLayout(original with { ServiceLocations = [] }), "Removed service placement was invisible");
            var reordered = original with { Name = "Renamed", Active = true,
                Channels = original.Channels.Reverse().Select(c => c with { Label = "Renamed light" }).ToArray() };
            Check(original.HasSameLayout(reordered), "Ordering, names or stream status changed geometry");
            var shared = original with { Channels = [original.Channels[0] with { Position = new(0, 1, 0),
                Members = [new("ent-light", 0), new("second-light", 1)] }],
                ServiceLocations = [original.ServiceLocations![0], new("second-light", [new(1, 1, 0)])] };
            Check(shared.HasSameLayout(shared with { Channels = [shared.Channels[0] with {
                    Position = new(0, 1, -0.0), Members = shared.Channels[0].Members.Reverse().ToArray() }],
                    ServiceLocations = shared.ServiceLocations!.Reverse().ToArray() }),
                "Member/service ordering or signed zero caused a false layout change");
            Check(!shared.HasSameLayout(shared with { ServiceLocations = [shared.ServiceLocations![0],
                shared.ServiceLocations[1] with { Brightness = .5 }] }), "Per-service balance edit was invisible");
        });
        add("Hue placement supports legacy position and rejects malformed service locations", () =>
        {
            var legacy = Node(High);
            var entry = legacy["locations"]!["service_locations"]![0]!.AsObject();
            entry["position"] = JsonSerializer.SerializeToNode(Low, Options);
            entry.Remove("positions");
            Check(Parse(legacy).ServiceLocations![0].Positions.SequenceEqual(new[] { Low }), "Legacy placement missing");
            entry["positions"] = JsonSerializer.SerializeToNode(new[] { High }, Options);
            Check(Parse(legacy).ServiceLocations![0].Positions.SequenceEqual(new[] { High }), "Deprecated position overrode current positions");
            foreach (var mutation in new Action<JsonNode>[] {
                n => n["locations"]!["service_locations"]![0]!["positions"] = new JsonArray(),
                n => n["locations"]!["service_locations"]![0]!["positions"]![0]!["x"] = 100,
                n => n["locations"]!["service_locations"]![0]!["equalization_factor"] = 1.1,
                n => n["locations"]!["service_locations"]!.AsArray().Add(n["locations"]!["service_locations"]![0]!.DeepClone()) })
            {
                var invalid = Node(High); mutation(invalid);
                var rejected = false;
                try { Parse(invalid); } catch (Exception ex) when (ex is CrimsonHueException or FormatException) { rejected = true; }
                Check(rejected, "Malformed service placement accepted");
            }
        });
        async("Changed service placement blocks stream start and segment identify before acquisition", async () =>
        {
            await using var mock = await MockBridge.StartAsync();
            mock.AreaLocationsJson = Locations(High);
            using var bridge = new BridgeClient(mock.Credentials);
            var area = (await bridge.GetAreasAsync()).Single();
            Volatile.Write(ref mock.AreaLocationsJson, Locations(High with { Y = -.5 }));
            var telemetry = new TelemetryState(); telemetry.Accept(Fixtures.Snapshot());
            var session = new StreamingSession(bridge, () => new SpyTransport());
            var startRejected = false; var identifyRejected = false;
            try { await session.RunAsync(area, telemetry, () => new(), default); }
            catch (CrimsonHueException ex) when (ex.Message.Contains("layout changed")) { startRejected = true; }
            try { await session.IdentifyAsync(area, area.Channels[0].Id, default); }
            catch (CrimsonHueException ex) when (ex.Message.Contains("layout before testing")) { identifyRejected = true; }
            Check(startRejected && identifyRejected && mock.Starts == 0 && mock.Restored == 0,
                "Stale service placement acquired or changed the bridge");
        });
        async("Service-only edit during streaming is detected and owned light states are restored", async () =>
        {
            await using var mock = await MockBridge.StartAsync();
            mock.AreaLocationsJson = Locations(High);
            using var bridge = new BridgeClient(mock.Credentials);
            var area = (await bridge.GetAreasAsync()).Single();
            var telemetry = new TelemetryState(); var transport = new SpyTransport();
            var session = new StreamingSession(bridge, () => transport);
            using var cancel = new CancellationTokenSource(TimeSpan.FromSeconds(10));
            var producer = Fixtures.Produce(telemetry, cancel.Token);
            var run = session.RunAsync(area, telemetry, () => new(), cancel.Token);
            var detected = false;
            try
            {
                await Fixtures.WaitUntil(() => transport.Packets.Count > 1);
                Volatile.Write(ref mock.AreaLocationsJson, Locations(High with { Y = -.5 }));
                try { await run; }
                catch (CrimsonHueException ex) when (ex.Message.Contains("layout changed during streaming")) { detected = true; }
            }
            finally
            {
                await cancel.CancelAsync(); await producer;
                if (!run.IsCompleted) await run;
            }
            Check(detected && mock.Starts == 1 && mock.Stops == 1 && mock.Restored == 1 &&
                transport.Disposed && !session.Sending, "Location-only edit did not stop and restore the owned session");
        });
    }
}
