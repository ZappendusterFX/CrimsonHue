using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using CrimsonHue.Core;

internal static class TuyaSecretStoreTests
{
    private const string Key = "fixture-only-key";
    private static TuyaLamp Lamp(string id = "fixture-lamp", string host = "127.0.0.1") =>
        new(id, "Synthetic lamp", host, "device-" + id, new(.2, .7, -.1));
    private static void Check(bool condition, string message)
    { if (!condition) throw new Exception(message); }
    private static void Reject(Action action)
    {
        try { action(); }
        catch (CrimsonHueException) { return; }
        throw new Exception("Invalid protected Tuya configuration was accepted.");
    }

    internal static void Register(Action<string, Action> add)
    {
        add("Tuya protected bundle: missing configuration remains absent", () =>
        {
            using var fixture = new Fixture();
            Check(fixture.Store.LoadTuyaConfiguration() == null && !Directory.Exists(fixture.Directory),
                "Reading missing Tuya setup created defaults on disk.");
        });
        add("Tuya protected bundle: layout and key subset round-trip without plaintext files", () =>
        {
            using var fixture = new Fixture();
            var layout = new TuyaLayout(true, .4, [Lamp(), Lamp("unconfigured", "127.0.0.2")], 120, .3);
            fixture.Store.SaveTuyaConfiguration(layout, new Dictionary<string, string> { ["fixture-lamp"] = Key });
            var loaded = fixture.Store.LoadTuyaConfiguration()!;
            Check(JsonSerializer.Serialize(loaded.Layout) == JsonSerializer.Serialize(layout) && loaded.Keys.Count == 1 &&
                loaded.Keys["fixture-lamp"] == Key && !loaded.Keys.ContainsKey("unconfigured"),
                "Protected layout and configured key subset did not round-trip together.");
            Check(Directory.GetFiles(fixture.Directory).Select(Path.GetFileName).SequenceEqual(["tuya.secrets"]),
                "Tuya setup created an additional unprotected file.");
            var bytes = File.ReadAllBytes(fixture.Bundle);
            foreach (var clearText in new[] { Key, "fixture-lamp", "device-fixture-lamp", "127.0.0.1" })
                Check(bytes.AsSpan().IndexOf(Encoding.UTF8.GetBytes(clearText)) < 0,
                    "Protected Tuya configuration contains plaintext connection data.");
        });
        add("Tuya protected bundle: rejected edits preserve exact previous layout and key bytes", () =>
        {
            using var fixture = new Fixture();
            var layout = new TuyaLayout(Devices: [Lamp()]);
            var keys = new Dictionary<string, string> { ["fixture-lamp"] = Key };
            fixture.Store.SaveTuyaConfiguration(layout, keys);
            var before = File.ReadAllBytes(fixture.Bundle);
            Reject(() => fixture.Store.SaveTuyaConfiguration(layout with { Brightness = 2 }, keys));
            Reject(() => fixture.Store.SaveTuyaConfiguration(layout, new Dictionary<string, string> { ["fixture-lamp"] = "invalid" }));
            Reject(() => fixture.Store.SaveTuyaConfiguration(layout, new Dictionary<string, string> { ["orphan"] = Key }));
            Check(File.ReadAllBytes(fixture.Bundle).SequenceEqual(before), "Rejected edit replaced the last valid bundle.");
            var loaded = fixture.Store.LoadTuyaConfiguration()!;
            Check(loaded.Layout.Devices![0] == Lamp() && loaded.Keys["fixture-lamp"] == Key,
                "Rejected edit combined old layout with a different key.");
        });
        add("Tuya protected bundle: failed atomic replacement retains old endpoint and old key", () =>
        {
            using var fixture = new Fixture();
            var initial = new TuyaLayout(Devices: [Lamp()]);
            fixture.Store.SaveTuyaConfiguration(initial, new Dictionary<string, string> { ["fixture-lamp"] = Key });
            var changed = initial with { Devices = [Lamp(host: "127.0.0.2")] };
            const string replacement = "replacement-key!";
            var before = File.ReadAllBytes(fixture.Bundle);
            // Deny replacement of the destination while allowing reads. The
            // encrypted temporary write can succeed, but its rename must fail.
            using (var locked = new FileStream(fixture.Bundle, FileMode.Open, FileAccess.Read, FileShare.Read))
            {
                var failed = false;
                try { fixture.Store.SaveTuyaConfiguration(changed, new Dictionary<string, string> { ["fixture-lamp"] = replacement }); }
                catch (IOException) { failed = true; }
                Check(failed, "The locked destination unexpectedly allowed replacement.");
            }
            Check(File.ReadAllBytes(fixture.Bundle).SequenceEqual(before), "Failed replacement modified the old bundle.");
            var loaded = fixture.Store.LoadTuyaConfiguration()!;
            Check(loaded.Layout.Devices![0].Host == "127.0.0.1" && loaded.Keys["fixture-lamp"] == Key,
                "Failed save paired new credentials with the old endpoint.");
            fixture.Store.SaveTuyaConfiguration(changed, new Dictionary<string, string> { ["fixture-lamp"] = replacement });
            loaded = fixture.Store.LoadTuyaConfiguration()!;
            Check(loaded.Layout.Devices![0].Host == "127.0.0.2" && loaded.Keys["fixture-lamp"] == replacement,
                "Successful replacement did not publish the endpoint and key together.");
        });
        add("Tuya protected bundle: corrupted or invalid protected data is rejected without rewriting", () =>
        {
            using var fixture = new Fixture();
            Directory.CreateDirectory(fixture.Directory);
            var corrupt = new byte[] { 1, 2, 3, 4 };
            File.WriteAllBytes(fixture.Bundle, corrupt);
            var rejected = false;
            try { fixture.Store.LoadTuyaConfiguration(); }
            catch (CryptographicException) { rejected = true; }
            Check(rejected && File.ReadAllBytes(fixture.Bundle).SequenceEqual(corrupt),
                "Corrupt protected storage was accepted or overwritten.");
            var invalid = JsonSerializer.SerializeToUtf8Bytes(new TuyaSavedConfiguration(new(Devices: [Lamp()]),
                new Dictionary<string, string> { ["orphan"] = Key }));
            try { File.WriteAllBytes(fixture.Bundle, ProtectedData.Protect(invalid, null, DataProtectionScope.CurrentUser)); }
            finally { CryptographicOperations.ZeroMemory(invalid); }
            var before = File.ReadAllBytes(fixture.Bundle);
            Reject(() => fixture.Store.LoadTuyaConfiguration());
            Check(File.ReadAllBytes(fixture.Bundle).SequenceEqual(before), "Invalid protected layout was rewritten on load.");
        });
        add("Tuya protected bundle: removing a lamp removes its credential in the same replacement", () =>
        {
            using var fixture = new Fixture();
            fixture.Store.SaveTuyaConfiguration(new(Devices: [Lamp()]), new Dictionary<string, string> { ["fixture-lamp"] = Key });
            fixture.Store.SaveTuyaConfiguration(new(Devices: []), new Dictionary<string, string>());
            var loaded = fixture.Store.LoadTuyaConfiguration()!;
            Check(loaded.Layout.Devices!.Count == 0 && loaded.Keys.Count == 0, "Removed lamp or credential survived replacement.");
            foreach (var length in new[] { 16, 24, 32 }) Check(SettingsStore.ValidTuyaKey(new string('x', length)), "Valid ASCII key length rejected.");
            Check(!SettingsStore.ValidTuyaKey(null) && !SettingsStore.ValidTuyaKey(new string('x', 17)) &&
                !SettingsStore.ValidTuyaKey(new string('é', 16)) && !SettingsStore.ValidTuyaKey(new string('\t', 16)),
                "Invalid length or nonprintable/non-ASCII key accepted.");
        });
    }

    private sealed class Fixture : IDisposable
    {
        public string Directory { get; } = Path.Combine(Path.GetTempPath(), "CrimsonHue-Tuya-bundle-" + Guid.NewGuid().ToString("N"));
        public string Bundle => Path.Combine(Directory, "tuya.secrets");
        public SettingsStore Store { get; }
        public Fixture() => Store = new(Directory);
        public void Dispose()
        {
            if (!System.IO.Directory.Exists(Directory)) return;
            // This fixture creates only flat files under its own unique folder.
            foreach (var file in System.IO.Directory.GetFiles(Directory)) File.Delete(file);
            System.IO.Directory.Delete(Directory);
        }
    }
}
