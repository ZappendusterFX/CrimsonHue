using System.Buffers.Binary;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace CrimsonHue.Core;

public sealed record TuyaColorCommand(bool Power, Vec3 Rgb, double Brightness);
public enum TuyaWriteResult { Acknowledged, Unacknowledged }
public sealed record TuyaDeviceState(IReadOnlyDictionary<int, JsonElement> Dps);
public sealed record TuyaPacket(uint Sequence, uint Command, byte[] Payload);

// Wire behavior follows the existing LEDVANCE backend and TinyTuya's 6699 codec:
// https://github.com/jasonacox/tinytuya/blob/master/tinytuya/core/message_helper.py
// No device credentials or raw response text are included in protocol errors.
public static class TuyaProtocol
{
    public const int HeaderBytes = 18, MaximumBodyBytes = 65536;
    public static byte[] LocalNonce => "0123456789abcdef"u8.ToArray();
    private static readonly byte[] VersionHeader = [0x33, 0x2e, 0x35, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0];

    public static byte[] KeyBytes(string localKey)
    {
        if (localKey is null || localKey.Length > 32) throw new CrimsonHueException("Tuya local key must contain 16, 24 or 32 UTF-8 bytes.");
        var key = Encoding.UTF8.GetBytes(localKey);
        if (key.Length is 16 or 24 or 32) return key;
        CryptographicOperations.ZeroMemory(key);
        throw new CrimsonHueException("Tuya local key must contain 16, 24 or 32 UTF-8 bytes.");
    }

    public static string Hsv(Vec3 rgb, double brightness)
    {
        if (!rgb.IsFinite || rgb.X is < 0 or > 1 || rgb.Y is < 0 or > 1 || rgb.Z is < 0 or > 1 ||
            !double.IsFinite(brightness) || brightness is < 0 or > 1)
            throw new CrimsonHueException("Tuya color and brightness must be finite values between zero and one.");
        var max = Math.Max(rgb.X, Math.Max(rgb.Y, rgb.Z));
        var min = Math.Min(rgb.X, Math.Min(rgb.Y, rgb.Z));
        var delta = max - min;
        var hue = delta <= .0001 ? 0 : max == rgb.X ? 60 * (((rgb.Y - rgb.Z) / delta) % 6) :
            max == rgb.Y ? 60 * ((rgb.Z - rgb.X) / delta + 2) : 60 * ((rgb.X - rgb.Y) / delta + 4);
        if (hue < 0) hue += 360;
        var saturation = max <= .0001 ? 0 : delta / max;
        static int Round(double n) => (int)Math.Round(n, MidpointRounding.AwayFromZero);
        return Round(hue).ToString("x4", CultureInfo.InvariantCulture) +
            Round(saturation * 1000).ToString("x4", CultureInfo.InvariantCulture) +
            (Math.Clamp(Round(brightness * 100), 1, 100) * 10).ToString("x4", CultureInfo.InvariantCulture);
    }

    public static IReadOnlyDictionary<int, JsonElement> BuildDps(TuyaLamp lamp, TuyaColorCommand command)
    {
        lamp.Validate();
        var hsv = Hsv(command.Rgb, command.Brightness); // Validate even an off command before any I/O.
        var dps = new Dictionary<int, JsonElement> { [lamp.PowerDps] = JsonSerializer.SerializeToElement(command.Power) };
        if (command.Power)
        {
            dps[lamp.ModeDps] = JsonSerializer.SerializeToElement("colour");
            dps[lamp.ColorDps] = JsonSerializer.SerializeToElement(hsv);
        }
        return dps;
    }

    public static byte[] ControlPayload(IReadOnlyDictionary<int, JsonElement> dps, long unixSeconds)
    {
        var json = JsonSerializer.SerializeToUtf8Bytes(new { protocol = 5,
            t = unixSeconds.ToString(CultureInfo.InvariantCulture), data = new { dps } });
        return [.. VersionHeader, .. json];
    }

