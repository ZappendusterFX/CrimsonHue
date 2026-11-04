using System.Text.Json;
using CrimsonHue.Core;

internal static class IndependentOutputTests
{
    internal static void Register(Action<string, Action> add, Action<string, Func<Task>> asyncTest)
    {
        add("outputs: all seven selections work without implicit Hue", Selections);
        asyncTest("outputs: one failure does not cancel another backend", FailureIsolation);
        asyncTest("outputs: completed backend does not stop others; Stop awaits cleanup", CompletionAndStop);
        add("outputs: old settings preserve pairing opt-in and isolated optional config", Migration);
    }
    private static void Check(bool value, string message)
    { if (!value) throw new Exception(message); }
    private static void Selections()
    {
        for (var mask = 1; mask < 8; mask++)
        {
            var selection = new OutputSelection((mask & 1) != 0, (mask & 2) != 0, (mask & 4) != 0);
            Check(selection.Ready(true, true, true) == selection, "A selected backend depends on another backend.");
            var noHue = selection.Ready(false, true, true);
            Check(!noHue.Hue && noHue.OpenRgb == selection.OpenRgb && noHue.Tuya == selection.Tuya,
                "An absent Hue setup blocked OpenRGB or Tuya.");
        }
        Check(!new OutputSelection().Any && !new OutputSelection(true, true, true).Ready(false, false, false).Any,
            "Start was possible without a configured selected output.");
    }
    private static async Task FailureIsolation()
    {
        using var stop = new CancellationTokenSource();
        var running = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var failed = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);
        var session = IndependentOutputs.RunAsync([
            new("Hue", _ => throw new IOException("secret request fixture must never leave the failing output")),
            new("OpenRGB", async ct => { running.TrySetResult(); await Task.Delay(Timeout.Infinite, ct); })],
            stop.Token, (name, message) => failed.TrySetResult(name + ":" + message));
        await running.Task.WaitAsync(TimeSpan.FromSeconds(3));
        var message = await failed.Task.WaitAsync(TimeSpan.FromSeconds(3));
        Check(!session.IsCompleted && !stop.IsCancellationRequested, "Hue failure stopped the independent OpenRGB session.");
        Check(!message.Contains("secret", StringComparison.Ordinal), "Failure report leaked the original exception.");
        stop.Cancel(); await session.WaitAsync(TimeSpan.FromSeconds(3));
    }
    private static async Task CompletionAndStop()
    {
        using var stop = new CancellationTokenSource();
        var started = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var cleaning = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var finish = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var session = IndependentOutputs.RunAsync([
            new("Hue", _ => Task.CompletedTask),
            new("Tuya", async ct =>
            {
                started.TrySetResult();
                try { await Task.Delay(Timeout.Infinite, ct); }
                finally { cleaning.TrySetResult(); await finish.Task; }
            })], stop.Token);
        await started.Task.WaitAsync(TimeSpan.FromSeconds(3));
        Check(!session.IsCompleted, "Completing Hue cancelled Tuya.");
        stop.Cancel(); await cleaning.Task.WaitAsync(TimeSpan.FromSeconds(3));
        Check(!session.IsCompleted, "Stop returned before cleanup finished.");
        finish.TrySetResult(); await session.WaitAsync(TimeSpan.FromSeconds(3));
    }
    private static void Migration()
    {
        var legacy = JsonSerializer.Deserialize<AppSettings>("{\"AreaId\":\"old-area\",\"OpenRgb\":{\"Enabled\":true}}") ?? throw new Exception("Missing legacy settings.");
        Check(legacy.HueEnabled == null && legacy.OpenRgb?.Enabled == true, "Legacy optional outputs were lost.");
        Check(OutputSelection.MigrateHue(legacy.HueEnabled, true), "An existing paired Hue installation lost its output selection.");
        Check(!OutputSelection.MigrateHue(null, false) && !OutputSelection.MigrateHue(false, true),
            "Fresh or explicitly disabled Hue was forced on.");
        Check(OutputSelection.MigrateHue(true, false), "Explicit Hue setup intent was discarded.");
    }
}
