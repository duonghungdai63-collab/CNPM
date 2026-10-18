namespace DaLat.DAL.Entities;

/// <summary>Thiết bị ẩn danh (ClientId).</summary>
public class Visitor
{
    public int Id { get; set; }
    public string ClientId { get; set; } = "";
    public string? DeviceLanguage { get; set; }
    public DateTime FirstSeenAt { get; set; } = DateTime.UtcNow;
    public DateTime LastSeenAt { get; set; } = DateTime.UtcNow;
    public int VisitCount { get; set; }
    public ICollection<VisitSession> Sessions { get; set; } = new List<VisitSession>();
}

/// <summary>Một phiên truy cập (mỗi tab một phiên).</summary>
public class VisitSession
{
    public int Id { get; set; }
    public int VisitorId { get; set; }
    public Visitor Visitor { get; set; } = null!;
    public string SessionKey { get; set; } = "";
    public string? IpAddress { get; set; }
    public string? UserAgent { get; set; }
    public string DeviceType { get; set; } = "desktop";
    public string Browser { get; set; } = "Other";
    public string Os { get; set; } = "Other";
    public string? LanguageCode { get; set; }
    public bool GpsEnabled { get; set; }
    public double? Latitude { get; set; }
    public double? Longitude { get; set; }
    public DateTime StartedAt { get; set; } = DateTime.UtcNow;
    public DateTime LastSeenAt { get; set; } = DateTime.UtcNow;
    public int PingCount { get; set; }
}

/// <summary>Nhật ký lượt nghe (TriggerType: gps | manual).</summary>
public class PlayLog
{
    public long Id { get; set; }
    public int PlaceId { get; set; }
    public Place Place { get; set; } = null!;
    public int? VisitorId { get; set; }
    public Visitor? Visitor { get; set; }
    public string LanguageCode { get; set; } = "";
    public string TriggerType { get; set; } = "gps";
    public DateTime PlayedAt { get; set; } = DateTime.UtcNow;
}

public class AdminUser
{
    public int Id { get; set; }
    public string Username { get; set; } = "";
    public string PasswordHash { get; set; } = "";
    public string DisplayName { get; set; } = "";
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
