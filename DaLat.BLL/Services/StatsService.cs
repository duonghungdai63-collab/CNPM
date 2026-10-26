using DaLat.BLL.Common;
using DaLat.DAL.Repositories;
using Microsoft.EntityFrameworkCore;

namespace DaLat.BLL.Services;

/// <summary>Thống kê truy cập và lượt nghe (UC9).</summary>
public class StatsService(IVisitorRepository repo, IPlaceRepository places)
{
    private static (DateTime From, DateTime ToExclusive, DateOnly FromD, DateOnly ToD) Range(DateOnly? from, DateOnly? to)
    {
        var t = to ?? DateOnly.FromDateTime(DateTime.UtcNow);
        var f = from ?? t.AddDays(-29);
        if (f > t) throw new AppException("Ngày bắt đầu phải trước ngày kết thúc.");
        if (t.DayNumber - f.DayNumber > 366) throw new AppException("Khoảng ngày tối đa 366 ngày.");
        return (f.ToDateTime(TimeOnly.MinValue), t.AddDays(1).ToDateTime(TimeOnly.MinValue), f, t);
    }

    public async Task<VisitStatsDto> GetVisitStatsAsync(DateOnly? from, DateOnly? to)
    {
        var (f, tx, fd, td) = Range(from, to);
        var q = repo.Sessions.Where(s => s.StartedAt >= f && s.StartedAt < tx);

        var total = await q.CountAsync();
        var unique = await q.Select(s => s.VisitorId).Distinct().CountAsync();

        // Nhóm theo ngày ở phía bộ nhớ để không phụ thuộc cách dịch hàm ngày của provider.
        var starts = await q.Select(s => s.StartedAt).ToListAsync();
        var byDay = starts.GroupBy(d => DateOnly.FromDateTime(d)).ToDictionary(g => g.Key, g => g.Count());
        var days = Enumerable.Range(0, td.DayNumber - fd.DayNumber + 1)
            .Select(i => fd.AddDays(i)).Select(d => new DayCount(d, byDay.GetValueOrDefault(d))).ToList();

        return new VisitStatsDto(fd, td, total, unique, days,
            await Group(q.Select(s => s.DeviceType)),
            await Group(q.Select(s => s.Browser)),
            await Group(q.Select(s => s.LanguageCode ?? "unknown")));
    }

    public async Task<PlayStatsDto> GetPlayStatsAsync(DateOnly? from, DateOnly? to)
    {
        var (f, tx, _, _) = Range(from, to);
        var q = repo.PlayLogs.Where(p => p.PlayedAt >= f && p.PlayedAt < tx);

        var perPlace = await q.GroupBy(p => new { p.PlaceId, p.TriggerType })
            .Select(g => new { g.Key.PlaceId, g.Key.TriggerType, Count = g.Count() }).ToListAsync();

        var all = await places.GetAllWithTranslationsAsync();
        var byPlace = perPlace.GroupBy(x => x.PlaceId).Select(g =>
        {
            var place = all.FirstOrDefault(p => p.Id == g.Key);
            var name = place?.Translations.FirstOrDefault(t => t.LanguageCode == "vi")?.Name ?? place?.Slug ?? $"#{g.Key}";
            var gps = g.Where(x => x.TriggerType == "gps").Sum(x => x.Count);
            var manual = g.Where(x => x.TriggerType == "manual").Sum(x => x.Count);
            return new PlaceStatDto(g.Key, place?.Slug ?? "", name, gps + manual, gps, manual);
        }).OrderByDescending(x => x.Total).ToList(); // xếp hạng địa điểm được nghe nhiều

        return new PlayStatsDto(byPlace.Sum(x => x.Total), byPlace.Sum(x => x.Gps), byPlace.Sum(x => x.Manual),
            byPlace, await Group(q.Select(p => p.LanguageCode)));
    }

    private static async Task<List<CountItem>> Group(IQueryable<string> keys) =>
        (await keys.GroupBy(k => k).Select(g => new { g.Key, Count = g.Count() }).ToListAsync())
            .OrderByDescending(x => x.Count).Select(x => new CountItem(x.Key, x.Count)).ToList();
}