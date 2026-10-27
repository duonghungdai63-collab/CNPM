using DaLat.BLL.Common;
using DaLat.DAL.Entities;
using DaLat.DAL.Repositories;

namespace DaLat.BLL.Services;

/// <summary>API công khai cho du khách: chọn bản dịch theo ngôn ngữ, tính khoảng cách.</summary>
public class PlaceService(IPlaceRepository places, ILanguageRepository languages)
{
    /// <summary>Chọn bản dịch: đúng ngôn ngữ → en → vi (BR3). Bỏ qua ngôn ngữ đang tắt (BR7).</summary>
    public static PlaceTranslation? Pick(IEnumerable<PlaceTranslation> all, string lang)
    {
        var ts = all.Where(t => t.Language == null || t.Language.IsActive).ToList();
        return ts.FirstOrDefault(t => t.LanguageCode == lang)
            ?? ts.FirstOrDefault(t => t.LanguageCode == "en")
            ?? ts.FirstOrDefault(t => t.LanguageCode == "vi");
    }

    /// <summary>Danh sách địa điểm đang hiển thị; có tọa độ thì kèm khoảng cách và sắp gần → xa.</summary>
    public async Task<List<PlaceDto>> GetPlacesAsync(string? lang, double? lat = null, double? lng = null)
    {
        var active = (await languages.GetActiveAsync()).Select(l => l.Code).ToList();
        var code = LanguageResolver.Resolve(lang, active);
        var list = new List<PlaceDto>();

        foreach (var p in await places.GetActiveWithTranslationsAsync())
        {
            var t = Pick(p.Translations, code);
            if (t == null) continue;

            double? dist = lat.HasValue && lng.HasValue
                ? Math.Round(GeoHelper.DistanceMeters(lat.Value, lng.Value, p.Latitude, p.Longitude), 1)
                : null;

            list.Add(new PlaceDto(
                p.Id, p.Slug, p.Latitude, p.Longitude, p.RadiusMeters, p.Address, p.ImageUrl,
                t.Name, t.NarrationText, t.AudioUrl, t.LanguageCode, t.LanguageCode != code,
                dist, dist.HasValue ? dist <= p.RadiusMeters : null));
        }

        return list.OrderBy(x => x.DistanceMeters ?? double.MaxValue).ThenBy(x => x.Id).ToList();
    }

    public async Task<PlaceDto> GetBySlugAsync(string slug, string? lang)
    {
        var all = await GetPlacesAsync(lang);
        return all.FirstOrDefault(p => p.Slug == slug) ?? throw new NotFoundException("Không tìm thấy địa điểm.");
    }
}
