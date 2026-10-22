using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace QwenLiveTranslate;

public sealed class Settings
{
    public const string Model = "qwen3.8-livetranslate-flash-realtime";
    public static readonly string Folder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "QwenLiveTranslate");
    public string Endpoint { get; set; } = "wss://maas.qianwenaiapi.com/api-ws/v1/realtime";
    public string DeviceId { get; set; } = "";
    public static Settings Load()
    {
        try { return JsonSerializer.Deserialize<Settings>(File.ReadAllText(Path.Combine(Folder, "settings.json"))) ?? new(); }
        catch { return new(); }
    }
    public void Save(string key)
    {
        Directory.CreateDirectory(Folder);
        File.WriteAllText(Path.Combine(Folder, "settings.json"), JsonSerializer.Serialize(this));
        File.WriteAllBytes(Path.Combine(Folder, "secret.bin"), ProtectedData.Protect(Encoding.UTF8.GetBytes(key), null, DataProtectionScope.CurrentUser));
    }
    public static string LoadKey()
    {
        try { return Encoding.UTF8.GetString(ProtectedData.Unprotect(File.ReadAllBytes(Path.Combine(Folder, "secret.bin")), null, DataProtectionScope.CurrentUser)); }
        catch { return Environment.GetEnvironmentVariable("DASHSCOPE_API_KEY") ?? ""; }
    }
    public Uri GetUri()
    {
        if (!Uri.TryCreate(Endpoint.Trim(), UriKind.Absolute, out var uri) || uri.Scheme != "wss" || !string.IsNullOrEmpty(uri.UserInfo))
            throw new InvalidOperationException("请填写有效的 wss:// 实时接口地址。");
        var builder = new UriBuilder(uri) { Query = "model=" + Uri.EscapeDataString(Model) };
        return builder.Uri;
    }
}
