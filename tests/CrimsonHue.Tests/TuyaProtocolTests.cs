using System.Buffers.Binary;
using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using CrimsonHue.Core;

internal static class TuyaProtocolTests
{
    private const string TestKey = "0123456789abcdef"; // Public synthetic fixture, never a device credential.
    private static readonly byte[] Key = Encoding.ASCII.GetBytes(TestKey);
    private static readonly TuyaLamp Lamp = new("fixture", "Synthetic lamp", "127.0.0.1", "synthetic-device", default);
    private static readonly TuyaColorCommand Red = new(true, new(1, 0, 0), .5);
    private static void Assert(bool value, string reason) { if (!value) throw new Exception(reason); }
    private static void Reject(Action action)
    {
        try { action(); } catch (Exception e) when (e is InvalidDataException or CryptographicException or CrimsonHueException) { return; }
        throw new Exception("Invalid Tuya input was accepted.");
    }
    private static async Task RejectAsync(Func<Task> action, string? category = null)
    {
        try { await action(); }
        catch (CrimsonHueException e)
        {
            Assert(!e.ToString().Contains(TestKey, StringComparison.Ordinal) && !e.ToString().Contains("synthetic-device", StringComparison.Ordinal),
                "Tuya failure exposed a credential or raw device data.");
            if (category != null) Assert(e.Message.Contains(category, StringComparison.OrdinalIgnoreCase), "Wrong failure category.");
            return;
        }
        throw new Exception("Tuya operation did not report failure.");
    }

