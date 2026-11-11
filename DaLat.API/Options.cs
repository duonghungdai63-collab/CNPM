namespace DaLat.API;

public class JwtOptions
{
    public string Key { get; set; } = "";
    public string Issuer { get; set; } = "DaLatAudioGuide";
    public string Audience { get; set; } = "DaLatAudioGuide.Admin";
    public int ExpireMinutes { get; set; } = 480;
}

public class OllamaOptions
{
    public const string Section = "Ollama";
    public string BaseUrl { get; set; } = "http://host.docker.internal:11434";
    public string Model { get; set; } = "translategemma:4b";
    /// <summary>Thời gian chờ mỗi yêu cầu dịch, 30–3600 giây (BR19).</summary>
    public int TimeoutSeconds { get; set; } = 600;

    public static bool IsValid(OllamaOptions o) =>
        Uri.TryCreate(o.BaseUrl, UriKind.Absolute, out var uri) && (uri.Scheme is "http" or "https")
        && !string.IsNullOrWhiteSpace(o.Model)
        && o.TimeoutSeconds is >= 30 and <= 3600;
}

public record UploadSettings(string Path);
