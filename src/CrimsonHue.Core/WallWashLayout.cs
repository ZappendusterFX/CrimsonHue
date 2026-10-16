namespace CrimsonHue.Core;

// Local room calibration, deliberately separate from shareable mixer presets.
// Angles describe the visible wall from the seated viewer, not emitter aim.
public sealed record WallWashSegment(int Index, double YawOffset = 0, double Elevation = 0, double VerticalSpan = 0,
    bool NeedsReview = false);
public sealed record WallWasher(string AreaId, string ServiceId, bool Enabled, double Yaw, double HorizontalSpan,
    IReadOnlyList<WallWashSegment> Segments);
public readonly record struct WallWashSample(Vec3 Direction, double Weight);
public sealed record WallWashLayout(IReadOnlyList<WallWasher> Washers)
{
    public static WallWashLayout Empty { get; } = new([]);

    public void Validate()
    {
        if (Washers == null || Washers.Count > 160) throw new CrimsonHueException("Too many wall washer calibrations.");
        var keys = new HashSet<(string, string)>();
        foreach (var washer in Washers)
        {
            if (washer == null || !Guid.TryParseExact(washer.AreaId, "D", out _) ||
                string.IsNullOrWhiteSpace(washer.ServiceId) || washer.ServiceId.Length > 128 ||
                !keys.Add((washer.AreaId, washer.ServiceId)) || !Range(washer.Yaw, -180, 180) ||
                !Range(washer.HorizontalSpan, 0, 180) || washer.Segments == null || washer.Segments.Count is < 1 or > 160)
                throw new CrimsonHueException("Invalid wall washer calibration.");
            var indices = new HashSet<int>();
            foreach (var segment in washer.Segments)
                if (segment == null || segment.Index is < 0 or > 255 || !indices.Add(segment.Index) ||
                    !Range(segment.YawOffset, -180, 180) || !Range(segment.Elevation, -89, 89) ||
                    !Range(segment.VerticalSpan, 0, 90))
                    throw new CrimsonHueException("Invalid wall washer segment calibration.");
        }
    }

    private static bool Range(double value, double min, double max) => double.IsFinite(value) && value >= min && value <= max;

    public WallWasher GetOrCreate(EntertainmentArea area, string serviceId)
    {
        var channels = area.Channels.Where(c => c.Members.Any(m => m.ServiceId == serviceId)).ToArray();
        if (channels.Length == 0) throw new CrimsonHueException("Choose a light in the selected area.");
        var saved = Washers.FirstOrDefault(w => w.AreaId == area.Id && w.ServiceId == serviceId);
        var position = channels.Select(c => c.Position).Aggregate(default(Vec3), (sum, p) => sum + p) / channels.Length;
        var segments = channels.SelectMany(c => c.Members).Where(m => m.ServiceId == serviceId).Select(m => m.SegmentIndex).Distinct().Order()
            .Select(index => saved?.Segments.FirstOrDefault(s => s.Index == index) ?? new WallWashSegment(index,
                Elevation: Math.Clamp(Elevation(channels.First(c => c.Members.Contains(new ChannelMember(serviceId, index))).Position), -89, 89),
                NeedsReview: saved != null)).ToArray();
        // Seed from imported coordinates only. Outward placement is an explicit user choice.
        return new(area.Id, serviceId, saved?.Enabled ?? false, saved?.Yaw ?? Yaw(position), saved?.HorizontalSpan ?? 60, segments);
    }

    public WallWashLayout Set(WallWasher washer)
    {
        var next = new WallWashLayout(Washers.Where(w => w.AreaId != washer.AreaId || w.ServiceId != washer.ServiceId).Append(washer).ToArray());
        next.Validate(); return next;
    }

    public WallWashLayout Remove(string areaId, string serviceId) =>
        new(Washers.Where(w => w.AreaId != areaId || w.ServiceId != serviceId).ToArray());