    public static void Register(Action<string, Action> add, Action<string, Func<Task>> asyncTest)
    {
        add("Tuya RGB HSV payload uses colour mode and omits white brightness", () =>
        {
            Assert(TuyaProtocol.Hsv(new(1, 0, 0), .5) == "000003e801f4", "Red HSV mismatch.");
            Assert(TuyaProtocol.Hsv(new(0, 1, 0), 1) == "007803e803e8", "Green HSV mismatch.");
            Assert(TuyaProtocol.Hsv(new(0, 0, 1), .1) == "00f003e80064", "Blue HSV mismatch.");
            Assert(TuyaProtocol.Hsv(new(.1, 0, 0), .5) == TuyaProtocol.Hsv(new(1, 0, 0), .5), "RGB intensity changed explicit HSV value.");
            Assert(TuyaProtocol.Hsv(new(1, 1, 1), 0) == "00000000000a", "Minimum on brightness mismatch.");
            Reject(() => TuyaProtocol.Hsv(new(double.NaN, 0, 0), .5));
            Reject(() => TuyaProtocol.Hsv(new(1, 0, 0), double.PositiveInfinity));
            var dps = TuyaProtocol.BuildDps(Lamp, Red);
            Assert(dps.Count == 3 && dps[20].GetBoolean() && dps[21].GetString() == "colour" &&
                dps[24].GetString() == "000003e801f4" && !dps.ContainsKey(22), "RGB DPS contract mismatch.");
            var off = TuyaProtocol.BuildDps(Lamp, Red with { Power = false });
            Assert(off.Count == 1 && !off[20].GetBoolean(), "Power off included mode/color DPS.");
            var payload = TuyaProtocol.ControlPayload(dps, 1700000000);
            Assert(payload.AsSpan(0, 3).SequenceEqual("3.5"u8) && payload.AsSpan(3, 12).ToArray().All(b => b == 0), "Missing 3.5 payload prefix.");
            using var json = JsonDocument.Parse(payload.AsMemory(15));
            Assert(json.RootElement.GetProperty("protocol").GetInt32() == 5 &&
                json.RootElement.GetProperty("t").GetString() == "1700000000" &&
                json.RootElement.GetProperty("data").GetProperty("dps").GetProperty("24").GetString() == "000003e801f4" &&
                !Encoding.UTF8.GetString(payload).Contains(Lamp.DeviceId), "Control JSON mismatch.");
            Reject(() => TuyaProtocol.BuildDps(Lamp with { Protocol = "3.3" }, Red));
            Reject(() => TuyaProtocol.KeyBytes("invalid-secret-length"));
        });
        add("Tuya 6699 packet and session derivation match independent AES-GCM vectors", () =>
        {
            // Independently generated with Node/OpenSSL createCipheriv aes-128-gcm,
            // synthetic key above, IV00..0b, AAD big-endian flags/seq/cmd/length.
            var expected = Convert.FromHexString("000066990000000000070000000d00000023000102030405060708090a0b9847a5706e185ecfbc056d20c2fb0bac85bbf30b1614da00009966");
            var iv = Enumerable.Range(0, 12).Select(i => (byte)i).ToArray();
            Assert(TuyaProtocol.Pack(7, 13, "example"u8, Key, iv).SequenceEqual(expected), "Independent 6699 vector mismatch.");
            var decoded = TuyaProtocol.Unpack(expected, Key);
            Assert(decoded.Sequence == 7 && decoded.Command == 13 && decoded.Payload.AsSpan().SequenceEqual("example"u8), "Vector decode mismatch.");
            var remote = Enumerable.Range(0, 16).Select(i => (byte)i).ToArray();
            Assert(Convert.ToHexString(TuyaProtocol.DeriveSessionKey(Key, TuyaProtocol.LocalNonce, remote)) ==
                "52723DCB1E501DB933331C8DEBB3C89A", "Independent session derivation mismatch.");
            Assert(Convert.ToHexString(HMACSHA256.HashData(Key, TuyaProtocol.LocalNonce)) ==
                "FB5B26229C20B7ED866706A2FBFAE67E3F404BB6ABE77FF45063A459A42924A4", "Handshake HMAC mismatch.");
            var random1 = TuyaProtocol.Pack(1, 3, [], Key);
            var random2 = TuyaProtocol.Pack(1, 3, [], Key);
            Assert(!random1.AsSpan(18, 12).SequenceEqual(random2.AsSpan(18, 12)), "Packet IV was reused.");
        });
        add("Tuya authenticated framing rejects tampering and unbounded allocations", () =>
        {
            var packet = TuyaProtocol.Pack(1, 13, "fixture"u8, Key);
            foreach (var offset in new[] { 0, 4, 6, 10, 18, 30, packet.Length - 5, packet.Length - 1 })
            {
                var corrupt = packet.ToArray(); corrupt[offset] ^= 1;
                Reject(() => TuyaProtocol.Unpack(corrupt, Key));
            }
            Reject(() => TuyaProtocol.Unpack(packet[..^1], Key));
            Reject(() => TuyaProtocol.Unpack([], Key));
            var header = packet[..18]; BinaryPrimitives.WriteUInt32BigEndian(header.AsSpan(14), 65537);
            Reject(() => TuyaProtocol.PacketLength(header));
            BinaryPrimitives.WriteUInt32BigEndian(header.AsSpan(14), 27);
            Reject(() => TuyaProtocol.PacketLength(header));
            Reject(() => TuyaProtocol.Pack(1, 13, new byte[65509], Key));
        });
        asyncTest("Tuya stream decoder handles fragmented and coalesced frames", async () =>
        {
            var first = TuyaProtocol.Pack(1, 4, "first"u8, Key);
            var second = TuyaProtocol.Pack(2, 13, "second"u8, Key);
            using var stream = new Fragments([.. first, .. second]);
            Assert((await TuyaProtocol.ReadPacketAsync(stream, CancellationToken.None)).SequenceEqual(first), "Fragmented first packet changed.");
            Assert((await TuyaProtocol.ReadPacketAsync(stream, CancellationToken.None)).SequenceEqual(second), "Coalesced second packet lost.");
            using var truncated = new Fragments(first[..^2]);
            try { await TuyaProtocol.ReadPacketAsync(truncated, CancellationToken.None); }
            catch (EndOfStreamException) { return; }
            throw new Exception("Truncated frame accepted.");
        });
        asyncTest("Tuya loopback authenticates session and accepts globally sequenced acknowledgements", async () =>
        {
            await Serve(async lamp => Assert(await new TuyaClient().SendAsync(lamp, TestKey, Red, CancellationToken.None) ==
                TuyaWriteResult.Acknowledged, "Acknowledgement not accepted."), async (stream, token) =>
            {
                var (key, packet) = await Handshake(stream, token);
                Assert(packet.Command == 13 && packet.Sequence == 3, "Control sequence/command mismatch.");
                using var json = JsonDocument.Parse(packet.Payload.AsMemory(15));
                Assert(json.RootElement.GetProperty("data").GetProperty("dps").GetProperty("24").GetString() == "000003e801f4", "Control color mismatch.");
                await FragmentWrite(stream, TuyaProtocol.Pack(901, 13, new byte[4], key), token);
            });
        });
        asyncTest("Tuya late handshake samples current off or menu color and reports exactly what was sent", async () =>
        {
            foreach (var replacement in new[] { Red with { Power = false }, new TuyaColorCommand(true, new(0, 0, 1), .1) })
            {
                TuyaColorCommand? desired = Red; var reads = 0;
                await Serve(async lamp =>
                {
                    var result = await new TuyaClient().SendLatestAsync(lamp, TestKey,
                        () => { Interlocked.Increment(ref reads); return Volatile.Read(ref desired); }, CancellationToken.None);
                    Assert(reads == 1 && result.Result == TuyaWriteResult.Acknowledged && result.SentCommand == replacement,
                        "Late command result did not identify the actual sent color.");
                }, async (stream, token) =>
                {
                    var (key, packet) = await Handshake(stream, token, async () =>
                    {
                        await Task.Delay(600, token); // Cross the producer's 500 ms freshness window.
                        Assert(reads == 0, "Desired color was frozen before handshake completed.");
                        Volatile.Write(ref desired, replacement);
                    });
                    using var json = JsonDocument.Parse(packet.Payload.AsMemory(15));
                    var dps = json.RootElement.GetProperty("data").GetProperty("dps");
                    Assert(dps.GetProperty("20").GetBoolean() == replacement.Power, "Obsolete powered-on command escaped.");
                    if (replacement.Power) Assert(dps.GetProperty("24").GetString() == "00f003e80064", "Pre-handshake RGB escaped.");
                    else Assert(dps.EnumerateObject().Count() == 1, "Latest off command retained color DPS.");
                    Volatile.Write(ref desired, Red); // Further updates must not relabel the command already sent.
                    await stream.WriteAsync(TuyaProtocol.Pack(91, 13, new byte[4], key), token);
                });
            }
        });
        asyncTest("Tuya authorization lost during handshake sends no control packet", async () =>
        {
            TuyaColorCommand? desired = Red;
            await Serve(async lamp =>
            {
                var result = await new TuyaClient().SendLatestAsync(lamp, TestKey, () => Volatile.Read(ref desired), CancellationToken.None);
                Assert(result is { Result: null, SentCommand: null }, "Revoked authorization claimed a write.");
            }, async (stream, token) =>
            {
                _ = await AcceptHandshake(stream, token, async () =>
                { await Task.Delay(600, token); Volatile.Write(ref desired, null); });
                Assert(await stream.ReadAsync(new byte[1], token) == 0, "Revoked authorization sent a control packet.");
            });
        });
        asyncTest("Tuya nonzero acknowledgements fail without exposing secrets", async () =>
        {
            await Serve(lamp => RejectAsync(() => new TuyaClient().SendAsync(lamp, TestKey, Red, CancellationToken.None), "rejected"),
                async (stream, token) =>
                {
                    var (key, _) = await Handshake(stream, token);
                    await stream.WriteAsync(TuyaProtocol.Pack(20, 13, new byte[] { 0, 0, 0, 5 }, key), token);
                });
        });
        asyncTest("Tuya rejects a wrong handshake HMAC before sending a control", async () =>
        {
            await Serve(lamp => RejectAsync(() => new TuyaClient().SendAsync(lamp, TestKey, Red, CancellationToken.None), "handshake"),
                async (stream, token) =>
                {
                    var first = TuyaProtocol.Unpack(await TuyaProtocol.ReadPacketAsync(stream, token), Key);
                    Assert(first.Command == 3, "Missing handshake.");
                    await stream.WriteAsync(TuyaProtocol.Pack(1, 4, new byte[52], Key), token);
                    var one = new byte[1];
                    Assert(await stream.ReadAsync(one, token) == 0, "Control escaped after failed HMAC.");
                });
        });
        asyncTest("Tuya timeout distinguishes unsent handshake from ambiguous fully sent control", async () =>
        {
            await Serve(lamp => RejectAsync(() => new TuyaClient(TimeSpan.FromMilliseconds(500)).SendAsync(lamp, TestKey, Red, CancellationToken.None), "timed out"),
                async (stream, token) =>
                {
                    _ = await TuyaProtocol.ReadPacketAsync(stream, token);
                    Assert(await stream.ReadAsync(new byte[1], token) == 0, "Handshake timeout did not close socket.");
                });
            var elapsed = Stopwatch.StartNew();
            await Serve(async lamp => Assert(await new TuyaClient(TimeSpan.FromSeconds(1)).SendAsync(lamp, TestKey, Red, CancellationToken.None) ==
                TuyaWriteResult.Unacknowledged, "Unacknowledged complete write was retried or reported as confirmed."),
                async (stream, token) =>
                {
                    _ = await Handshake(stream, token);
                    Assert(await stream.ReadAsync(new byte[1], token) == 0, "Timed-out command connection stayed open.");
                });
            Assert(elapsed.Elapsed < TimeSpan.FromSeconds(4), "Command acknowledgement wait was not bounded.");
        });
        asyncTest("Tuya caller cancellation preserves ambiguity after a complete command send", async () =>
        {
            using var cancellation = new CancellationTokenSource();
            await Serve(async lamp =>
            {
                var result = await new TuyaClient().SendAsync(lamp, TestKey, Red, cancellation.Token);
                Assert(cancellation.IsCancellationRequested && result == TuyaWriteResult.Unacknowledged,
                    "Caller cancellation lost a possible write or claimed acknowledgement.");
            }, async (stream, token) =>
            {
                _ = await Handshake(stream, token);
                cancellation.Cancel();
                Assert(await stream.ReadAsync(new byte[1], token) == 0, "Caller cancellation retained the socket.");
            });
            using var beforeSend = new CancellationTokenSource(); beforeSend.Cancel();
            try { await new TuyaClient().SendAsync(Lamp, TestKey, Red, beforeSend.Token); }
            catch (OperationCanceledException) { return; }
            throw new Exception("Cancellation before sending was not propagated.");
        });
        asyncTest("Tuya TCP probe sends no handshake or state-changing bytes", async () =>
        {
            await Serve(async lamp => Assert(await new TuyaClient().ProbeAsync(lamp, CancellationToken.None), "Loopback probe failed."),
                async (stream, token) => Assert(await stream.ReadAsync(new byte[1], token) == 0, "Read-only TCP probe wrote data."));
        });
        asyncTest("Tuya state query is command16 without protocol prefix and filters unrelated DPS", async () =>
        {
            await Serve(async lamp =>
            {
                var state = await new TuyaClient().ReadStateAsync(lamp, TestKey, CancellationToken.None);
                Assert(state.Dps.Count == 4 && state.Dps[20].GetBoolean() && state.Dps[21].GetString() == "white" && !state.Dps.ContainsKey(99),
                    "Read state lost configured fields or included unrelated DPS.");
            }, async (stream, token) =>
            {
                var (key, packet) = await Handshake(stream, token);
                Assert(packet.Command == 16 && packet.Payload.AsSpan().SequenceEqual("{}"u8), "State query wrote data or included version prefix.");
                var response = Encoding.UTF8.GetBytes("{\"data\":{\"dps\":{\"20\":true,\"21\":\"white\",\"22\":500,\"23\":300,\"99\":\"ignored\"}}}");
                await FragmentWrite(stream, TuyaProtocol.Pack(99, 8, [0, 0, 0, 0, .. response], key), token);
            });
        });
        asyncTest("Tuya restoration is mode-specific and never adds white brightness to colour", async () =>
        {
            foreach (var mode in new[] { "colour", "white", "off" })
            {
                var dps = new Dictionary<int, JsonElement> { [20] = JsonSerializer.SerializeToElement(mode != "off"),
                    [21] = JsonSerializer.SerializeToElement(mode == "off" ? "colour" : mode),
                    [24] = JsonSerializer.SerializeToElement("00f003e80064"), [22] = JsonSerializer.SerializeToElement(500),
                    [23] = JsonSerializer.SerializeToElement(300) };
                await Serve(async lamp => Assert(await new TuyaClient().RestoreAsync(lamp, TestKey, new(dps), CancellationToken.None) ==
                    TuyaWriteResult.Acknowledged, "Restore not acknowledged."), async (stream, token) =>
                {
                    var (key, packet) = await Handshake(stream, token);
                    using var json = JsonDocument.Parse(packet.Payload.AsMemory(15));
                    var actual = json.RootElement.GetProperty("data").GetProperty("dps");
                    Assert(actual.EnumerateObject().Count() == (mode == "off" ? 1 : mode == "colour" ? 3 : 4), "Restore sent unrelated DPS.");
                    Assert(actual.TryGetProperty("22", out _) == (mode == "white"), "Restore broke the colour/white separation.");
                    await stream.WriteAsync(TuyaProtocol.Pack(23, 13, new byte[4], key), token);
                });
            }
            var unsupported = new Dictionary<int, JsonElement> { [20] = JsonSerializer.SerializeToElement(true),
                [21] = JsonSerializer.SerializeToElement("scene") };
            // Refusal happens before opening a connection.
            await RejectAsync(() => new TuyaClient().RestoreAsync(Lamp, TestKey, new(unsupported), CancellationToken.None));
        });
    }

