using System.Security.Cryptography;
using System.Text;

namespace CrimsonHue.Core;

// The server owns hardware access. SDK 6 uses opaque controller IDs; older SDKs
// use connection-local indexes. A device-list change invalidates either snapshot.
public sealed record OpenRgbMode(string Name, uint Flags, uint ColorMode, byte[] WireData)
{
    public bool AutomaticSave => (Flags & (1u << 9)) != 0;
    public bool Direct => Name.Equals("Direct", StringComparison.OrdinalIgnoreCase) &&
        (Flags & (1u << 5)) != 0 && ColorMode == 1 && !AutomaticSave;
}

public sealed record OpenRgbZone(string Name, uint LedCount, uint StartIndex, uint[]? Matrix);
public sealed record OpenRgbDevice(uint Id, string Name, int Kind, string Identity,
    IReadOnlyList<OpenRgbMode> Modes, int ActiveMode, IReadOnlyList<OpenRgbZone> Zones,
    IReadOnlyList<string> LedNames, uint[] Colors, bool HasZoneModes = false)
{
    public int DirectMode => Modes.Select((mode, index) => (mode, index)).Where(x => x.mode.Direct)
        .Select(x => x.index).DefaultIfEmpty(-1).First();

    public int[] PreviewLeds()
    {
        // Player/status LEDs on gamepads are not general RGB lighting. For the
        // initial preview only a reported Lightbar zone is selected on gamepads.
        return Zones.Where(z => Kind != 10 || z.Name.Equals("Lightbar", StringComparison.OrdinalIgnoreCase))
            .SelectMany(z => z.Matrix is { } matrix
                ? matrix.Where(i => i != uint.MaxValue).Select(i => checked((int)(z.StartIndex + i)))
                : Enumerable.Range(checked((int)z.StartIndex), checked((int)z.LedCount)))
            .Distinct().Order().ToArray();
    }
}

public static class OpenRgbProtocol
{
    public const uint Version = 6;
    public const uint MinimumVersion = 4;
    public const int MaximumPacketBytes = 8 * 1024 * 1024;
    public const int MaximumDevices = 128;

