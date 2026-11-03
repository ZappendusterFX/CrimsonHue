using CrimsonHue.Core;

static class LocalTransitionTests
{
    private static readonly EntertainmentArea Area = new(Fixtures.Id, "Synthetic transition channel", false,
        [new(0, default, [], "Centre")]);
    private static readonly MappingSettings Settings = new(Brightness: 1, Radius: 10, FadeStart: 2,
        Spread: 0, SmoothingMs: 250, AmbientOutput: 0);
    private static readonly TelemetryFrame Red = Frame(new(1, 0, 0));
    private static readonly TelemetryFrame Blue = Frame(new(0, 0, 1));
    private static readonly TelemetryFrame Dark = Red with { Lights = [] };

    private static TelemetryFrame Frame(Vec3 rgb)
    {
        var at = DateTimeOffset.UnixEpoch;
        return new(1, 1, at, at, 0, default,
            new(default, new(1, 0, 0), new(0, 1, 0), new(0, 0, 1)),
            [new(new(0, 0, 1), rgb, 0)], ConfirmedVisibleOnly: true);
    }
    private static Vec3 Map(LightMapper mapper, TelemetryFrame frame, double seconds, MappingSettings? settings = null) =>
        mapper.Map(frame, Area, settings ?? Settings, seconds)[0].Rgb;
    private static Vec3 Target(TelemetryFrame frame) => Map(new(), frame, 0, Settings with { SmoothingMs = 0 });
    private static void Assert(bool condition, string message)
    {
        if (!condition) throw new Exception(message);
    }

