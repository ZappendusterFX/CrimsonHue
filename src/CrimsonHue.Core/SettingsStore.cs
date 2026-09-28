using System.Security.Cryptography;
using System.Text.Json;

namespace CrimsonHue.Core;

public sealed record AppSettings(string BridgeAddress = "192.168.2.109", string TelemetryAddress = "ws://127.0.0.1:27311/v1/stream",
    string? AreaId = null, MappingSettings? Mapping = null, int MappingRevision = 0);

public sealed class SettingsStore
{
    public string DirectoryPath { get; }
    private static readonly JsonSerializerOptions Options = new() { WriteIndented = true };
    public SettingsStore(string? directory = null)
    {
        DirectoryPath = directory ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "CrimsonHue");
    }
    public AppSettings LoadSettings()
    {
        var path = Path.Combine(DirectoryPath, "settings.json");
        var saved = File.Exists(path) ? JsonSerializer.Deserialize<AppSettings>(File.ReadAllBytes(path)) ?? new() : new();
        // 0.2.1 used 60% as its untouched default. Lift only that exact preset;
        // once saved with a revision, a deliberate 60% selection stays at 60%.
        var oldDefault = new MappingSettings(Brightness: 0.6);
        return saved.MappingRevision == 0 && saved.Mapping == oldDefault
            ? saved with { Mapping = new MappingSettings(), MappingRevision = 1 }
            : saved;
    }
    public BridgeCredentials? LoadCredentials()
    {
        var path = Path.Combine(DirectoryPath, "bridge.secrets");
        if (!File.Exists(path)) return null;
        var clear = ProtectedData.Unprotect(File.ReadAllBytes(path), null, DataProtectionScope.CurrentUser);
        try { return JsonSerializer.Deserialize<BridgeCredentials>(clear); }
        finally { CryptographicOperations.ZeroMemory(clear); }
    }
    public void SaveSettings(AppSettings settings) => SaveAtomic("settings.json", JsonSerializer.SerializeToUtf8Bytes(settings with { MappingRevision = 1 }, Options));
    public void SaveCredentials(BridgeCredentials credentials)
    {
        var clear = JsonSerializer.SerializeToUtf8Bytes(credentials);
        try { SaveAtomic("bridge.secrets", ProtectedData.Protect(clear, null, DataProtectionScope.CurrentUser)); }
        finally { CryptographicOperations.ZeroMemory(clear); }
    }
    private void SaveAtomic(string name, byte[] bytes)
    {
        Directory.CreateDirectory(DirectoryPath);
        var destination = Path.Combine(DirectoryPath, name);
        var temp = destination + ".tmp";
        File.WriteAllBytes(temp, bytes);
        File.Move(temp, destination, true);
    }
}
