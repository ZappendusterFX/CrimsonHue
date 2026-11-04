namespace CrimsonHue.Core;

public sealed record OutputSelection(bool Hue = false, bool OpenRgb = false, bool Tuya = false)
{
    public bool Any => Hue || OpenRgb || Tuya;
    public OutputSelection Ready(bool hue, bool openRgb, bool tuya) =>
        new(Hue && hue, OpenRgb && openRgb, Tuya && tuya);
    // Older installations with a pairing keep Hue enabled. New installations
    // start with no selected output and never require a synthetic Hue setup.
    public static bool MigrateHue(bool? saved, bool hasPairing) => saved ?? hasPairing;
}

public sealed record LightingOutput(string Name, Func<CancellationToken, Task> Run);

public static class IndependentOutputs
{
    public static Task RunAsync(IReadOnlyList<LightingOutput> outputs, CancellationToken token,
        Action<string, string>? failed = null)
    {
        return Task.WhenAll(outputs.Select(RunOne));
        async Task RunOne(LightingOutput output)
        {
            try { await output.Run(token).ConfigureAwait(false); }
            catch (OperationCanceledException) when (token.IsCancellationRequested) { }
            catch (Exception)
            {
                // Only the failed output stops. Never publish exception payloads
                // (network and credential libraries can include request data).
                failed?.Invoke(output.Name, "Output stopped after a connection or configuration error. Stop and Start to retry.");
            }
        }
    }
}
