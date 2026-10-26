using DaLat.BLL.Common;

namespace DaLat.BLL.Services;

/// <summary>Cấp JWT; cài đặt nằm ở lớp API để BLL không phụ thuộc thư viện token.</summary>
public interface IJwtTokenIssuer
{
    (string Token, DateTime ExpiresAt) Issue(int userId, string username, string displayName);
}

/// <summary>Dịch tự động tiếng Việt → ngôn ngữ đích; chỉ trả bản nháp, không ghi CSDL (BR16).</summary>
public interface ITranslationService
{
    Task<TranslationDto> TranslateAsync(AutoTranslateRequest request, CancellationToken ct);
}
