using System.Buffers.Binary;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.Json.Nodes;
using CrimsonHue.Core;

internal static class OpenRgbTests
{
    private static void Assert(bool condition, string message = "OpenRGB assertion failed")
    { if (!condition) throw new Exception(message); }
    private static async Task Reject(Func<Task> action)
    { try { await action(); } catch (IOException) { return; } catch (OperationCanceledException) { return; } throw new Exception("Expected rejection"); }

    public static void Register(Action<string, Action> add, Action<string, Func<Task>> asyncTest)
    {
        add("OpenRGB desk coverage retains a blue effect above devices without whitening or changing Hue settings", () =>
        {
            var at = DateTimeOffset.UtcNow;
            var frame = new TelemetryFrame(1,1,at,at,0,default,
                new(default,new(1,0,0),new(0,1,0),new(0,0,1)), [new(new(0,2,3),new(0,0,20))]);
            var area = OpenRgbStreamingSession.Front with { Channels = [new(0,new(-.24,.79,-.21),[],"Desk keyboard")] };
            var shared = LightingProfile.Defaults with { Brightness = 1, SmoothingMs = 0, AmbientSmoothingMs = 0 };
            var sky = new AmbientFrame(1,at,0,0,1.3);
            var old = new LightMapper().Map(frame,area,shared with { Brightness = .25 },.05,sky)[0].Rgb;
            var next = new LightMapper().Map(frame,area,new OpenRgbLayout().Mapping(shared),.05,sky)[0].Rgb;
            Assert(Math.Abs(old.X-old.Z)<1e-12 && old.X>.08, "Old narrow desk mapping no longer reproduces neutral-only output");
            Assert(next.Z>.2 && next.X<.06 && next.Y<.06 && next.Z<=.25);
            Assert(shared.SourceDiscRadiusDegrees==30 && shared.AmbientOutput==.4 && shared.Brightness==1);
        });
        add("OpenRGB response defaults migrate old layouts, roundtrip, and reject invalid settings", () =>
        {
            var old = System.Text.Json.JsonSerializer.Deserialize<OpenRgbLayout>("{\"Brightness\":0.5,\"Devices\":[]}")!;
            Assert(old.SourceRadiusDegrees==90 && old.BackgroundAmount==.25 && old.Brightness==.5);
            var custom = old with { SourceRadiusDegrees=65, BackgroundAmount=.7 };
            var restored=System.Text.Json.JsonSerializer.Deserialize<OpenRgbLayout>(System.Text.Json.JsonSerializer.Serialize(custom))!;
            Assert(restored.SourceRadiusDegrees==65 && restored.BackgroundAmount==.7 && restored.Brightness==.5 && restored.Devices?.Count==0);
            foreach(var invalid in new[] { custom with {SourceRadiusDegrees=0}, custom with {SourceRadiusDegrees=181},
                custom with {BackgroundAmount=double.NaN}, custom with {BackgroundAmount=1.1} })
            {
                var rejected=false;try{invalid.Validate();}catch(CrimsonHueException){rejected=true;}Assert(rejected);
            }
        });
        add("OpenRGB background balance is independent and neutral sources never acquire invented color", () =>
        {
            var at=DateTimeOffset.UtcNow; var shared=LightingProfile.Defaults with {SmoothingMs=0,AmbientSmoothingMs=0};
            var frame=new TelemetryFrame(1,1,at,at,0,default,new(default,new(1,0,0),new(0,1,0),new(0,0,1)),[]);
            var sky=new AmbientFrame(1,at,0,0,1.3);
            var quiet=new LightMapper();var full=new LightMapper();
            quiet.Map(frame,OpenRgbStreamingSession.Front,new OpenRgbLayout().Mapping(shared),.05,sky);
            full.Map(frame,OpenRgbStreamingSession.Front,new OpenRgbLayout(BackgroundAmount:1).Mapping(shared),.05,sky);
            Assert(Math.Abs(quiet.AmbientFill*4-full.AmbientFill)<1e-12);
            var local = new LightMapper().Map(frame with {Lights=[new(new(0,0,3),new(1,1,1))]},
                OpenRgbStreamingSession.Front,new OpenRgbLayout(BackgroundAmount:0).Mapping(shared),.05,sky)[0].Rgb;
            Assert(local.X==local.Y && local.Y==local.Z && local.X>0);
        });
        add("OpenRGB placements survive settings roundtrip and SDK IDs changing", () =>
        {
            var device = OpenRgbProtocol.ParseDevice(42, new Device().Bytes());
            Assert(device.Identity == OpenRgbProtocol.ParseDevice(999, new Device().Bytes(6), 6).Identity);
            var layout = new OpenRgbLayout().Discover([device]);
            var position = new Vec3(-.7, .4, -.3);
            layout = layout with { Devices = [layout.Devices![0] with { Position = position }] };
            var saved = System.Text.Json.JsonSerializer.Serialize(new AppSettings(OpenRgb: layout));
            var restored = System.Text.Json.JsonSerializer.Deserialize<AppSettings>(saved)!.OpenRgb!;
            var targets = restored.Targets([device with { Id = 999 }]);
            Assert(targets.Single().Device.Id == 999 && targets.Single().Position == position);
            Assert(!saved.Contains("private-test") && !saved.Contains("\"Id\""));
            var offline = restored.Discover([]);
            Assert(offline.Devices!.Single().Position == position);
        });
        add("OpenRGB layout rejects unsafe coordinates and duplicate identities and respects selection", () =>
        {
            var device = OpenRgbProtocol.ParseDevice(42, new Device().Bytes());
            var layout = new OpenRgbLayout().Discover([device]);
            var entry = layout.Devices![0];
            foreach (var invalid in new[] { layout with { Devices = [entry, entry] },
                layout with { Devices = [entry with { Position = new(0,0,double.NaN) }] },
                layout with { Devices = [entry with { Position = new(0,0,1.01) }] }, layout with { Port = 0 } })
            {
                var rejected = false; try { invalid.Validate(); } catch (CrimsonHueException) { rejected = true; }
                Assert(rejected);
            }
            Assert((layout with { Devices = [entry with { Enabled = false }] }).Targets([device]).Count == 0);
        });
        asyncTest("OpenRGB spatial sync sends distinct device colors and applies live 3D position edits", async () =>
        {
            await using var host = new Server { SecondModel = new Device { Name = "Second RGB" } };
            var firstOriginal = host.Model.Bytes(); var secondOriginal = host.SecondModel.Bytes();
            var first = OpenRgbProtocol.ParseDevice(host.ControllerId, firstOriginal);
            var second = OpenRgbProtocol.ParseDevice(77, secondOriginal);
            var layout = new OpenRgbLayout(Port: host.Port, Devices: [new(first.Identity, first.Name, new(0,1,0)),
                new(second.Identity, second.Name, new(0,-1,0))]);
            var telemetry = new TelemetryState();
            using var source = new CancellationTokenSource(); using var stop = new CancellationTokenSource();
            var feed = ProduceFront(telemetry, source.Token); var session = new OpenRgbStreamingSession();
            var task = session.RunAsync(telemetry, new(), () => LightingProfile.Defaults with { SmoothingMs = 0 }, stop.Token,
                () => Volatile.Read(ref layout));
            try
            {
                await Fixtures.WaitUntil(() => session.State.DeviceColors?.GetValueOrDefault(first.Identity).X > .1);
                Assert(session.State.DeviceColors![second.Identity] == default);
                Volatile.Write(ref layout, layout with { Devices = layout.Devices!.Select(p => p with { Position = p.Position * -1 }).ToArray() });
                await Fixtures.WaitUntil(() => session.State.DeviceColors?.GetValueOrDefault(second.Identity).X > .1);
                Assert(session.State.DeviceColors![first.Identity] == default);
            }
            finally { source.Cancel(); stop.Cancel(); await feed; await task.WaitAsync(TimeSpan.FromSeconds(5)); }
            Assert(host.Model.Bytes().SequenceEqual(firstOriginal) && host.SecondModel.Bytes().SequenceEqual(secondOriginal));
        });
        add("OpenRGB v4 parser preserves mode properties and excludes matrix holes and gamepad status LEDs", () =>
        {
            var wire = new Device(); var parsed = OpenRgbProtocol.ParseDevice(0, wire.Bytes());
            Assert(parsed.PreviewLeds().SequenceEqual(new[] { 0, 2 }));
            Assert(parsed.Modes[0].WireData.SequenceEqual(wire.Modes[0]));
            wire.Gamepad = true; parsed = OpenRgbProtocol.ParseDevice(0, wire.Bytes());
            Assert(parsed.PreviewLeds().SequenceEqual(new[] { 0 }));
            var identity = parsed.Identity; wire.Colors[0] = 123;
            Assert(OpenRgbProtocol.ParseDevice(0, wire.Bytes()).Identity == identity);
            wire.Name = "Replacement";
            Assert(OpenRgbProtocol.ParseDevice(0, wire.Bytes()).Identity != identity);
        });
        add("OpenRGB color packets use exact v4 lengths and RGB byte order", () =>
        {
            var packet = OpenRgbProtocol.ColorsPacket([OpenRgbProtocol.Encode(new(1, .5, 0)), 0x00FF0000]);
            Assert(packet.SequenceEqual(new byte[] { 14,0,0,0, 2,0, 255,128,0,0, 0,0,255,0 }));
        });
        add("OpenRGB virtual front uses camera-facing room direction and the output cap", () =>
        {
            var at = DateTimeOffset.UtcNow;
            var frame = new TelemetryFrame(1, 1, at, at, 0, default,
                new(default, new(1,0,0), new(0,1,0), new(0,0,1)), [new(new(0,0,1), new(1,0,0))]);
            var settings = LightingProfile.Defaults with { Brightness = .25, SmoothingMs = 0, FadeStart = 2 };
            var front = new LightMapper().Map(frame, OpenRgbDiagnostic.Front, settings, .05)[0].Rgb;
            var rear = new LightMapper().Map(frame with { Lights = [new(new(0,0,-1), new(1,0,0))] },
                OpenRgbDiagnostic.Front, settings, .05)[0].Rgb;
            Assert(front.X > .1 && front.X <= .25 && front.Y == 0 && front.Z == 0 && rear == default);
        });
        foreach (var version in new uint[] { 4, 5, 6 })
        asyncTest($"OpenRGB fragmented SDK {version} inventory uses negotiated IDs and stays read-only", async () =>
        {
            await using var host = new Server { Fragment = true, Version = version };
            await using var client = new OpenRgbClient(); await client.ConnectAsync(host.Port);
            var devices = await client.InventoryAsync();
            Assert(client.ProtocolVersion == version && devices.Count == 1 && devices[0].Colors.Length == 3);
            Assert(devices[0].Id == host.ControllerId);
            Assert(host.Commands.SequenceEqual(new uint[] { 40, 0, 1 }));
        });
        foreach (var version in new uint[] { 4, 6 })
        asyncTest($"OpenRGB SDK {version} output preserves padding, deduplicates colors and restores mode properties", async () =>
        {
            await using var host = new Server { Version = version };
            var initial = host.Model.Bytes();
            await using var client = new OpenRgbClient(); await client.ConnectAsync(host.Port);
            await using var session = new OpenRgbOutputSession(client);
            await session.StartAsync(await client.InventoryAsync());
            await session.WriteAsync(new(.25,0,0)); await session.WriteAsync(new(.25,0,0));
            await session.VerifyOwnershipAsync();
            Assert(host.Model.Colors.SequenceEqual(new uint[] { 64, 0x221100, 64 }));
            Assert(session.ColorWrites == 1);
            var restore = await session.RestoreAsync();
            Assert(restore.Single().Restored && host.Model.Bytes().SequenceEqual(initial));
            Assert(host.Commands.Where(IsWrite).SequenceEqual(new uint[] { 1101,1050,1050,1101 }));
        });
        asyncTest("OpenRGB DualSense lightbar leaves player LEDs unchanged and restores Direct state", async () =>
        {
            await using var host = new Server(); host.Model.Gamepad = true; host.Model.Active = 1;
            var initial = host.Model.Bytes();
            await using var client = new OpenRgbClient(); await client.ConnectAsync(host.Port);
            await using var session = new OpenRgbOutputSession(client);
            await session.StartAsync(await client.InventoryAsync()); await session.WriteAsync(new(0,0,.25));
            await session.VerifyOwnershipAsync();
            Assert(host.Model.Colors.SequenceEqual(new uint[] { 0x400000, 0x221100, 0x443322 }));
            Assert((await session.RestoreAsync()).Single().Restored && initial.SequenceEqual(host.Model.Bytes()));
            Assert(!host.Commands.Contains(1101u));
        });
        foreach (var changeMode in new[] { false, true })
            asyncTest($"OpenRGB external {(changeMode ? "mode" : "color")} edit stops output and survives cleanup", async () =>
            {
                await using var host = new Server();
                await using var client = new OpenRgbClient(); await client.ConnectAsync(host.Port);
                await using var session = new OpenRgbOutputSession(client);
                await session.StartAsync(await client.InventoryAsync()); await session.WriteAsync(new(.1,.2,.3));
                await session.VerifyOwnershipAsync();
                host.Edit(d => { if (changeMode) d.Active = 0; else d.Colors[0] = 0x123456; });
                var writes = host.Commands.Count(IsWrite);
                await Reject(() => session.VerifyOwnershipAsync());
                await Reject(() => session.WriteAsync(new(.4,0,0)));
                Assert(!(await session.RestoreAsync()).Single().Restored && host.Commands.Count(IsWrite) == writes);
                Assert(changeMode ? host.Model.Active == 0 : host.Model.Colors[0] == 0x123456);
            });
        asyncTest("OpenRGB list change invalidates indexes and prevents restore to a different device", async () =>
        {
            await using var host = new Server();
            await using var client = new OpenRgbClient(); await client.ConnectAsync(host.Port);
            await using var session = new OpenRgbOutputSession(client);
            await session.StartAsync(await client.InventoryAsync()); await session.WriteAsync(new(.1,.2,.3));
            await session.VerifyOwnershipAsync(); var writes = host.Commands.Count(IsWrite);
            host.NextDeviceReply = 100;
            await Reject(() => session.VerifyOwnershipAsync());
            Assert(!client.Healthy && !(await session.RestoreAsync()).Single().Restored);
            Assert(host.Commands.Count(IsWrite) == writes);
        });
        asyncTest("OpenRGB canceled output still restores on a healthy connection", async () =>
        {
            await using var host = new Server(); var initial = host.Model.Bytes();
            await using var client = new OpenRgbClient(); await client.ConnectAsync(host.Port);
            await using var session = new OpenRgbOutputSession(client);
            await session.StartAsync(await client.InventoryAsync()); await session.WriteAsync(new(.1,.2,.3));
            using var canceled = new CancellationTokenSource(); canceled.Cancel();
            await Reject(() => session.WriteAsync(new(.5,0,0), canceled.Token));
            Assert((await session.RestoreAsync()).Single().Restored && initial.SequenceEqual(host.Model.Bytes()));
        });
        foreach (var rejectKind in new[] { "autosave", "no Direct" })
            asyncTest($"OpenRGB refuses {rejectKind} devices without hardware writes", async () =>
            {
                await using var host = new Server();
                if (rejectKind == "autosave") host.Model.Modes[0] = Device.Mode("Rainbow", 1u << 9, 0);
                else host.Model.Modes[1] = Device.Mode("Static", 1u << 5, 1);
                await using var client = new OpenRgbClient(); await client.ConnectAsync(host.Port);
                await using var session = new OpenRgbOutputSession(client);
                await Reject(async () => await session.StartAsync(await client.InventoryAsync()));
                Assert(!host.Commands.Any(IsWrite));
            });
        foreach (var problem in new[] { "oversize", "timeout", "truncated layout" })
            asyncTest($"OpenRGB rejects {problem} replies within bounded time", async () =>
            {
                await using var host = new Server();
                await using var client = new OpenRgbClient(); await client.ConnectAsync(host.Port);
                host.BadReply = problem;
                await Reject(async () => await client.DeviceAsync(host.ControllerId));
                Assert(!host.Commands.Any(IsWrite));
                if (problem != "truncated layout") Assert(!client.Healthy);
            });
        asyncTest("OpenRGB SDK 6 rejected write ACK invalidates output without further writes", async () =>
        {
            await using var host = new Server { RejectWrite = true };
            await using var client = new OpenRgbClient(); await client.ConnectAsync(host.Port);
            await using var session = new OpenRgbOutputSession(client);
            await Reject(async () => await session.StartAsync(await client.InventoryAsync()));
            Assert(!client.Healthy && host.Model.Active == 0);
            await session.RestoreAsync();
            Assert(host.Commands.Count(IsWrite) == 1);
        });
        asyncTest("OpenRGB continuous sync waits for telemetry without touching the SDK", async () =>
        {
            await using var host = new Server();
            using var stop = new CancellationTokenSource();
            var session = new OpenRgbStreamingSession(host.Port);
            var task = session.RunAsync(new(), new(), () => LightingProfile.Defaults, stop.Token);
            try
            {
                await Task.Delay(150);
                Assert(host.Commands.Count == 0 && !session.State.Sending);
            }
            finally { stop.Cancel(); await task.WaitAsync(TimeSpan.FromSeconds(4)); }
        });
        asyncTest("OpenRGB continuous sync maps fresh front light, caps RGB, clears stale input and restores on Stop", async () =>
        {
            await using var host = new Server(); var original = host.Model.Bytes();
            var telemetry = new TelemetryState(); var ambient = new AmbientState();
            using var source = new CancellationTokenSource(); using var stop = new CancellationTokenSource();
            var feed = ProduceFront(telemetry, source.Token);
            var session = new OpenRgbStreamingSession(host.Port);
            var task = session.RunAsync(telemetry, ambient, () => LightingProfile.Defaults with { SmoothingMs = 0 }, stop.Token);
            try
            {
                await Fixtures.WaitUntil(() => session.State.Sending && session.State.Rgb.X > .1);
                Assert(session.State.Rgb.X <= .25 && session.State.Rgb.Y == 0 && session.State.Rgb.Z == 0);
                source.Cancel(); await feed;
                await Fixtures.WaitUntil(() => session.State.Sending && session.State.Rgb == default);
            }
            finally { source.Cancel(); stop.Cancel(); await feed; await task.WaitAsync(TimeSpan.FromSeconds(5)); }
            Assert(host.Model.Bytes().SequenceEqual(original) && session.State.Status.Contains("restored"));
        });
        asyncTest("OpenRGB continuous sync preserves external edits and stops its backend", async () =>
        {
            await using var host = new Server();
            var telemetry = new TelemetryState();
            using var source = new CancellationTokenSource(); using var stop = new CancellationTokenSource();
            var feed = ProduceFront(telemetry, source.Token);
            var session = new OpenRgbStreamingSession(host.Port);
            var task = session.RunAsync(telemetry, new(), () => LightingProfile.Defaults with { SmoothingMs = 0 }, stop.Token);
            try
            {
                await Fixtures.WaitUntil(() => session.State.Sending && session.State.Rgb.X > .1);
                host.Edit(d => d.Active = 0);
                await task.WaitAsync(TimeSpan.FromSeconds(5));
                Assert(!session.State.Sending && host.Model.Active == 0 && session.State.Status.Contains("External edit preserved"));
            }
            finally { source.Cancel(); stop.Cancel(); await feed; await task; }
        });
    }

