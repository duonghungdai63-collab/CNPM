using DaLat.BLL.Common;

namespace DaLat.API.Middleware;

/// <summary>Chuyển AppException thành JSON { message } với mã HTTP tương ứng.</summary>
public class ExceptionHandlingMiddleware(RequestDelegate next, ILogger<ExceptionHandlingMiddleware> log)
{
    public async Task InvokeAsync(HttpContext ctx)
    {
        try { await next(ctx); }
        catch (OperationCanceledException) when (ctx.RequestAborted.IsCancellationRequested) { /* client đã hủy */ }
        catch (AppException ex) { await Write(ctx, ex.StatusCode, ex.Message); }
        catch (Exception ex)
        {
            log.LogError(ex, "Lỗi không xử lý được: {Path}", ctx.Request.Path);
            await Write(ctx, 500, "Đã xảy ra lỗi hệ thống. Vui lòng thử lại.");
        }
    }

    private static async Task Write(HttpContext ctx, int status, string message)
    {
        if (ctx.Response.HasStarted) return;
        ctx.Response.Clear();
        ctx.Response.StatusCode = status;
        await ctx.Response.WriteAsJsonAsync(new { message });
    }
}

/// <summary>
/// US-33: yêu cầu đi qua Cloudflare Tunnel tới /admin và /api/admin bị trả 404.
/// Phải đặt TRƯỚC UseForwardedHeaders để còn thấy các header gốc của tunnel.
/// </summary>
public class AdminLocalOnlyMiddleware(RequestDelegate next, IConfiguration config)
{
    private readonly bool _enabled = config.GetValue("AdminLocalOnly", true);

    public Task InvokeAsync(HttpContext ctx)
    {
        if (_enabled && IsAdminPath(ctx.Request.Path) && ViaTunnel(ctx.Request))
        {
            ctx.Response.StatusCode = StatusCodes.Status404NotFound;
            return Task.CompletedTask;
        }
        return next(ctx);
    }

    private static bool IsAdminPath(PathString p) =>
        p.StartsWithSegments("/admin", StringComparison.OrdinalIgnoreCase) ||
        p.StartsWithSegments("/api/admin", StringComparison.OrdinalIgnoreCase);

    private static bool ViaTunnel(HttpRequest r) =>
        r.Headers.ContainsKey("CF-Connecting-IP") || r.Headers.ContainsKey("CF-Ray") ||
        r.Headers.ContainsKey("X-Forwarded-For");
}