    public static void Register(Action<string, Action> add)
    {
        add("Local transition preserves the configured response time without overshoot or a long settling tail", () =>
        {
            var target = Target(Red).X;
            foreach (var milliseconds in new[] { 100.0, 250, 500 })
            {
                var settings = Settings with { SmoothingMs = milliseconds };
                var mapper = new LightMapper();
                var atResponseTime = Map(mapper, Red, milliseconds / 1000, settings).X / target;
                Assert(Math.Abs(atResponseTime - (1 - Math.Exp(-1))) < 1e-10,
                    "The selected transition no longer reaches 63.2% at its configured time");
                var previous = atResponseTime;
                for (var i = 0; i < 60; i++)
                {
                    var next = Map(mapper, Red, milliseconds / 20000, settings).X / target;
                    Assert(next >= previous - 1e-12 && next <= 1 + 1e-12,
                        "A steady local target rang or overshot");
                    previous = next;
                }
                Assert(previous > .998, "A steady target retained a long settling tail beyond four response times");
            }
        });

        add("Local transition rejects repeated short visibility gaps without changing their mean brightness", () =>
        {
            const int rate = 600;
            var target = Target(Red).X;
            foreach (var frequency in new[] { 5, 10 })
            foreach (var duty in new[] { .25, .5 })
            {
                var mapper = new LightMapper();
                var period = rate / frequency;
                var onTicks = (int)(period * duty);
                var old = 0.0;
                var alpha = 1 - Math.Exp(-1.0 / rate / .25);
                var values = new List<double>();
                var oldValues = new List<double>();
                for (var tick = 0; tick < rate * 6; tick++)
                {
                    var on = tick % period < onTicks;
                    var output = Map(mapper, on ? Red : Dark, 1.0 / rate).X / target;
                    old += ((on ? 1 : 0) - old) * alpha;
                    if (tick >= rate * 4) { values.Add(output); oldValues.Add(old); }
                }
                var ripple = values.Max() - values.Min();
                var oldRipple = oldValues.Max() - oldValues.Min();
                Assert(ripple < oldRipple * (frequency == 5 ? .6 : .35),
                    $"The {frequency} Hz / {duty:P0} gap pattern was not substantially quieter than the previous transition");
                Assert(Math.Abs(values.Average() - duty) < .002 &&
                    Math.Abs(values.Average() - oldValues.Average()) < .002,
                    "Suppressing visibility chatter introduced a brightness hold or bias");
            }
        });

        add("Local transition retains brief light pulses with bounded peak delay and complete release", () =>
        {
            const double step = .001;
            var target = Target(Red).X;
            foreach (var (milliseconds, minimumRetention) in new[] { (50, .85), (100, .92), (200, 1.0) })
            {
                var mapper = new LightMapper();
                var peak = 0.0;
                var peakAt = 0.0;
                var output = 0.0;
                for (var tick = 0; tick < 2000; tick++)
                {
                    output = Map(mapper, tick < milliseconds ? Red : Dark, step).X / target;
                    Assert(output >= 0 && output <= 1, "A light pulse overshot or undershot");
                    if (output > peak) { peak = output; peakAt = (tick + 1) * step; }
                }
                var duration = milliseconds / 1000.0;
                var oldPeak = 1 - Math.Exp(-duration / .25);
                Assert(peak >= oldPeak * minimumRetention && peak <= oldPeak * 1.1,
                    $"The {milliseconds} ms light pulse was excessively suppressed or amplified");
                Assert(peakAt >= duration && peakAt <= duration + .1,
                    $"The {milliseconds} ms pulse peak was delayed by more than 100 ms");
                Assert(output < .001, "A completed light pulse left persistent local output");
            }
        });

        add("Local transition blends a color vector without hue bias, ringing or spurious components", () =>
        {
            var mapper = new LightMapper();
            var red = Map(mapper, Red, 0, Settings with { SmoothingMs = 0 });
            var blue = Target(Blue);
            var previous = red;
            for (var tick = 0; tick < 150; tick++)
            {
                var output = Map(mapper, Blue, .01);
                Assert(output.IsFinite && output.Y == 0 && output.X >= 0 && output.Z >= 0 &&
                    output.X <= previous.X + 1e-12 && output.Z >= previous.Z - 1e-12 && output.Z <= blue.Z + 1e-12,
                    "A red/blue handoff changed hue components or rang beyond its targets");
                Assert(Math.Abs(output.X / red.X + output.Z / blue.Z - 1) < 1e-11,
                    "RGB components followed different transition coefficients");
                previous = output;
            }
            Assert(previous.X < red.X * .0001 && previous.Z > blue.Z * .9999,
                "The color handoff did not converge to the new target");
        });

        add("Local transition handles immediate mode, reset and tiny positive durations without old state or NaN", () =>
        {
            var mapper = new LightMapper();
            Map(mapper, Red, .1);
            var blue = Map(mapper, Blue, 0, Settings with { SmoothingMs = 0 });
            Assert((blue - Target(Blue)).Length < 1e-12, "Zero transition was not immediate");
            Assert((Map(mapper, Red, 0) - blue).Length < 1e-12 &&
                (Map(mapper, Red, -.1) - blue).Length < 1e-12,
                "Re-enabling the transition restored stale state or advanced with no elapsed time");
            Map(mapper, Red, .1);
            mapper.Reset();
            Assert(Map(mapper, Dark, .1) == default, "Reset retained one of the transition states");
            foreach (var milliseconds in new[] { double.Epsilon, 1e-100 })
            {
                mapper.Reset();
                var settings = Settings with { SmoothingMs = milliseconds };
                Assert(Map(mapper, Red, 0, settings) == default,
                    "A tiny positive duration advanced at zero elapsed time");
                var output = Map(mapper, Red, .01, settings);
                Assert(output.IsFinite && (output - Target(Red)).Length < 1e-12,
                    "A tiny positive transition produced NaN or failed to reach its target");
                Assert(Map(mapper, Dark, .01, settings) == default,
                    "A tiny positive transition did not release fully");
            }
        });

        add("Local transition integrates identical held targets consistently at regular and irregular update intervals", () =>
        {
            var settings = Settings with { SmoothingMs = 173 };
            var phases = new[] { (Red, .4), (Dark, .2), (Blue, .4) };
            List<Vec3> Run(double[]? intervals)
            {
                var mapper = new LightMapper();
                var result = new List<Vec3>();
                var index = 0;
                foreach (var (frame, duration) in phases)
                {
                    var elapsed = 0.0;
                    var output = default(Vec3);
                    while (elapsed < duration - 1e-12)
                    {
                        var step = Math.Min(duration - elapsed, intervals is null ? duration : intervals[index++ % intervals.Length]);
                        output = Map(mapper, frame, step, settings);
                        elapsed += step;
                    }
                    result.Add(output);
                }
                return result;
            }
            var reference = Run(null);
            foreach (var intervals in new[] { new[] { 1.0 / 15 }, new[] { 1.0 / 30 }, new[] { 1.0 / 60 },
                new[] { 1.0 / 120 }, new[] { .001, .047, .013, .089, .004 } })
                Assert(reference.Zip(Run(intervals)).All(p => (p.First - p.Second).Length < 1e-10),
                    "Local transition speed or color changed with update scheduling");
        });
    }
}