    public static OpenRgbDevice ParseDevice(uint id, byte[] data, uint protocol = MinimumVersion)
    {
        if (protocol is < MinimumVersion or > Version) throw new IOException("Unsupported OpenRGB protocol.");
        using var stream = new MemoryStream(data, false);
        using var r = new BinaryReader(stream, Encoding.UTF8);
        if (data.Length > MaximumPacketBytes || r.ReadUInt32() != data.Length) throw new IOException("Invalid OpenRGB device packet.");
        string Text()
        {
            var count = r.ReadUInt16();
            var bytes = r.ReadBytes(count);
            if (bytes.Length != count || (count > 0 && bytes[^1] != 0)) throw new IOException("Invalid OpenRGB string.");
            return count == 0 ? "" : Encoding.UTF8.GetString(bytes, 0, count - 1);
        }
        uint[] Colors()
        {
            var count = r.ReadUInt16();
            return Enumerable.Range(0, count).Select(_ => r.ReadUInt32()).ToArray();
        }
        var kind = r.ReadInt32(); var name = Text(); var vendor = Text();
        var description = Text(); var version = Text(); var serial = Text(); var location = Text();
        OpenRgbMode Mode()
        {
            var start = checked((int)stream.Position);
            var modeName = Text(); if (protocol < 6) _ = r.ReadInt32(); var flags = r.ReadUInt32();
            // speed limits, brightness limits, color limits, current speed,
            // current brightness and direction precede the current color mode.
            for (var field = 0; field < 9; field++) _ = r.ReadUInt32();
            var colorMode = r.ReadUInt32(); _ = Colors();
            return new(modeName, flags, colorMode, data[start..checked((int)stream.Position)]);
        }
        var modes = new List<OpenRgbMode>();
        var modeCount = r.ReadUInt16(); var activeMode = r.ReadInt32();
        for (var i = 0; i < modeCount; i++) modes.Add(Mode());
        var zones = new List<OpenRgbZone>();
        var zoneCount = r.ReadUInt16(); uint offset = 0;
        var hasZoneModes = false;
        var layout = new StringBuilder();
        for (var i = 0; i < zoneCount; i++)
        {
            var zoneName = Text(); var type = r.ReadInt32();
            var min = r.ReadUInt32(); var max = r.ReadUInt32(); var count = r.ReadUInt32();
            if (count > ushort.MaxValue || offset + count > ushort.MaxValue) throw new IOException("OpenRGB LED count exceeds preview limit.");
            var matrixBytes = r.ReadUInt16(); uint[]? matrix = null;
            if (matrixBytes != 0)
            {
                var rows = r.ReadUInt32(); var columns = r.ReadUInt32();
                if ((ulong)rows * columns * 4 + 8 != (uint)matrixBytes) throw new IOException("Invalid OpenRGB matrix.");
                matrix = Enumerable.Range(0, checked((int)(rows * columns))).Select(_ => r.ReadUInt32()).ToArray();
                if (matrix.Any(index => index != uint.MaxValue && index >= count)) throw new IOException("OpenRGB matrix index out of range.");
                layout.AppendJoin(',', matrix);
            }
            var segmentCount = r.ReadUInt16();
            for (var segment = 0; segment < segmentCount; segment++)
            {
                layout.Append(Text()).Append(':').Append(r.ReadInt32()).Append(':')
                    .Append(r.ReadUInt32()).Append(':').Append(r.ReadUInt32());
                if (protocol >= 6)
                {
                    var bytes = r.ReadUInt16(); var segmentMap = r.ReadBytes(bytes);
                    if (segmentMap.Length != bytes) throw new IOException("Truncated OpenRGB segment matrix.");
                    _ = r.ReadUInt32();
                }
            }
            if (protocol >= 5) _ = r.ReadUInt32();
            if (protocol >= 6)
            {
                _ = r.ReadInt32(); var zoneModes = r.ReadUInt16(); hasZoneModes |= zoneModes > 0;
                for (var mode = 0; mode < zoneModes; mode++)
                { var value = Mode(); layout.Append(value.Name).Append(':').Append(value.Flags); }
                _ = Text(); // Cosmetic display name.
            }
            layout.Append(zoneName).Append(':').Append(type).Append(':').Append(min).Append(':').Append(max).Append(':').Append(count);
            zones.Add(new(zoneName, count, offset, matrix)); offset += count;
        }
        var ledCount = r.ReadUInt16(); var names = new List<string>();
        for (var i = 0; i < ledCount; i++) { names.Add(Text()); if (protocol < 6) _ = r.ReadUInt32(); }
        var colors = Colors();
        if (protocol >= 5)
        {
            var displayNames = r.ReadUInt16();
            for (var i = 0; i < displayNames; i++) _ = Text();
            _ = r.ReadUInt32();
        }
        if (protocol >= 6)
        {
            _ = Text(); var configurationBytes = r.ReadUInt32();
            if (configurationBytes > stream.Length - stream.Position) throw new IOException("Truncated OpenRGB configuration.");
            stream.Position += configurationBytes; // Opaque device configuration is never used or changed.
        }
        if (stream.Position != stream.Length || colors.Length != ledCount || offset != ledCount ||
            (modes.Count > 0 && (activeMode < 0 || activeMode >= modes.Count))) throw new IOException("Inconsistent OpenRGB device layout.");
        // Raw hardware paths/serials stay private and never appear in diagnostics.
        var identity = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(string.Join('\0',
            kind, name, vendor, description, version, serial, location, layout, string.Join('\0', names),
            string.Join('\0', modes.Select(m => $"{m.Name}:{m.Flags}"))))));
        return new(id, name, kind, identity, modes, activeMode, zones, names, colors, hasZoneModes);
    }

    public static byte[] ModePacket(int index, OpenRgbMode mode)
    {
        using var memory = new MemoryStream(); using var w = new BinaryWriter(memory);
        w.Write(checked((uint)(8 + mode.WireData.Length))); w.Write(index); w.Write(mode.WireData);
        return memory.ToArray();
    }

    public static byte[] ColorsPacket(IReadOnlyList<uint> colors)
    {
        if (colors.Count is < 1 or > ushort.MaxValue) throw new IOException("Invalid OpenRGB color count.");
        using var memory = new MemoryStream(); using var w = new BinaryWriter(memory);
        w.Write(checked((uint)(6 + colors.Count * 4))); w.Write((ushort)colors.Count);
        foreach (var color in colors) w.Write(color);
        return memory.ToArray();
    }

    public static uint Encode(Vec3 rgb)
    {
        if (!rgb.IsFinite) throw new IOException("Invalid OpenRGB output color.");
        uint Byte(double value) => (uint)Math.Round(Math.Clamp(value, 0, 1) * 255);
        return Byte(rgb.X) | Byte(rgb.Y) << 8 | Byte(rgb.Z) << 16;
    }
}
