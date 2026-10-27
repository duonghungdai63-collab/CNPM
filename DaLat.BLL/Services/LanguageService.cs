using System.Text.RegularExpressions;
using DaLat.BLL.Common;
using DaLat.DAL.Entities;
using DaLat.DAL.Repositories;

namespace DaLat.BLL.Services;

public partial class LanguageService(ILanguageRepository repo)
{
    [GeneratedRegex("^[a-z]{2,3}(-[A-Za-z0-9]{1,6})?$")]
    private static partial Regex CodeRegex();

    private static LanguageDto ToDto(Language l) =>
        new(l.Code, l.Name, l.NativeName, l.SpeechLocale, l.IsActive, l.SortOrder);

    public async Task<List<LanguageDto>> GetActiveAsync() => (await repo.GetActiveAsync()).Select(ToDto).ToList();
    public async Task<List<LanguageDto>> GetAllAsync() => (await repo.GetAllAsync()).Select(ToDto).ToList();

    /// <summary>Thêm mới hoặc cập nhật ngôn ngữ (US-23).</summary>
    public async Task<LanguageDto> UpsertAsync(LanguageUpsertRequest r)
    {
        var code = (r.Code ?? "").Trim();
        if (!CodeRegex().IsMatch(code)) throw new AppException("Mã ngôn ngữ không hợp lệ (ví dụ: vi, en, zh-TW).");
        if (string.IsNullOrWhiteSpace(r.Name) || string.IsNullOrWhiteSpace(r.NativeName))
            throw new AppException("Tên ngôn ngữ không được để trống.");
        if (r.Name.Length > 100 || r.NativeName.Length > 100 || (r.SpeechLocale?.Length ?? 0) > 20)
            throw new AppException("Tên hoặc mã giọng đọc quá dài.");

        var lang = await repo.FindAsync(code);
        if (lang == null)
        {
            lang = new Language { Code = code };
            await repo.AddAsync(lang);
        }
        lang.Name = r.Name.Trim();
        lang.NativeName = r.NativeName.Trim();
        lang.SpeechLocale = string.IsNullOrWhiteSpace(r.SpeechLocale) ? code : r.SpeechLocale.Trim();
        lang.IsActive = r.IsActive;
        lang.SortOrder = r.SortOrder;
        await repo.SaveAsync();
        return ToDto(lang);
    }

    /// <summary>Bật/tắt hiển thị; không xóa dữ liệu (BR7).</summary>
    public async Task SetActiveAsync(string code, bool active)
    {
        var lang = await repo.FindAsync(code) ?? throw new NotFoundException("Không tìm thấy ngôn ngữ.");
        lang.IsActive = active;
        await repo.SaveAsync();
    }
}
