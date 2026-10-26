using System.Security.Cryptography;

namespace DaLat.BLL.Common;

public static class GeoHelper
{
    private const double EarthRadiusMeters = 6_371_000;

    /// <summary>Khoảng cách Haversine (mét).</summary>
    public static double DistanceMeters(double lat1, double lon1, double lat2, double lon2)
    {
        static double Rad(double d) => d * Math.PI / 180;
        var dLat = Rad(lat2 - lat1);
        var dLon = Rad(lon2 - lon1);
        var a = Math.Sin(dLat / 2) * Math.Sin(dLat / 2) +
                Math.Cos(Rad(lat1)) * Math.Cos(Rad(lat2)) * Math.Sin(dLon / 2) * Math.Sin(dLon / 2);
        return EarthRadiusMeters * 2 * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1 - a));
    }

    /// <summary>Làm tròn 3 chữ số thập phân (~110 m) để bảo vệ riêng tư (BR12).</summary>
    public static double RoundForPrivacy(double value) => Math.Round(value, 3);
}

public static class LanguageResolver
{
    /// <summary>vi-VN → vi, zh-TW → zh; chưa hỗ trợ thì dùng en (BR9).</summary>
    public static string Resolve(string? requested, IEnumerable<string> activeCodes)
    {
        var active = activeCodes.ToHashSet(StringComparer.OrdinalIgnoreCase);
        var code = (requested ?? "").Trim().ToLowerInvariant().Split('-', '_')[0];
        if (code.Length > 0 && active.Contains(code)) return code;
        return "en";
    }
}

public static class UserAgentParser
{
    public static (string Device, string Browser, string Os) Parse(string? ua)
    {
        ua ??= "";
        string device = ua.Contains("iPad") || ua.Contains("Tablet") ? "tablet"
            : ua.Contains("Mobi") || ua.Contains("iPhone") || ua.Contains("Android") ? "mobile"
            : "desktop";

        string browser =
            ua.Contains("Edg/") ? "Edge" :
            ua.Contains("OPR/") ? "Opera" :
            ua.Contains("Firefox/") || ua.Contains("FxiOS/") ? "Firefox" :
            ua.Contains("Chrome/") || ua.Contains("CriOS/") ? "Chrome" :
            ua.Contains("Safari/") ? "Safari" : "Other";

        string os =
            ua.Contains("Windows") ? "Windows" :
            ua.Contains("Android") ? "Android" :
            ua.Contains("iPhone") || ua.Contains("iPad") || ua.Contains("iPod") ? "iOS" :
            ua.Contains("Mac OS X") || ua.Contains("Macintosh") ? "macOS" :
            ua.Contains("Linux") ? "Linux" : "Other";

        return (device, browser, os);
    }
}

/// <summary>Băm mật khẩu PBKDF2-SHA256 (BR8).</summary>
public static class PasswordHasher
{
    private const int Iterations = 210_000;
    private const int SaltSize = 16;
    private const int KeySize = 32;
    private const string Prefix = "PBKDF2-SHA256";

    public static string Hash(string password)
    {
        var salt = RandomNumberGenerator.GetBytes(SaltSize);
        var key = Rfc2898DeriveBytes.Pbkdf2(password, salt, Iterations, HashAlgorithmName.SHA256, KeySize);
        return $"{Prefix}${Iterations}${Convert.ToBase64String(salt)}${Convert.ToBase64String(key)}";
    }

    public static bool Verify(string password, string stored)
    {
        try
        {
            var parts = stored.Split('$');
            if (parts.Length != 4 || parts[0] != Prefix) return false;
            var iterations = int.Parse(parts[1]);
            var salt = Convert.FromBase64String(parts[2]);
            var expected = Convert.FromBase64String(parts[3]);
            var actual = Rfc2898DeriveBytes.Pbkdf2(password, salt, iterations, HashAlgorithmName.SHA256, expected.Length);
            return CryptographicOperations.FixedTimeEquals(actual, expected);
        }
        catch (FormatException) { return false; }
    }
}

/// <summary>Chia văn bản thành đoạn tối đa N ký tự, ưu tiên ngắt ở dấu câu (BR21).</summary>
public static class TextChunker
{
    private static readonly char[] SentenceEnds = ['.', '!', '?', '。', '！', '？', '…', ';', '\n'];

    public static List<string> Split(string text, int maxLength = 600)
    {
        var result = new List<string>();
        var rest = (text ?? "").Trim();
        while (rest.Length > maxLength)
        {
            var cut = FindCut(rest, maxLength);
            result.Add(rest[..cut].Trim());
            rest = rest[cut..].Trim();
        }
        if (rest.Length > 0) result.Add(rest);
        return result;
    }

    private static int FindCut(string s, int max)
    {
        var minCut = max / 3; // tránh đoạn quá ngắn
        for (var i = max - 1; i >= minCut; i--)
            if (Array.IndexOf(SentenceEnds, s[i]) >= 0) return i + 1;
        for (var i = max - 1; i >= minCut; i--)
            if (char.IsWhiteSpace(s[i]) || s[i] == ',' || s[i] == '，' || s[i] == '、') return i + 1;
        return max; // cắt cứng (văn bản không có khoảng trắng như CJK dài)
    }
}