    private sealed class Fragments(byte[] bytes) : MemoryStream(bytes)
    {
        public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default) =>
            base.ReadAsync(buffer[..Math.Min(buffer.Length, 3)], cancellationToken);
    }
    private static async Task Serve(Func<TuyaLamp, Task> client, Func<NetworkStream, CancellationToken, Task> server)
    {
        var listener = new TcpListener(IPAddress.Loopback, 0); listener.Start();
        using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(12));
        var serving = Task.Run(async () =>
        {
            using var accepted = await listener.AcceptTcpClientAsync(deadline.Token);
            await server(accepted.GetStream(), deadline.Token);
        });
        try
        {
            await client(Lamp with { Port = ((IPEndPoint)listener.LocalEndpoint).Port });
            await serving;
        }
        finally { deadline.Cancel(); listener.Stop(); }
    }
    private static async Task FragmentWrite(NetworkStream stream, byte[] bytes, CancellationToken token)
    {
        for (var i = 0; i < bytes.Length; i += 7)
            await stream.WriteAsync(bytes.AsMemory(i, Math.Min(7, bytes.Length - i)), token);
    }
    private static async Task<(byte[] Key, TuyaPacket Packet)> Handshake(NetworkStream stream, CancellationToken token,
        Func<Task>? beforeResponse = null)
    {
        var session = await AcceptHandshake(stream, token, beforeResponse);
        return (session, TuyaProtocol.Unpack(await TuyaProtocol.ReadPacketAsync(stream, token), session));
    }
    private static async Task<byte[]> AcceptHandshake(NetworkStream stream, CancellationToken token,
        Func<Task>? beforeResponse = null)
    {
        var start = TuyaProtocol.Unpack(await TuyaProtocol.ReadPacketAsync(stream, token), Key);
        Assert(start.Command == 3 && start.Sequence == 1 && start.Payload.SequenceEqual(TuyaProtocol.LocalNonce), "Session start mismatch.");
        if (beforeResponse != null) await beforeResponse();
        var remote = Enumerable.Range(0, 16).Select(i => (byte)i).ToArray();
        var mac = HMACSHA256.HashData(Key, start.Payload);
        await FragmentWrite(stream, TuyaProtocol.Pack(10, 4, [0, 0, 0, 0, .. remote, .. mac], Key), token);
        var finish = TuyaProtocol.Unpack(await TuyaProtocol.ReadPacketAsync(stream, token), Key);
        Assert(finish.Command == 5 && finish.Sequence == 2 && CryptographicOperations.FixedTimeEquals(finish.Payload, HMACSHA256.HashData(Key, remote)),
            "Session finish HMAC mismatch.");
        return Convert.FromHexString("52723DCB1E501DB933331C8DEBB3C89A");
    }
}
