using DaLat.BLL.Common;
using DaLat.BLL.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace DaLat.API.Controllers;

[ApiController]
[Route("api/admin/auth")]
public class AuthController(AuthService auth) : ControllerBase
{
    [HttpPost("login")]
    [AllowAnonymous]
    [EnableRateLimiting("login")]
    public async Task<IActionResult> Login([FromBody] LoginRequest request) => Ok(await auth.LoginAsync(request));

    [HttpPost("change-password")]
    [Authorize]
    public async Task<IActionResult> ChangePassword([FromBody] ChangePasswordRequest request)
    {
        var id = int.TryParse(User.FindFirst("sub")?.Value, out var v) ? v : throw new AppException("Token không hợp lệ.", 401);
        await auth.ChangePasswordAsync(id, request);
        return NoContent();
    }
}

[ApiController]
[Authorize]
[Route("api/admin/places")]
public class AdminPlacesController(PlaceAdminService places, ITranslationService translator) : ControllerBase
{
    [HttpGet] public async Task<IActionResult> List() => Ok(await places.ListAsync());

    [HttpGet("{id:int}")] public async Task<IActionResult> Get(int id) => Ok(await places.GetAsync(id));

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] PlaceUpsertRequest request)
    {
        var created = await places.CreateAsync(request);
        return CreatedAtAction(nameof(Get), new { id = created.Id }, created);
    }

    [HttpPut("{id:int}")]
    public async Task<IActionResult> Update(int id, [FromBody] PlaceUpsertRequest request) =>
        Ok(await places.UpdateAsync(id, request));

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int id)
    {
        await places.DeleteAsync(id);
        return NoContent();
    }

    [HttpPatch("{id:int}/active")]
    public async Task<IActionResult> SetActive(int id, [FromBody] SetActiveRequest request)
    {
        await places.SetActiveAsync(id, request.IsActive);
        return NoContent();
    }

    /// <summary>"Lưu tất cả bản dịch": bỏ qua khối thiếu tên hoặc nội dung.</summary>
    [HttpPut("{id:int}/translations")]
    public async Task<IActionResult> SaveTranslations(int id, [FromBody] List<TranslationDto> translations) =>
        Ok(await places.SaveTranslationsAsync(id, translations));

    /// <summary>Dịch tự động tiếng Việt → ngôn ngữ đích, trả bản nháp (không ghi CSDL).</summary>
    [HttpPost("auto-translate")]
    [EnableRateLimiting("translate")]
    public async Task<IActionResult> AutoTranslate([FromBody] AutoTranslateRequest request) =>
        Ok(await translator.TranslateAsync(request, HttpContext.RequestAborted));
}

[ApiController]
[Authorize]
[Route("api/admin/languages")]
public class AdminLanguagesController(LanguageService languages) : ControllerBase
{
    [HttpGet] public async Task<IActionResult> List() => Ok(await languages.GetAllAsync());

    [HttpPost]
    public async Task<IActionResult> Upsert([FromBody] LanguageUpsertRequest request) =>
        Ok(await languages.UpsertAsync(request));

    [HttpPatch("{code}/active")]
    public async Task<IActionResult> SetActive(string code, [FromBody] SetActiveRequest request)
    {
        await languages.SetActiveAsync(code, request.IsActive);
        return NoContent();
    }
}

[ApiController]
[Authorize]
[Route("api/admin/visitors")]
public class AdminVisitorsController(VisitorService visitors) : ControllerBase
{
    [HttpGet("online")] public async Task<IActionResult> Online() => Ok(await visitors.GetOnlineAsync());
}

[ApiController]
[Authorize]
[Route("api/admin/stats")]
public class AdminStatsController(StatsService stats) : ControllerBase
{
    [HttpGet("visits")]
    public async Task<IActionResult> Visits([FromQuery] DateOnly? from, [FromQuery] DateOnly? to) =>
        Ok(await stats.GetVisitStatsAsync(from, to));

    [HttpGet("plays")]
    public async Task<IActionResult> Plays([FromQuery] DateOnly? from, [FromQuery] DateOnly? to) =>
        Ok(await stats.GetPlayStatsAsync(from, to));
}

[ApiController]
[Authorize]
[Route("api/admin/uploads")]
public class AdminUploadsController(UploadSettings settings) : ControllerBase
{
    private const long MaxImageBytes = 4 * 1024 * 1024;
    private const long MaxAudioBytes = 20 * 1024 * 1024;
    private static readonly string[] ImageExt = [".jpg", ".jpeg", ".png", ".webp"];
    private static readonly string[] AudioExt = [".mp3", ".wav", ".ogg", ".m4a"];

    /// <summary>Tải ảnh (jpg/png/webp ≤ 4 MB) hoặc âm thanh (mp3/wav/ogg/m4a); trả về /uploads/...</summary>
    [HttpPost]
    [RequestSizeLimit(25 * 1024 * 1024)]
    public async Task<IActionResult> Upload(IFormFile file, [FromQuery] string type = "image")
    {
        if (file == null || file.Length == 0) throw new AppException("Chưa chọn file.");
        var ext = Path.GetExtension(file.FileName).ToLowerInvariant();
        var isImage = type.Equals("image", StringComparison.OrdinalIgnoreCase);

        if (isImage)
        {
            if (!ImageExt.Contains(ext)) throw new AppException("Ảnh phải là jpg, png hoặc webp.");
            if (file.Length > MaxImageBytes) throw new AppException("Ảnh tối đa 4 MB.");
        }
        else if (type.Equals("audio", StringComparison.OrdinalIgnoreCase))
        {
            if (!AudioExt.Contains(ext)) throw new AppException("Âm thanh phải là mp3, wav, ogg hoặc m4a.");
            if (file.Length > MaxAudioBytes) throw new AppException("Âm thanh tối đa 20 MB.");
        }
        else throw new AppException("type phải là 'image' hoặc 'audio'.");

        if (isImage && !await LooksLikeImageAsync(file)) throw new AppException("Nội dung file không phải ảnh hợp lệ.");

        Directory.CreateDirectory(settings.Path);
        var name = $"{Guid.NewGuid():N}{ext}"; // tên ngẫu nhiên, tránh path traversal
        await using (var fs = System.IO.File.Create(Path.Combine(settings.Path, name)))
            await file.CopyToAsync(fs);

        return Ok(new { url = $"/uploads/{name}" });
    }

    private static async Task<bool> LooksLikeImageAsync(IFormFile file)
    {
        var head = new byte[12];
        await using var s = file.OpenReadStream();
        var read = await s.ReadAsync(head);
        if (read < 12) return false;
        var jpeg = head[0] == 0xFF && head[1] == 0xD8 && head[2] == 0xFF;
        var png = head[0] == 0x89 && head[1] == 0x50 && head[2] == 0x4E && head[3] == 0x47;
        var webp = head[0] == 'R' && head[1] == 'I' && head[2] == 'F' && head[3] == 'F'
                   && head[8] == 'W' && head[9] == 'E' && head[10] == 'B' && head[11] == 'P';
        return jpeg || png || webp;
    }
}
