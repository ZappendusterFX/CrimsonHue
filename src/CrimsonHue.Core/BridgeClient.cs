using System.Net;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text.Json;

namespace CrimsonHue.Core;

public sealed record BridgeProbe(string Address, string Name, string BridgeId, string? CertificateSha256);

public sealed class BridgeClient : IDisposable
{
    private readonly HttpClient http;
    public BridgeCredentials Credentials { get; }
    public BridgeClient(BridgeCredentials credentials)
    {
        Credentials = credentials;
        http = CreateHttp(credentials.Address, credentials.CertificateSha256);
        if (!string.IsNullOrWhiteSpace(credentials.ApplicationKey))
            http.DefaultRequestHeaders.Add("hue-application-key", credentials.ApplicationKey);
    }
    public static string NormalizeAddress(string input)
    {
        if (!input.Contains("://")) input = "https://" + input;
        if (!Uri.TryCreate(input, UriKind.Absolute, out var uri) || !IPAddress.TryParse(uri.Host, out var ip) ||
            ip.AddressFamily != System.Net.Sockets.AddressFamily.InterNetwork || uri.UserInfo.Length > 0 ||
            uri.AbsolutePath != "/" || uri.Query.Length > 0 || uri.Fragment.Length > 0 ||
            !(IPAddress.IsLoopback(ip) || ip.GetAddressBytes()[0] == 10 || (ip.GetAddressBytes()[0] == 172 && ip.GetAddressBytes()[1] is >= 16 and <= 31) ||
              (ip.GetAddressBytes()[0] == 192 && ip.GetAddressBytes()[1] == 168)) ||
            (uri.Scheme != "https" && !(uri.Scheme == "http" && IPAddress.IsLoopback(ip))))
            throw new CrimsonHueException("Enter the bridge's local IPv4 address (HTTPS), for example 192.168.2.109.");
        return uri.GetLeftPart(UriPartial.Authority);
    }
    private static HttpClient CreateHttp(string address, string? pin, Action<string>? observeCertificate = null)
    {
        var handler = new HttpClientHandler { AllowAutoRedirect = false, UseProxy = false };
        handler.ServerCertificateCustomValidationCallback = (_, cert, _, errors) =>
        {
            if (cert == null) return false;
            var fingerprint = Convert.ToHexString(SHA256.HashData(cert.RawData));
            // Probe sends no credentials. The UI displays this identity before explicit pairing.
            if (observeCertificate != null) { observeCertificate(fingerprint); return true; }
            return pin != null ? string.Equals(pin, fingerprint, StringComparison.OrdinalIgnoreCase) : errors == System.Net.Security.SslPolicyErrors.None;
        };
        return new HttpClient(handler) { BaseAddress = new Uri(NormalizeAddress(address)), Timeout = TimeSpan.FromSeconds(5), MaxResponseContentBufferSize = 8 * 1024 * 1024 };
    }
    public static async Task<BridgeProbe> ProbeAsync(string address, CancellationToken token = default)
    {
        address = NormalizeAddress(address);
        string? pin = null;
        using var http = CreateHttp(address, null, value => pin = value);
        using var response = await http.GetAsync("/api/config", token);
        response.EnsureSuccessStatusCode();
        using var json = JsonDocument.Parse(await response.Content.ReadAsByteArrayAsync(token));
        var root = json.RootElement;
        return new(address, root.GetProperty("name").GetString() ?? "Hue Bridge", root.GetProperty("bridgeid").GetString() ?? "Unknown", pin);
    }
    public static async Task<BridgeCredentials> PairAsync(BridgeProbe probe, CancellationToken token = default)
    {
        using var http = CreateHttp(probe.Address, probe.CertificateSha256);
        using var response = await http.PostAsJsonAsync("/api", new { devicetype = "CrimsonHue#Windows", generateclientkey = true }, token);
        response.EnsureSuccessStatusCode();
        using var json = JsonDocument.Parse(await response.Content.ReadAsByteArrayAsync(token));
        var item = json.RootElement[0];
        if (item.TryGetProperty("error", out var error))
        {
            if (error.TryGetProperty("type", out var type) && type.GetInt32() == 101)
                throw new CrimsonHueException("Press the bridge link button (in diyHue's web UI), then click Pair again within 30 seconds.");
            throw new CrimsonHueException("Bridge rejected pairing. Check the link button and try again.");
        }
        var success = item.GetProperty("success");
        var key = success.GetProperty("username").GetString()!;
        var client = success.GetProperty("clientkey").GetString()!;
        if (string.IsNullOrWhiteSpace(key) || client.Length != 32 || !client.All(Uri.IsHexDigit))
            throw new CrimsonHueException("Bridge did not return a valid Entertainment client key.");
        return new(probe.Address, key, client, probe.CertificateSha256);
    }
    public async Task<JsonDocument> GetAsync(string path, CancellationToken token = default)
    {
        using var response = await http.GetAsync(path, token);
        if (response.StatusCode == HttpStatusCode.Forbidden || response.StatusCode == HttpStatusCode.Unauthorized)
            throw new CrimsonHueException("Bridge authorization expired. Pair this app again.");
        response.EnsureSuccessStatusCode();
        var json = JsonDocument.Parse(await response.Content.ReadAsByteArrayAsync(token));
        try { CheckErrors(json.RootElement); return json; } catch { json.Dispose(); throw; }
    }
    private static void CheckErrors(JsonElement root)
    {
        if (root.TryGetProperty("errors", out var errors) && errors.GetArrayLength() != 0)
            throw new CrimsonHueException("Bridge rejected the request. Check the selected Entertainment area.");
    }
    public async Task<IReadOnlyList<EntertainmentArea>> GetAreasAsync(CancellationToken token = default)
    {
        using var areas = await GetAsync("/clip/v2/resource/entertainment_configuration", token);
        using var lights = await GetAsync("/clip/v2/resource/light", token);
        using var entertainment = await GetAsync("/clip/v2/resource/entertainment", token);
        var names = lights.RootElement.GetProperty("data").EnumerateArray().ToDictionary(x => x.GetProperty("id").GetString()!, x => x.GetProperty("metadata").GetProperty("name").GetString() ?? "Light");
        var labels = new Dictionary<string, string>();
        foreach (var e in entertainment.RootElement.GetProperty("data").EnumerateArray())
            if (e.TryGetProperty("renderer_reference", out var reference) && reference.TryGetProperty("rid", out var rid) && names.TryGetValue(rid.GetString()!, out var name))
                labels[e.GetProperty("id").GetString()!] = name;
        return areas.RootElement.GetProperty("data").EnumerateArray().Select(x => ParseArea(x, labels)).ToArray();
    }
    public static EntertainmentArea ParseArea(JsonElement area, IReadOnlyDictionary<string, string>? labels = null)
    {
        var id = area.GetProperty("id").GetString()!;
        if (!Guid.TryParseExact(id, "D", out _)) throw new CrimsonHueException("Invalid Entertainment area identifier.");
        var brightness = new Dictionary<string, double>();
        if (area.TryGetProperty("locations", out var locations) && locations.TryGetProperty("service_locations", out var serviceLocations))
            foreach (var loc in serviceLocations.EnumerateArray())
                if (loc.TryGetProperty("equalization_factor", out var eq))
                {
                    var factor = eq.GetDouble();
                    if (!double.IsFinite(factor) || factor < 0 || factor > 1) throw new CrimsonHueException("Invalid Hue brightness balance.");
                    brightness[loc.GetProperty("service").GetProperty("rid").GetString()!] = factor;
                }
        var channels = new List<EntertainmentChannel>();
        foreach (var c in area.GetProperty("channels").EnumerateArray())
        {
            var channelId = c.GetProperty("channel_id").GetInt32();
            if (channelId is < 0 or > 255 || channels.Any(x => x.Id == channelId)) throw new CrimsonHueException("Invalid or duplicate Entertainment channel.");
            var position = Vec3.Read(c.GetProperty("position"));
            if (Math.Max(Math.Abs(position.X), Math.Max(Math.Abs(position.Y), Math.Abs(position.Z))) > 2)
                throw new CrimsonHueException("Entertainment position is outside the supported room coordinates.");
            var members = c.GetProperty("members").EnumerateArray().Select(m => new ChannelMember(m.GetProperty("service").GetProperty("rid").GetString()!, m.GetProperty("index").GetInt32())).ToArray();
            if (members.Length == 0 || members.Any(m => string.IsNullOrWhiteSpace(m.ServiceId) || m.SegmentIndex < 0)) throw new CrimsonHueException("Entertainment channel has no valid light members.");
            var label = string.Join(" + ", members.Select(m => labels != null && labels.TryGetValue(m.ServiceId, out var name) ? name : $"Channel {channelId}" ).Distinct());
            channels.Add(new((byte)channelId, position, members, label, members.Min(m => brightness.GetValueOrDefault(m.ServiceId, 1))));
        }
        if (channels.Count > 160) throw new CrimsonHueException("This area exceeds CrimsonHue's 160-channel datagram limit.");
        return new(id, area.GetProperty("metadata").GetProperty("name").GetString() ?? "Entertainment", area.GetProperty("status").GetString() == "active", channels);
    }
    public async Task<JsonElement> GetAreaStateAsync(string id, CancellationToken token = default)
    {
        using var doc = await GetAsync("/clip/v2/resource/entertainment_configuration/" + id, token);
        return doc.RootElement.GetProperty("data")[0].Clone();
    }
    public async Task SetStreamingAsync(string id, bool start, CancellationToken token = default)
    {
        using var response = await http.PutAsJsonAsync("/clip/v2/resource/entertainment_configuration/" + id, new { action = start ? "start" : "stop" }, token);
        response.EnsureSuccessStatusCode();
        using var doc = JsonDocument.Parse(await response.Content.ReadAsByteArrayAsync(token));
        CheckErrors(doc.RootElement);
    }
    public async Task<Dictionary<string, JsonElement>> CaptureLightStatesAsync(EntertainmentArea area, CancellationToken token)
    {
        using var entertainment = await GetAsync("/clip/v2/resource/entertainment", token);
        var selected = area.Channels.SelectMany(c => c.Members).Select(m => m.ServiceId).ToHashSet();
        var ids = entertainment.RootElement.GetProperty("data").EnumerateArray()
            .Where(e => selected.Contains(e.GetProperty("id").GetString()!) && e.TryGetProperty("renderer_reference", out _))
            .Select(e => e.GetProperty("renderer_reference").GetProperty("rid").GetString()!).ToHashSet();
        using var lights = await GetAsync("/clip/v2/resource/light", token);
        var result = new Dictionary<string, JsonElement>();
        foreach (var light in lights.RootElement.GetProperty("data").EnumerateArray())
        {
            var id = light.GetProperty("id").GetString()!;
            if (!ids.Contains(id)) continue;
            var state = new Dictionary<string, object>();
            if (light.TryGetProperty("on", out var on)) state["on"] = new { on = on.GetProperty("on").GetBoolean() };
            if (light.TryGetProperty("dimming", out var dim)) state["dimming"] = new { brightness = dim.GetProperty("brightness").GetDouble() };
            if (light.TryGetProperty("color_temperature", out var ct) && ct.TryGetProperty("mirek_valid", out var valid) && valid.ValueKind == JsonValueKind.True && ct.GetProperty("mirek").ValueKind == JsonValueKind.Number)
                state["color_temperature"] = new { mirek = ct.GetProperty("mirek").GetInt32() };
            else if (light.TryGetProperty("color", out var color)) state["color"] = new { xy = color.GetProperty("xy").Clone() };
            if (light.TryGetProperty("gradient", out var gradient) && gradient.TryGetProperty("points", out var points) && points.GetArrayLength() > 0)
                state["gradient"] = new { points = points.Clone() };
            result[id] = JsonSerializer.SerializeToElement(state);
        }
        if (result.Count != ids.Count || ids.Count == 0) throw new CrimsonHueException("Could not read the selected lights before starting.");
        return result;
    }
    public async Task RestoreLightStatesAsync(Dictionary<string, JsonElement> states, CancellationToken token)
    {
        foreach (var (id, state) in states)
        {
            using var response = await http.PutAsJsonAsync("/clip/v2/resource/light/" + id, state, token);
            response.EnsureSuccessStatusCode();
            using var doc = JsonDocument.Parse(await response.Content.ReadAsByteArrayAsync(token));
            CheckErrors(doc.RootElement);
        }
    }
    public void Dispose() => http.Dispose();
}
