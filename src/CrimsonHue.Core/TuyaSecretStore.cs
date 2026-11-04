using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace CrimsonHue.Core;

public sealed record TuyaSavedConfiguration(TuyaLayout Layout, IReadOnlyDictionary<string, string> Keys);

public sealed partial class SettingsStore
{
    public static bool ValidTuyaKey(string? value) => value is not null &&
        value.All(c => c is >= ' ' and <= '~') && Encoding.UTF8.GetByteCount(value) is 16 or 24 or 32;

    public TuyaSavedConfiguration? LoadTuyaConfiguration()
    {
        var path = Path.Combine(DirectoryPath, "tuya.secrets");
        if (!File.Exists(path)) return null;
        var clear = ProtectedData.Unprotect(File.ReadAllBytes(path), null, DataProtectionScope.CurrentUser);
        try
        {
            var result = JsonSerializer.Deserialize<TuyaSavedConfiguration>(clear) ??
                throw new CrimsonHueException("Invalid saved Tuya configuration.");
            ValidateTuyaConfiguration(result.Layout, result.Keys);
            return result;
        }
        finally { CryptographicOperations.ZeroMemory(clear); }
    }

    public void SaveTuyaConfiguration(TuyaLayout layout, IReadOnlyDictionary<string, string> keys)
    {
        ValidateTuyaConfiguration(layout, keys);
        // One atomic protected bundle prevents a new key from being paired with
        // an old endpoint/layout after a failed save or process interruption.
        var configuration = new TuyaSavedConfiguration(layout with { Devices = layout.Devices?.ToArray() },
            new Dictionary<string, string>(keys, StringComparer.Ordinal));
        ValidateTuyaConfiguration(configuration.Layout, configuration.Keys);
        var clear = JsonSerializer.SerializeToUtf8Bytes(configuration);
        try { SaveAtomic("tuya.secrets", ProtectedData.Protect(clear, null, DataProtectionScope.CurrentUser)); }
        finally { CryptographicOperations.ZeroMemory(clear); }
    }

    private static void ValidateTuyaConfiguration(TuyaLayout? layout, IReadOnlyDictionary<string, string>? keys)
    {
        if (layout == null || keys == null)
            throw new CrimsonHueException("Invalid saved Tuya configuration.");
        layout.Validate();
        var ids = (layout.Devices ?? []).Select(lamp => lamp.Id).ToHashSet(StringComparer.Ordinal);
        // Missing keys are allowed so incomplete lamps can be displayed for setup.
        // Orphan keys are not: removing a lamp must also remove its credential.
        if (keys.Count > TuyaLayout.MaximumDevices || keys.Any(kv => !ids.Contains(kv.Key) || !ValidTuyaKey(kv.Value)))
            throw new CrimsonHueException("Invalid Tuya credentials. Keys must belong to configured lamps.");
    }
}
