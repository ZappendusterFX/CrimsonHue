using System.Diagnostics;
using System.Text.Json.Serialization;

namespace CrimsonHue.Core;

public sealed record MenuLightingSettings(bool Enabled = true, Vec3? Color = null,
    double Brightness = .2, double ResumeBlendMs = 500)
{
    [JsonIgnore] public Vec3 EffectiveColor => Color ?? new Vec3(1, .65, .35);
    public void Validate()
    {
        var c = EffectiveColor;
        if (!c.IsFinite || c.X is < 0 or > 1 || c.Y is < 0 or > 1 || c.Z is < 0 or > 1 ||
            !double.IsFinite(Brightness) || Brightness is < 0 or > 1 ||
            !double.IsFinite(ResumeBlendMs) || ResumeBlendMs is < 0 or > 1000)
            throw new CrimsonHueException("Invalid menu lighting settings.");
    }
}

public sealed record MenuLightingDecision(TelemetryFrame? Frame, AmbientFrame? Ambient,
    bool UseMenuLight, bool ResetMapper, double GameplayBlend = 1)
{
    public bool CanOutput => UseMenuLight || Frame != null;
}

// One policy per preview/output mapper. Capture barriers are local to this consumer;
// resetting a shared telemetry receiver would race the other independent outputs.
public sealed class MenuLightingPolicy
{
    private string? session;
    private long epoch = -1;
    private bool observedKnownState;
    private bool wasPaused;
    private bool resuming;
    private long resumeStarted;
    private long? blendStarted;
    private DateTimeOffset? captureBarrier;

    public MenuLightingDecision Evaluate(ActivityFrame? activity, TelemetryFrame? frame, AmbientFrame? ambient,
        MenuLightingSettings menu, DateTimeOffset? utcNow = null, long? monotonicTimestamp = null)
    {
        menu.Validate();
        var now = monotonicTimestamp ?? Stopwatch.GetTimestamp();
        var utc = utcNow ?? DateTimeOffset.UtcNow;
        var paused = menu.Enabled && activity is { State: "paused", AcquisitionPaused: true };
        var running = activity is { State: "running", AcquisitionPaused: false };
        var firstKnown = !observedKnownState && (running || paused);
        if (running || paused) observedKnownState = true;
        var changed = activity != null && session != null && (session != activity.SessionId || epoch != activity.Epoch);
        var reset = false;
        if (activity != null) { session = activity.SessionId; epoch = activity.Epoch; }
        if (paused)
        {
            reset = !wasPaused || changed;
            wasPaused = true; resuming = false; blendStarted = null;
            if (reset) captureBarrier = activity!.TimestampUtc;
            return new(null, null, true, reset, 0);
        }
        if (wasPaused || changed || firstKnown)
        {
            reset = true;
            captureBarrier = running ? activity!.TimestampUtc : utc;
            resuming = wasPaused && running && menu.Enabled;
            resumeStarted = now; blendStarted = null; wasPaused = false;
        }
        if (!running && resuming)
        {
            // Lost authorization must clear immediately; it never extends the fixed hold.
            resuming = false; blendStarted = null; reset = true; captureBarrier = utc;
        }
        if (captureBarrier is { } barrier)
        {
            if (frame != null && frame.LightCapturedAt < barrier) frame = null;
            if (ambient != null && ambient.CapturedAt < barrier) ambient = null;
        }
        if (resuming)
        {
            if (frame == null)
            {
                blendStarted = null;
                if (Stopwatch.GetElapsedTime(resumeStarted, now).TotalMilliseconds < 1000)
                    return new(null, null, true, reset, 0);
                resuming = false;
                return new(null, null, false, true);
            }
            blendStarted ??= now;
            var blend = menu.ResumeBlendMs == 0 ? 1 : Math.Clamp(
                Stopwatch.GetElapsedTime(blendStarted.Value, now).TotalMilliseconds / menu.ResumeBlendMs, 0, 1);
            if (blend >= 1) resuming = false;
            return new(frame, ambient, false, reset, blend);
        }
        return new(frame, ambient, false, reset);
    }

    // Output RGB is already display RGB; never gamma/amplify this configured color.
    public static IReadOnlyList<ChannelColor> Apply(MenuLightingDecision decision, IReadOnlyList<ChannelColor> mapped,
        EntertainmentArea area, MappingSettings effectiveMapping, MenuLightingSettings menu)
    {
        menu.Validate(); LightMapper.Validate(effectiveMapping);
        if (!decision.CanOutput) return area.Channels.Select(c => new ChannelColor(c.Id, default)).ToArray();
        if (!decision.UseMenuLight && decision.GameplayBlend >= 1) return mapped;
        var byId = mapped.ToDictionary(c => c.Id, c => c.Rgb);
        return area.Channels.Select(channel =>
        {
            var fixedColor = menu.EffectiveColor * (menu.Brightness * effectiveMapping.Brightness * Math.Clamp(channel.Brightness, 0, 1));
            var color = decision.UseMenuLight ? fixedColor : fixedColor * (1 - decision.GameplayBlend) +
                byId.GetValueOrDefault(channel.Id) * decision.GameplayBlend;
            return new ChannelColor(channel.Id, color);
        }).ToArray();
    }
}
