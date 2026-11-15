using DaLat.BLL.Common;
using Xunit;

namespace DaLat.Tests;

public class HelperTests
{
    [Fact]
    public void Haversine_SamePoint_IsZero() =>
        Assert.Equal(0, GeoHelper.DistanceMeters(11.9437, 108.4419, 11.9437, 108.4419), 3);

    [Fact]
    public void Haversine_KnownDistance_IsReasonable()
    {
        // Hồ Xuân Hương → Ga Đà Lạt: khoảng 1,3 km
        var d = GeoHelper.DistanceMeters(11.9437, 108.4419, 11.9427, 108.4551);
        Assert.InRange(d, 1200, 1600);
    }

    [Fact]
    public void PrivacyRounding_KeepsAbout110Meters() =>
        Assert.Equal(11.944, GeoHelper.RoundForPrivacy(11.94372));

    [Theory]
    [InlineData("vi-VN", "vi")]
    [InlineData("zh-TW", "zh")]
    [InlineData("fr-FR", "en")]   // chưa hỗ trợ → tiếng Anh (BR9)
    [InlineData(null, "en")]
    public void LanguageResolver_FollowsBr9(string? requested, string expected) =>
        Assert.Equal(expected, LanguageResolver.Resolve(requested, ["vi", "en", "zh", "ja", "ko"]));

    [Fact]
    public void Password_HashAndVerify()
    {
        var hash = PasswordHasher.Hash("Admin@12345");
        Assert.StartsWith("PBKDF2-SHA256$", hash);
        Assert.True(PasswordHasher.Verify("Admin@12345", hash));
        Assert.False(PasswordHasher.Verify("sai-mat-khau", hash));
        Assert.False(PasswordHasher.Verify("x", "khong-phai-hash"));
    }

    [Fact]
    public void UserAgent_Mobile_Chrome_Android()
    {
        var (device, browser, os) = UserAgentParser.Parse(
            "Mozilla/5.0 (Linux; Android 14; Pixel 8) AppleWebKit/537.36 Chrome/126.0 Mobile Safari/537.36");
        Assert.Equal(("mobile", "Chrome", "Android"), (device, browser, os));
    }
}
