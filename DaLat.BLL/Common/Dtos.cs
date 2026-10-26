namespace DaLat.BLL.Common;

// ---------- Ngôn ngữ ----------
public record LanguageDto(string Code, string Name, string NativeName, string SpeechLocale, bool IsActive, int SortOrder);
public record LanguageUpsertRequest(string Code, string Name, string NativeName, string SpeechLocale, bool IsActive, int SortOrder);
public record SetActiveRequest(bool IsActive);

// ---------- Địa điểm (du khách) ----------
public record PlaceDto(
    int Id, string Slug, double Latitude, double Longitude, int RadiusMeters,
    string? Address, string? ImageUrl,
    string Name, string NarrationText, string? AudioUrl,
    string LanguageCode, bool IsFallback,
    double? DistanceMeters, bool? InRange);

// ---------- Địa điểm (quản trị) ----------
public record TranslationDto(string LanguageCode, string Name, string NarrationText, string? AudioUrl);

public record PlaceAdminDto(
    int Id, string Slug, double Latitude, double Longitude, int RadiusMeters,
    string? Address, string? ImageUrl, bool IsActive,
    DateTime CreatedAt, DateTime UpdatedAt, List<TranslationDto> Translations);

public record PlaceUpsertRequest(
    string Slug, double Latitude, double Longitude, int RadiusMeters,
    string? Address, string? ImageUrl, bool IsActive, List<TranslationDto>? Translations);

/// <summary>Yêu cầu dịch tự động: nguồn luôn là tiếng Việt.</summary>
public record AutoTranslateRequest(string TargetLanguage, string Name, string NarrationText);

// ---------- Xác thực ----------
public record LoginRequest(string Username, string Password);
public record LoginResponse(string Token, DateTime ExpiresAt, string DisplayName);
public record ChangePasswordRequest(string CurrentPassword, string NewPassword);

// ---------- Theo dõi truy cập ----------
public record PingRequest(
    string ClientId, string SessionKey, string? DeviceLanguage, string? LanguageCode,
    bool GpsEnabled, bool Demo, double? Latitude, double? Longitude);

public record PlayLogRequest(int PlaceId, string LanguageCode, string TriggerType, string? ClientId);

public record OnlineSessionDto(
    int Id, string DeviceType, string Browser, string Os, string? LanguageCode,
    bool GpsEnabled, double? Latitude, double? Longitude, DateTime StartedAt, DateTime LastSeenAt);

// ---------- Thống kê ----------
public record CountItem(string Key, int Count);
public record DayCount(DateOnly Date, int Sessions);

public record VisitStatsDto(
    DateOnly From, DateOnly To, int TotalSessions, int UniqueVisitors,
    List<DayCount> ByDay, List<CountItem> ByDevice, List<CountItem> ByBrowser, List<CountItem> ByLanguage);

public record PlaceStatDto(int PlaceId, string Slug, string Name, int Total, int Gps, int Manual);

public record PlayStatsDto(int Total, int Gps, int Manual, List<PlaceStatDto> ByPlace, List<CountItem> ByLanguage);
