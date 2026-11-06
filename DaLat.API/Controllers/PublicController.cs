using DaLat.BLL.Common;
using DaLat.BLL.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace DaLat.API.Controllers;

/// <summary>API công khai cho du khách.</summary>
[ApiController]
[Route("api")]
public class PublicController(LanguageService languages, PlaceService places, VisitorService visitors) : ControllerBase
{
    [HttpGet("languages")]
    public async Task<IActionResult> GetLanguages() => Ok(await languages.GetActiveAsync());

    [HttpGet("places")]
    public async Task<IActionResult> GetPlaces([FromQuery] string? lang) => Ok(await places.GetPlacesAsync(lang));

    [HttpGet("places/nearby")]
    public async Task<IActionResult> GetNearby([FromQuery] double lat, [FromQuery] double lng, [FromQuery] string? lang)
    {
        if (lat is < -90 or > 90 || lng is < -180 or > 180)
            throw new AppException("Tọa độ không hợp lệ.");
        return Ok(await places.GetPlacesAsync(lang, lat, lng));
    }

    [HttpGet("places/{slug}")]
    public async Task<IActionResult> GetPlace(string slug, [FromQuery] string? lang) =>
        Ok(await places.GetBySlugAsync(slug, lang));

    [HttpPost("playlogs")]
    [EnableRateLimiting("playlog")]
    public async Task<IActionResult> LogPlay([FromBody] PlayLogRequest request)
    {
        await visitors.LogPlayAsync(request);
        return NoContent();
    }

    [HttpPost("visits/ping")]
    [EnableRateLimiting("ping")]
    public async Task<IActionResult> Ping([FromBody] PingRequest request)
    {
        await visitors.PingAsync(request,
            HttpContext.Connection.RemoteIpAddress?.ToString(),
            Request.Headers.UserAgent.ToString());
        return NoContent();
    }
}
