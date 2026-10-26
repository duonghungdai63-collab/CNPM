using DaLat.BLL.Common;
using DaLat.DAL.Entities;
using DaLat.DAL.Repositories;

namespace DaLat.BLL.Services;

/// <summary>Ghi nhận truy cập ẩn danh, ping, người online, nhật ký lượt nghe (UC10, UC12).</summary>
public class VisitorService(IVisitorRepository repo, IPlaceRepository places)
{
    public const int OnlineWindowSeconds = 90; // BR11
    public const int RetentionDays = 90;

    private static readonly string[] Triggers = ["gps", "manual"];

    /// <summary>Tín hiệu ping mỗi 30 giây từ mỗi tab.</summary>
    public async Task PingAsync(PingRequest r, string? ip, string? userAgent)
    {
        var clientId = (r.ClientId ?? "").Trim();
        var sessionKey = (r.SessionKey ?? "").Trim();
        if (clientId.Length is 0 or > 64 || sessionKey.Length is 0 or > 64)
            throw new AppException("ClientId hoặc SessionKey không hợp lệ.");

        var now = DateTime.UtcNow;
        var visitor = await repo.FindVisitorAsync(clientId);
        if (visitor == null)
        {
            visitor = new Visitor { ClientId = clientId, FirstSeenAt = now, LastSeenAt = now };
            await repo.AddVisitorAsync(visitor);
        }

        var session = await repo.FindSessionAsync(sessionKey);
        if (session != null && (visitor.Id == 0 || session.VisitorId != visitor.Id))
            throw new AppException("SessionKey không thuộc thiết bị này.");

        if (session == null)
        {
            var (device, browser, os) = UserAgentParser.Parse(userAgent);
            session = new VisitSession
            {
                Visitor = visitor,
                SessionKey = sessionKey,
                IpAddress = Truncate(ip, 64),
                UserAgent = Truncate(userAgent, 500),
                DeviceType = device,
                Browser = browser,
                Os = os,
                StartedAt = now
            };
            visitor.VisitCount++;
            await repo.AddSessionAsync(session);
        }

        session.LastSeenAt = now;
        session.PingCount++;
        session.LanguageCode = Truncate(r.LanguageCode, 10);

        // BR12: chỉ lưu vị trí khi bật GPS thật (không phải demo), làm tròn ~110 m.
        var storeGps = r.GpsEnabled && !r.Demo && r.Latitude.HasValue && r.Longitude.HasValue
                       && r.Latitude is >= -90 and <= 90 && r.Longitude is >= -180 and <= 180;
        session.GpsEnabled = storeGps;
        session.Latitude = storeGps ? GeoHelper.RoundForPrivacy(r.Latitude!.Value) : null;
        session.Longitude = storeGps ? GeoHelper.RoundForPrivacy(r.Longitude!.Value) : null;

        visitor.LastSeenAt = now;
        visitor.DeviceLanguage = Truncate(r.DeviceLanguage, 20) ?? visitor.DeviceLanguage;
        await repo.SaveAsync();
    }

    /// <summary>Ghi một lượt nghe (BR6).</summary>
    public async Task LogPlayAsync(PlayLogRequest r)
    {
        var trigger = (r.TriggerType ?? "").Trim().ToLowerInvariant();
        if (!Triggers.Contains(trigger)) throw new AppException("TriggerType phải là 'gps' hoặc 'manual'.");
        var lang = (r.LanguageCode ?? "").Trim().ToLowerInvariant();
        if (lang.Length is 0 or > 10) throw new AppException("Ngôn ngữ không hợp lệ.");

        var place = await places.GetByIdAsync(r.PlaceId);
        if (place == null || !place.IsActive) throw new NotFoundException("Không tìm thấy địa điểm.");

        Visitor? visitor = string.IsNullOrWhiteSpace(r.ClientId) ? null : await repo.FindVisitorAsync(r.ClientId.Trim());
        await repo.AddPlayLogAsync(new PlayLog
        {
            PlaceId = place.Id,
            VisitorId = visitor?.Id,
            LanguageCode = lang,
            TriggerType = trigger,
            PlayedAt = DateTime.UtcNow
        });
        await repo.SaveAsync();
    }

    /// <summary>Phiên online = có ping trong 90 giây gần nhất.</summary>
    public async Task<List<OnlineSessionDto>> GetOnlineAsync()
    {
        var since = DateTime.UtcNow.AddSeconds(-OnlineWindowSeconds);
        return (await repo.GetOnlineSessionsAsync(since)).Select(s => new OnlineSessionDto(
            s.Id, s.DeviceType, s.Browser, s.Os, s.LanguageCode, s.GpsEnabled,
            s.Latitude, s.Longitude, s.StartedAt, s.LastSeenAt)).ToList();
    }

    /// <summary>Dọn phiên cũ hơn 90 ngày (US-30). Trả về số phiên đã xóa.</summary>
    public Task<int> CleanupOldSessionsAsync() =>
        repo.DeleteSessionsOlderThanAsync(DateTime.UtcNow.AddDays(-RetentionDays));

    private static string? Truncate(string? s, int max) =>
        string.IsNullOrWhiteSpace(s) ? null : (s.Length <= max ? s : s[..max]);
}