    private static async Task ProduceFront(TelemetryState telemetry, CancellationToken token)
    {
        long sequence = 0;
        while (!token.IsCancellationRequested)
        {
            var node = JsonNode.Parse(Fixtures.Snapshot(++sequence))!;
            node["lights"]!["rendered"]!["sources"] = new JsonArray(new JsonObject {
                ["position"] = new JsonObject { ["x"] = 0, ["y"] = 1, ["z"] = 3 },
                ["colorLinear"] = new JsonObject { ["x"] = 2, ["y"] = 0, ["z"] = 0 } });
            telemetry.Accept(Encoding.UTF8.GetBytes(node.ToJsonString()));
            try { await Task.Delay(40, token); } catch (OperationCanceledException) { }
        }
    }

    private static bool IsWrite(uint command) => command >= 1000;

    // Synthetic device state: three slots, with a deliberately unused matrix
    // slot and duplicate matrix cell. Never opens an actual hardware port.
    private sealed class Device
    {
        public string Name = "Synthetic RGB controller";
        public bool Gamepad;
        public int Active;
        public uint[] Colors = [0x010203, 0x221100, 0x443322];
        public byte[][] Modes = [Mode("Rainbow", 0, 0), Mode("Direct", 1u << 5, 1)];
        private static void Text(BinaryWriter w, string value)
        { var bytes = Encoding.UTF8.GetBytes(value); w.Write((ushort)(bytes.Length + 1)); w.Write(bytes); w.Write((byte)0); }
        public static byte[] Mode(string name, uint flags, uint colorMode)
        {
            using var stream = new MemoryStream(); using var w = new BinaryWriter(stream);
            Text(w, name); w.Write(17); w.Write(flags);
            foreach (var v in new uint[] { 1, 100, 0, 255, 0, 3, 37, 199, 2 }) w.Write(v);
            w.Write(colorMode); w.Write((ushort)2); w.Write(0xCC8844u); w.Write(0x4488CCu);
            return stream.ToArray();
        }
        public static byte[] WireMode(byte[] mode, uint protocol)
        {
            if (protocol < 6) return mode;
            var offset = 2 + BinaryPrimitives.ReadUInt16LittleEndian(mode);
            return [..mode[..offset], ..mode[(offset + 4)..]];
        }
        public void AcceptMode(int index, byte[] wire, uint protocol)
        {
            Active = index;
            if (protocol < 6) { Modes[index] = wire; return; }
            var offset = 2 + BinaryPrimitives.ReadUInt16LittleEndian(wire);
            Modes[index] = [..wire[..offset], ..Modes[index][offset..(offset+4)], ..wire[offset..]];
        }
        public byte[] Bytes(uint protocol = 4)
        {
            using var stream = new MemoryStream(); using var w = new BinaryWriter(stream);
            w.Write(0u); w.Write(Gamepad ? 10 : 5);
            foreach (var s in new[] { Name, "Synthetic", "Test", "1", "private-test-serial", "private-test-path" }) Text(w, s);
            w.Write((ushort)Modes.Length); w.Write(Active); foreach (var mode in Modes) w.Write(WireMode(mode, protocol));
            w.Write((ushort)(Gamepad ? 2 : 1));
            void Zone(string name, uint count, bool matrix)
            {
                Text(w, name); w.Write(matrix ? 2 : 0); w.Write(count); w.Write(count); w.Write(count);
                w.Write((ushort)(matrix ? 24 : 0));
                if (matrix) foreach (var v in new uint[] { 2, 2, 0, uint.MaxValue, 2, 0 }) w.Write(v);
                w.Write((ushort)0);
                if (protocol >= 5) w.Write(0u);
                if (protocol >= 6) { w.Write(0); w.Write((ushort)0); Text(w, ""); }
            }
            if (Gamepad) { Zone("Lightbar", 1, false); Zone("Player LEDs", 2, false); }
            else Zone("Keyboard", 3, true);
            w.Write((ushort)3); for (uint i = 0; i < 3; i++) { Text(w, "LED " + i); if (protocol < 6) w.Write(i); }
            w.Write((ushort)3); foreach (var color in Colors) w.Write(color);
            if (protocol >= 5) { w.Write((ushort)0); w.Write(0u); }
            if (protocol >= 6) { Text(w, ""); w.Write(1u); w.Write((byte)0); }
            var bytes = stream.ToArray(); BinaryPrimitives.WriteUInt32LittleEndian(bytes, (uint)bytes.Length); return bytes;
        }
    }