    public static byte[] Pack(uint sequence, uint command, ReadOnlySpan<byte> payload, ReadOnlySpan<byte> key,
        ReadOnlySpan<byte> iv = default)
    {
        if (key.Length is not (16 or 24 or 32) || payload.Length > MaximumBodyBytes - 28)
            throw new InvalidDataException("Invalid Tuya packet configuration.");
        var nonce = iv.IsEmpty ? RandomNumberGenerator.GetBytes(12) : iv.ToArray();
        if (nonce.Length != 12) throw new InvalidDataException("Invalid Tuya nonce length.");
        var packet = new byte[HeaderBytes + 28 + payload.Length + 4];
        BinaryPrimitives.WriteUInt32BigEndian(packet, 0x6699);
        BinaryPrimitives.WriteUInt32BigEndian(packet.AsSpan(6), sequence);
        BinaryPrimitives.WriteUInt32BigEndian(packet.AsSpan(10), command);
        BinaryPrimitives.WriteUInt32BigEndian(packet.AsSpan(14), (uint)(28 + payload.Length));
        nonce.CopyTo(packet, HeaderBytes);
        using var cipher = new AesGcm(key, 16);
        cipher.Encrypt(nonce, payload, packet.AsSpan(HeaderBytes + 12, payload.Length),
            packet.AsSpan(HeaderBytes + 12 + payload.Length, 16), packet.AsSpan(4, HeaderBytes - 4));
        BinaryPrimitives.WriteUInt32BigEndian(packet.AsSpan(packet.Length - 4), 0x9966);
        return packet;
    }

    public static int PacketLength(ReadOnlySpan<byte> header)
    {
        if (header.Length < HeaderBytes || BinaryPrimitives.ReadUInt32BigEndian(header) != 0x6699 ||
            BinaryPrimitives.ReadUInt16BigEndian(header[4..]) != 0)
            throw new InvalidDataException("Invalid Tuya frame header.");
        var body = BinaryPrimitives.ReadUInt32BigEndian(header[14..]);
        if (body is < 28 or > MaximumBodyBytes) throw new InvalidDataException("Invalid Tuya frame length.");
        return HeaderBytes + (int)body + 4;
    }

    public static TuyaPacket Unpack(ReadOnlySpan<byte> packet, ReadOnlySpan<byte> key)
    {
        if (packet.Length != PacketLength(packet) || BinaryPrimitives.ReadUInt32BigEndian(packet[^4..]) != 0x9966)
            throw new InvalidDataException("Invalid Tuya frame boundary.");
        var size = packet.Length - HeaderBytes - 32;
        var payload = new byte[size];
        using var cipher = new AesGcm(key, 16);
        cipher.Decrypt(packet.Slice(HeaderBytes, 12), packet.Slice(HeaderBytes + 12, size),
            packet.Slice(HeaderBytes + 12 + size, 16), payload, packet.Slice(4, HeaderBytes - 4));
        return new(BinaryPrimitives.ReadUInt32BigEndian(packet[6..]), BinaryPrimitives.ReadUInt32BigEndian(packet[10..]), payload);
    }

    public static byte[] DeriveSessionKey(ReadOnlySpan<byte> key, ReadOnlySpan<byte> localNonce, ReadOnlySpan<byte> remoteNonce)
    {
        if (localNonce.Length != 16 || remoteNonce.Length != 16) throw new InvalidDataException("Invalid Tuya handshake nonce.");
        Span<byte> material = stackalloc byte[16];
        for (var i = 0; i < 16; ++i) material[i] = (byte)(localNonce[i] ^ remoteNonce[i]);
        var result = new byte[16];
        Span<byte> tag = stackalloc byte[16];
        using var cipher = new AesGcm(key, 16);
        cipher.Encrypt(localNonce[..12], material, result, tag);
        CryptographicOperations.ZeroMemory(material);
        return result;
    }

    public static async Task<byte[]> ReadPacketAsync(Stream stream, CancellationToken token)
    {
        var header = new byte[HeaderBytes];
        await stream.ReadExactlyAsync(header, token);
        var packet = new byte[PacketLength(header)];
        header.CopyTo(packet, 0);
        await stream.ReadExactlyAsync(packet.AsMemory(HeaderBytes), token);
        return packet;
    }