    public IReadOnlyList<WallWashSample>? Samples(EntertainmentArea area, EntertainmentChannel channel,
        Vec3? source = null, double sourceRadius = 60)
    {
        if (Washers.Count == 0) return null;
        var matches = channel.Members.Select(m => (Member: m, Washer: Washers.FirstOrDefault(w =>
            w.Enabled && w.AreaId == area.Id && w.ServiceId == m.ServiceId))).ToArray();
        if (!matches.Any(m => m.Washer != null)) return null;
        var result = new List<WallWashSample>();
        foreach (var (member, washer) in matches)
        {
            if (washer == null) { result.Add(new(channel.Position.Unit, 1.0 / matches.Length)); continue; }
            var segment = washer.Segments.FirstOrDefault(s => s.Index == member.SegmentIndex);
            // A changed Hue layout must not silently turn an enabled wall region
            // into a point light. New segments await explicit placement review;
            // existing segments and the independent Ambient layer remain usable.
            if (segment == null || segment.NeedsReview) continue;
            // Equal-area quadrature in yaw / sin(elevation). Integrate only the
            // source cap's bounding box so even a one-degree source cannot fall
            // through the grid of a wide wall. Weights retain the full wall area.
            var span = washer.HorizontalSpan < 1e-6 ? 0 : washer.HorizontalSpan;
            var heightSpan = segment.VerticalSpan < 1e-6 ? 0 : segment.VerticalSpan;
            var horizontal = span == 0 ? 1 : 15;
            var vertical = heightSpan == 0 ? 1 : 5;
            var fullLow = Math.Clamp(segment.Elevation - heightSpan / 2, -89, 89);
            var fullHigh = Math.Clamp(segment.Elevation + heightSpan / 2, -89, 89);
            var center = washer.Yaw + segment.YawOffset;
            var yawLow = center - span / 2;
            var yawHigh = center + span / 2;
            var low = fullLow; var high = fullHigh;
            var yawIntervals = new List<(double Low, double High)>();
            if (source is { } direction)
            {
                var sourceElevation = Elevation(direction);
                low = Math.Max(low, sourceElevation - sourceRadius); high = Math.Min(high, sourceElevation + sourceRadius);
                if (low > high) continue;
                var yawRadius = Math.Abs(sourceElevation) + sourceRadius >= 90 ? 180 :
                    Math.Asin(Math.Clamp(Math.Sin(sourceRadius * Math.PI / 180) / Math.Cos(sourceElevation * Math.PI / 180), -1, 1)) * 180 / Math.PI;
                var sourceYaw = center + Math.IEEERemainder(Yaw(direction) - center, 360);
                if (yawRadius == 180) yawIntervals.Add((yawLow, yawHigh));
                else foreach (var turn in new[] { -360, 0, 360 })
                {
                    var a = Math.Max(yawLow, sourceYaw + turn - yawRadius);
                    var b = Math.Min(yawHigh, sourceYaw + turn + yawRadius);
                    if (a <= b && (a < b || span == 0)) yawIntervals.Add((a, b));
                }
            }
            else yawIntervals.Add((yawLow, yawHigh));
            var sinLow = Math.Sin(low * Math.PI / 180); var sinHigh = Math.Sin(high * Math.PI / 180);
            var verticalFraction = heightSpan == 0 ? 1 :
                (sinHigh - sinLow) / (Math.Sin(fullHigh * Math.PI / 180) - Math.Sin(fullLow * Math.PI / 180));
            foreach (var interval in yawIntervals)
            {
                var horizontalFraction = span == 0 ? 1 : (interval.High - interval.Low) / span;
                for (var x = 0; x < horizontal; x++)
                    for (var z = 0; z < vertical; z++)
                    {
                        var yaw = interval.Low + (interval.High - interval.Low) * (x + .5) / horizontal;
                        var elevation = Math.Asin(sinLow + (sinHigh - sinLow) * (z + .5) / vertical) * 180 / Math.PI;
                        result.Add(new(Direction(yaw, elevation), horizontalFraction * verticalFraction / (matches.Length * horizontal * vertical)));
                    }
            }
        }
        return result;
    }

    public static double Yaw(Vec3 position) => Math.Atan2(position.X, position.Y) * 180 / Math.PI;
    public static double Elevation(Vec3 position) => Math.Atan2(position.Z, Math.Sqrt(position.X * position.X + position.Y * position.Y)) * 180 / Math.PI;
    public static Vec3 Direction(double yaw, double elevation)
    {
        yaw *= Math.PI / 180; elevation *= Math.PI / 180;
        static double Axis(double value) => Math.Abs(value) < 1e-12 ? 0 : value;
        return new(Axis(Math.Sin(yaw) * Math.Cos(elevation)), Axis(Math.Cos(yaw) * Math.Cos(elevation)), Axis(Math.Sin(elevation)));
    }
}
