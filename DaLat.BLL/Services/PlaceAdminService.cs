using System.Text.RegularExpressions;
using DaLat.BLL.Common;
using DaLat.DAL.Entities;
using DaLat.DAL.Repositories;

namespace DaLat.BLL.Services;

/// <summary>CRUD địa điểm và bản dịch cho quản trị viên (UC6, UC7).</summary>
public partial class PlaceAdminService(IPlaceRepository places, ILanguageRepository languages)
{
    [GeneratedRegex("^[a-z0-9]+(-[a-z0-9]+)*$")]
    private static partial Regex SlugRegex();

    private static PlaceAdminDto ToDto(Place p) => new(
        p.Id, p.Slug, p.Latitude, p.Longitude, p.RadiusMeters, p.Address, p.ImageUrl, p.IsActive,
        p.CreatedAt, p.UpdatedAt,
        p.Translations.OrderBy(t => t.LanguageCode)
            .Select(t => new TranslationDto(t.LanguageCode, t.Name, t.NarrationText, t.AudioUrl)).ToList());

    public async Task<List<PlaceAdminDto>> ListAsync() =>
        (await places.GetAllWithTranslationsAsync()).Select(ToDto).ToList();

    public async Task<PlaceAdminDto> GetAsync(int id) =>
        ToDto(await places.GetByIdAsync(id) ?? throw new NotFoundException("Không tìm thấy địa điểm."));

    public async Task<PlaceAdminDto> CreateAsync(PlaceUpsertRequest r)
    {
        await ValidateAsync(r, null);
        var place = new Place { CreatedAt = DateTime.UtcNow };
        Apply(place, r);
        await places.AddAsync(place);
        await places.SaveAsync(); // lưu địa điểm trước (T6), rồi mới lưu bản dịch
        if (r.Translations is { Count: > 0 })
        {
            await ApplyTranslationsAsync(place, r.Translations);
            await places.SaveAsync();
        }
        return ToDto(place);
    }

    public async Task<PlaceAdminDto> UpdateAsync(int id, PlaceUpsertRequest r)
    {
        var place = await places.GetByIdAsync(id) ?? throw new NotFoundException("Không tìm thấy địa điểm.");
        await ValidateAsync(r, id);
        Apply(place, r);
        if (r.Translations is { Count: > 0 })
            await ApplyTranslationsAsync(place, r.Translations);
        await places.SaveAsync();
        return ToDto(place);
    }

    public async Task DeleteAsync(int id)
    {
        var place = await places.GetByIdAsync(id) ?? throw new NotFoundException("Không tìm thấy địa điểm.");
        places.Remove(place); // cascade xóa bản dịch và log
        await places.SaveAsync();
    }

    /// <summary>Ẩn/hiện địa điểm với du khách (US-22).</summary>
    public async Task SetActiveAsync(int id, bool active)
    {
        var place = await places.GetByIdAsync(id) ?? throw new NotFoundException("Không tìm thấy địa điểm.");
        place.IsActive = active;
        place.UpdatedAt = DateTime.UtcNow;
        await places.SaveAsync();
    }

    /// <summary>"Lưu tất cả bản dịch": khối thiếu tên hoặc nội dung bị bỏ qua (US-20).</summary>
    public async Task<PlaceAdminDto> SaveTranslationsAsync(int id, List<TranslationDto> translations)
    {
        var place = await places.GetByIdAsync(id) ?? throw new NotFoundException("Không tìm thấy địa điểm.");
        await ApplyTranslationsAsync(place, translations);
        place.UpdatedAt = DateTime.UtcNow;
        await places.SaveAsync();
        return ToDto(place);
    }

    // ---------------- nội bộ ----------------

    private static void Apply(Place p, PlaceUpsertRequest r)
    {
        p.Slug = r.Slug.Trim();
        p.Latitude = r.Latitude;
        p.Longitude = r.Longitude;
        p.RadiusMeters = r.RadiusMeters;
        p.Address = string.IsNullOrWhiteSpace(r.Address) ? null : r.Address.Trim();
        p.ImageUrl = string.IsNullOrWhiteSpace(r.ImageUrl) ? null : r.ImageUrl.Trim();
        p.IsActive = r.IsActive;
        p.UpdatedAt = DateTime.UtcNow;
    }

    private async Task ValidateAsync(PlaceUpsertRequest r, int? exceptId)
    {
        var slug = (r.Slug ?? "").Trim();
        if (slug.Length is 0 or > 100 || !SlugRegex().IsMatch(slug))
            throw new AppException("Slug chỉ gồm chữ thường, số và gạch ngang (tối đa 100 ký tự).");
        if (r.Latitude is < -90 or > 90 || double.IsNaN(r.Latitude))
            throw new AppException("Vĩ độ phải từ −90 đến 90.");
        if (r.Longitude is < -180 or > 180 || double.IsNaN(r.Longitude))
            throw new AppException("Kinh độ phải từ −180 đến 180.");
        if (r.RadiusMeters is < 20 or > 2000)
            throw new AppException("Bán kính kích hoạt phải từ 20 đến 2000 m.");
        if ((r.Address?.Length ?? 0) > 300) throw new AppException("Địa chỉ tối đa 300 ký tự.");
        if (!IsValidUrl(r.ImageUrl)) throw new AppException("Đường dẫn ảnh không hợp lệ.");
        if (await places.SlugExistsAsync(slug, exceptId)) throw new ConflictException("Slug đã tồn tại.");
    }

    private static bool IsValidUrl(string? url)
    {
        if (string.IsNullOrWhiteSpace(url)) return true;
        if (url.Length > 500) return false;
        return url.StartsWith('/') || url.StartsWith("http://") || url.StartsWith("https://");
    }

    private async Task ApplyTranslationsAsync(Place place, IEnumerable<TranslationDto> items)
    {
        foreach (var t in items)
        {
            var code = (t.LanguageCode ?? "").Trim();
            var name = (t.Name ?? "").Trim();
            var text = (t.NarrationText ?? "").Trim();
            if (name.Length == 0 || text.Length == 0) continue; // bỏ qua khối thiếu dữ liệu

            if (name.Length > 200) throw new AppException($"Tên ({code}) tối đa 200 ký tự.");
            if (text.Length > 4000) throw new AppException($"Thuyết minh ({code}) tối đa 4000 ký tự.");
            if ((t.AudioUrl?.Length ?? 0) > 500 || !IsValidUrl(t.AudioUrl))
                throw new AppException($"Đường dẫn âm thanh ({code}) không hợp lệ.");
            if (await languages.FindAsync(code) == null)
                throw new AppException($"Ngôn ngữ '{code}' chưa tồn tại.");

            var existing = place.Translations.FirstOrDefault(x => x.LanguageCode == code);
            if (existing == null)
            {
                existing = new PlaceTranslation { LanguageCode = code };
                place.Translations.Add(existing);
            }
            existing.Name = name;
            existing.NarrationText = text;
            existing.AudioUrl = string.IsNullOrWhiteSpace(t.AudioUrl) ? null : t.AudioUrl.Trim();
        }
    }
}
