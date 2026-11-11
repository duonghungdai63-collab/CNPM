using DaLat.BLL.Services;

namespace DaLat.API.Services;

/// <summary>Tác vụ nền: dọn phiên truy cập quá 90 ngày (US-30).</summary>
public class VisitCleanupService(IServiceScopeFactory scopes, ILogger<VisitCleanupService> log) : BackgroundService
{
    private static readonly TimeSpan Interval = TimeSpan.FromHours(6);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = scopes.CreateScope();
                var deleted = await scope.ServiceProvider.GetRequiredService<VisitorService>().CleanupOldSessionsAsync();
                if (deleted > 0) log.LogInformation("Đã dọn {Count} phiên truy cập cũ.", deleted);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                log.LogWarning(ex, "Dọn phiên truy cập thất bại.");
            }

            try { await Task.Delay(Interval, stoppingToken); }
            catch (OperationCanceledException) { break; }
        }
    }
}