    internal static byte[] ResponsePayload(TuyaPacket packet, bool handshake = false)
    {
        var payload = packet.Payload;
        // Handshake response is binary: its exact size distinguishes optional retcode.
        if (handshake && payload.Length == 48) return payload;
        var noRetcode = !handshake && (payload.Length == 0 || payload[0] == (byte)'{' || payload.AsSpan().StartsWith(VersionHeader));
        if (!noRetcode)
        {
            if (payload.Length < 4 || (handshake && payload.Length != 52)) throw new InvalidDataException("Invalid Tuya response.");
            if (BinaryPrimitives.ReadUInt32BigEndian(payload) != 0) throw new CrimsonHueException("Tuya device rejected the command.");
            payload = payload[4..];
        }
        if (!handshake && payload.AsSpan().StartsWith(VersionHeader)) payload = payload[VersionHeader.Length..];
        return payload;
    }

    internal static TuyaDeviceState ParseState(TuyaLamp lamp, byte[] payload)
    {
        using var json = JsonDocument.Parse(payload, new JsonDocumentOptions { MaxDepth = 16 });
        var root = json.RootElement;
        if (root.TryGetProperty("data", out var data)) root = data;
        if (root.ValueKind != JsonValueKind.Object || !root.TryGetProperty("dps", out var dps) || dps.ValueKind != JsonValueKind.Object)
            throw new InvalidDataException("Tuya state did not contain data points.");
        var values = new Dictionary<int, JsonElement>();
        foreach (var id in new[] { lamp.PowerDps, lamp.ModeDps, lamp.ColorDps, lamp.WhiteBrightnessDps, lamp.ColorTemperatureDps, lamp.SceneDps })
            if (dps.TryGetProperty(id.ToString(CultureInfo.InvariantCulture), out var value)) values.Add(id, value.Clone());
        if (!values.TryGetValue(lamp.PowerDps, out var power) || power.ValueKind is not (JsonValueKind.True or JsonValueKind.False))
            throw new InvalidDataException("Tuya state did not contain power status.");
        return new(values);
    }

    internal static IReadOnlyDictionary<int, JsonElement> RestoreDps(TuyaLamp lamp, TuyaDeviceState state)
    {
        var original = state.Dps;
        if (!original.TryGetValue(lamp.PowerDps, out var power) || power.ValueKind is not (JsonValueKind.True or JsonValueKind.False))
            throw new CrimsonHueException("Tuya prior state cannot be restored safely.");
        var result = new Dictionary<int, JsonElement> { [lamp.PowerDps] = power };
        if (!power.GetBoolean()) return result;
        if (!original.TryGetValue(lamp.ModeDps, out var mode) || mode.ValueKind != JsonValueKind.String)
            throw new CrimsonHueException("Tuya prior state cannot be restored safely.");
        result[lamp.ModeDps] = mode;
        var ids = mode.GetString() switch { "colour" => new[] { lamp.ColorDps },
            "white" => new[] { lamp.WhiteBrightnessDps, lamp.ColorTemperatureDps }, _ => [] };
        if (ids.Length == 0) throw new CrimsonHueException("Tuya prior mode cannot be restored safely.");
        foreach (var id in ids)
        {
            if (!original.TryGetValue(id, out var value)) throw new CrimsonHueException("Tuya prior state is incomplete.");
            if (id == lamp.ColorDps)
            {
                var color = value.ValueKind == JsonValueKind.String ? value.GetString() : null;
                if (color is null || color.Length != 12 || !color.All(Uri.IsHexDigit) ||
                    Convert.ToInt32(color[..4], 16) > 360 || Convert.ToInt32(color[4..8], 16) > 1000 ||
                    Convert.ToInt32(color[8..], 16) > 1000) throw new CrimsonHueException("Tuya prior color is invalid.");
            }
            else if (value.ValueKind != JsonValueKind.Number || !value.TryGetInt32(out var n) || n is < 0 or > 1000)
                throw new CrimsonHueException("Tuya prior white setting is invalid.");
            result[id] = value;
        }
        return result;
    }
}