    private sealed class Server : IAsyncDisposable
    {
        private readonly TcpListener listener = new(IPAddress.Loopback, 0);
        private readonly CancellationTokenSource stop = new();
        private readonly object gate = new();
        private readonly Task loop;
        public readonly Device Model = new();
        public Device? SecondModel;
        public readonly List<uint> Commands = [];
        public bool Fragment;
        public uint Version = 6;
        public uint ControllerId => Version >= 6 ? 42u : 0u;
        public bool RejectWrite;
        public uint? NextDeviceReply;
        public string? BadReply;
        public int Port => ((IPEndPoint)listener.LocalEndpoint).Port;
        public Server() { listener.Start(); loop = Run(); }
        public void Edit(Action<Device> action) { lock (gate) action(Model); }
        private async Task Run()
        {
            try
            {
                using var socket = await listener.AcceptTcpClientAsync(stop.Token); socket.NoDelay = true;
                using var stream = socket.GetStream();
                async Task Reply(uint command, uint id, byte[] body, bool oversized = false)
                {
                    var response = new byte[16 + body.Length]; "ORGB"u8.CopyTo(response);
                    BinaryPrimitives.WriteUInt32LittleEndian(response.AsSpan(4), id);
                    BinaryPrimitives.WriteUInt32LittleEndian(response.AsSpan(8), command);
                    BinaryPrimitives.WriteUInt32LittleEndian(response.AsSpan(12), oversized ?
                        (uint)OpenRgbProtocol.MaximumPacketBytes + 1 : (uint)body.Length);
                    body.CopyTo(response, 16);
                    var chunkSize = Fragment ? 3 : response.Length;
                    for (var i = 0; i < response.Length; i += chunkSize)
                        await stream.WriteAsync(response.AsMemory(i, Math.Min(chunkSize, response.Length - i)), stop.Token);
                }
                while (!stop.IsCancellationRequested)
                {
                    var header = new byte[16]; await stream.ReadExactlyAsync(header, stop.Token);
                    Assert(header.AsSpan(0,4).SequenceEqual("ORGB"u8));
                    var id = BinaryPrimitives.ReadUInt32LittleEndian(header.AsSpan(4));
                    var command = BinaryPrimitives.ReadUInt32LittleEndian(header.AsSpan(8));
                    var size = BinaryPrimitives.ReadUInt32LittleEndian(header.AsSpan(12));
                    Assert(size < 4096 && (command is 0 or 40 ? id == 0 : id == ControllerId || (SecondModel != null && id == 77))); var body = new byte[(int)size];
                    await stream.ReadExactlyAsync(body, stop.Token); byte[]? reply = null;
                    lock (gate)
                    {
                        Commands.Add(command);
                        var model = id == 77 ? SecondModel! : Model;
                        if (command == 1101)
                        {
                            Assert(BinaryPrimitives.ReadUInt32LittleEndian(body) == body.Length);
                            if (!RejectWrite) model.AcceptMode(BinaryPrimitives.ReadInt32LittleEndian(body.AsSpan(4)), body[8..], Version);
                        }
                        else if (command == 1050)
                        {
                            Assert(BinaryPrimitives.ReadUInt32LittleEndian(body) == body.Length &&
                                BinaryPrimitives.ReadUInt16LittleEndian(body.AsSpan(4)) == 3 && body.Length == 18);
                            if (!RejectWrite) model.Colors = Enumerable.Range(0,3).Select(i => BinaryPrimitives.ReadUInt32LittleEndian(body.AsSpan(6 + 4*i))).ToArray();
                        }
                        else
                        {
                            Assert(command is 0 or 1 or 40);
                            if (command == 40) Assert(body.SequenceEqual(new byte[] { 6,0,0,0 }));
                            if (command == 1) Assert(body.SequenceEqual(Version >= 6 ? [] : BitConverter.GetBytes(Version)));
                            reply = command switch { 40 => BitConverter.GetBytes(Version),
                                0 => Version >= 6 ? SecondModel == null ? [1,0,0,0,42,0,0,0] : [2,0,0,0,42,0,0,0,77,0,0,0] : [1,0,0,0], _ => model.Bytes(Version) };
                        }
                    }
                    if (BadReply == "timeout") { await Task.Delay(Timeout.Infinite, stop.Token); continue; }
                    if (reply != null)
                    {
                        if (BadReply == "truncated layout") reply = reply[..^1];
                        if (command == 1 && NextDeviceReply.HasValue) await Reply(NextDeviceReply.Value, id, []);
                        else await Reply(command, id, reply, BadReply == "oversize");
                    }
                    if (Version >= 6)
                    {
                        if (IsWrite(command) && !RejectWrite) await Reply(1150, id, new byte[8]);
                        if (command == 40) await Reply(51, 0, "Synthetic SDK server\0"u8.ToArray());
                        await Reply(10, id, [..BitConverter.GetBytes(command), ..BitConverter.GetBytes(RejectWrite && IsWrite(command) ? 4u : 0u)]);
                    }
                }
            }
            catch (Exception ex) when (ex is OperationCanceledException or IOException or SocketException) { }
        }
        public async ValueTask DisposeAsync() { stop.Cancel(); listener.Stop(); await loop; stop.Dispose(); }
    }
}
