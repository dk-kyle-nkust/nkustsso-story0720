// 定期清理過期票券、短期驗證紀錄與稽核資料。
using LinqToDB;
using NkustSso.Server.Models;

namespace NkustSso.Server.Services;

/// <summary>
/// 定期清理票券資料：移除已到期超過一天的票券及超過 30 天的票券稽核。
/// </summary>
public sealed class ExpiredTicketCleanupService(
    DatabaseFactory database,
    TimeProvider clock,
    ILogger<ExpiredTicketCleanupService> logger) : BackgroundService
{
    private static readonly TimeSpan Interval = TimeSpan.FromMinutes(10);

    /// <summary>票券過期超過一天才刪除，留一點時間供問題追查。</summary>
    private static readonly TimeSpan TicketRetention = TimeSpan.FromDays(1);

    /// <summary>稽核紀錄保留 30 天。</summary>
    private static readonly TimeSpan AuditRetention = TimeSpan.FromDays(30);

    /// <summary>
    /// 啟動時先清理一次，再每 10 分鐘執行；服務停止時取消等待，其他錯誤留待下次重試。
    /// </summary>
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(Interval);
        try
        {
            do
            {
                await CleanOnceAsync(stoppingToken);
            }
            while (await timer.WaitForNextTickAsync(stoppingToken));
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            // 服務停止，正常結束。
        }
    }

    private async Task CleanOnceAsync(CancellationToken stoppingToken)
    {
        try
        {
            await using var db = database.OpenSessions();
            await using var configurationDb = database.Open();

            var ticketsBefore = clock.GetUtcNow().Subtract(TicketRetention).ToUnixTimeMilliseconds();
            var auditsBefore = clock.GetUtcNow().Subtract(AuditRetention).ToUnixTimeMilliseconds();

            await configurationDb.GetTable<IntegrationAudit>()
                .Where(a => a.OccurredAt < auditsBefore && a.Action == "verify")
                .DeleteAsync(stoppingToken);

            await db.LoginTickets
                .Where(t => t.ExpiresAt < ticketsBefore)
                .DeleteAsync(stoppingToken);

            await db.GetTable<IntegrationConsumedTicket>()
                .Where(t => t.ExpiresAt < ticketsBefore)
                .DeleteAsync(stoppingToken);

            await db.TicketAudits
                .Where(a => a.OccurredAt < auditsBefore)
                .DeleteAsync(stoppingToken);

            await db.GetTable<SamlLoginRequest>()
                .Where(r => r.ExpiresAt < ticketsBefore)
                .DeleteAsync(stoppingToken);

            await db.GetTable<SamlAudit>()
                .Where(a => a.OccurredAt < auditsBefore)
                .DeleteAsync(stoppingToken);

            await db.GetTable<CampusLoginAttempt>()
                .Where(a => a.UpdatedAt < ticketsBefore && a.LockedUntil < ticketsBefore)
                .DeleteAsync(stoppingToken);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            logger.LogWarning(
                "Ticket cleanup failed ({ExceptionType}); will retry at the next interval.",
                exception.GetType().Name);
        }
    }
}
