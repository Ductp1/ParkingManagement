using System.Text.Json;
using AdminService.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using ParkingManagement.ServiceDefaults.Persistence;
using ParkingManagement.SharedKernel.Contracts;
using ParkingManagement.SharedKernel.Enums;

namespace AdminService.Infrastructure.Persistence.Seeding;

/// <summary>Tham số hệ thống & feature flag đã có qua migration (HasData). Seeder thêm 1 chế tài SLA và 2 audit log demo.</summary>
public sealed class AdminDataSeeder(ILogger<AdminDataSeeder> logger) : IDataSeeder<AdminDbContext>
{
    public async Task SeedAsync(AdminDbContext db, CancellationToken cancellationToken)
    {
        await SeedCoreAsync(db, cancellationToken);
        await SeedExtrasAsync(db, cancellationToken);
    }

    /// <summary>Dữ liệu demo ban đầu (migration InitialCreate).</summary>
    private async Task SeedCoreAsync(AdminDbContext db, CancellationToken cancellationToken)
    {
        if (await db.Sanctions.AnyAsync(cancellationToken)) return;
        var now = DateTime.UtcNow;

        db.Sanctions.Add(new Sanction
        {
            OwnerProfileId = DemoIds.OwnerTsn, ParkingLotId = DemoIds.LotBenThanh, Level = SanctionLevel.TemporarySuspension,
            Reason = "Để xảy ra overbooking 3 lần trong tuần.", StartsAtUtc = now.AddDays(-2), EndsAtUtc = now.AddDays(12),
            IssuedByUserId = DemoIds.AdminUser
        });
        db.AuditLogs.AddRange(
            new AuditLog
            {
                SourceService = "ParkingService", UserId = DemoIds.AdminUser, Action = "Kyb.Approved", EntityName = "KybApplication",
                EntityId = DemoIds.LotVincom.ToString(), Reason = "Hồ sơ đầy đủ, khảo sát thực địa đạt."
            },
            new AuditLog
            {
                SourceService = "PaymentService", UserId = DemoIds.OwnerVincomUser, OwnerProfileId = DemoIds.OwnerVincom,
                Action = "RateCard.Created", EntityName = "RateCard", EntityId = DemoIds.RateCardVincom.ToString(),
                NewValuesJson = JsonSerializer.Serialize(new { firstHour = 30000, nextHours = 20000, after4h = 15000 })
            });
        await db.SaveChangesAsync(cancellationToken);
        logger.LogInformation("Seed AdminService xong: 1 chế tài, 2 audit log.");
    }

    /// <summary>Dữ liệu demo cho các bảng thêm ở migration AddDocumentCoverage.</summary>
    private static async Task SeedExtrasAsync(AdminDbContext db, CancellationToken cancellationToken)
    {
        if (await db.RiskFlags.AnyAsync(cancellationToken)) return;
        var now = DateTime.UtcNow;
        var sanctionId = await db.Sanctions.OrderBy(s => s.Id).Select(s => (int?)s.Id).FirstOrDefaultAsync(cancellationToken);

        db.RiskFlags.AddRange(
            new RiskFlag
            {
                SubjectType = RiskSubjectType.ParkingLot, SubjectId = DemoIds.LotBenThanh, RuleCode = "OVERBOOKING_REPEATED",
                Severity = RiskSeverity.Critical, Status = RiskFlagStatus.Resolved,
                Description = "Bãi Bến Thành để xảy ra overbooking 3 lần trong 7 ngày.",
                DueAtUtc = now.AddDays(-3).AddMinutes(15), ResolvedAtUtc = now.AddDays(-2), AssignedToUserId = DemoIds.AdminUser,
                ResolutionNote = "Đã áp chế tài SLA cấp 3.", SanctionId = sanctionId
            },
            new RiskFlag
            {
                SubjectType = RiskSubjectType.User, SubjectId = DemoIds.Driver2User, RuleCode = "BOOKING_ABUSE",
                Severity = RiskSeverity.Info, Status = RiskFlagStatus.Open,
                Description = "Tạo 4 booking rồi để hết hạn giữ chỗ trong 1 giờ.",
                EvidenceJson = "{\"expiredHolds\":4,\"windowMinutes\":60}", DueAtUtc = now.AddHours(24)
            },
            new RiskFlag
            {
                SubjectType = RiskSubjectType.ParkingLot, SubjectId = DemoIds.LotTsn, RuleCode = "LEAKAGE_SUSPECTED",
                Severity = RiskSeverity.Warning, Status = RiskFlagStatus.Investigating,
                Description = "Số lượt qua barie cao hơn 35% so với số booking + vãng lai được ghi nhận trong tuần.",
                EvidenceJson = "{\"barrierPasses\":412,\"recordedSessions\":305}", DueAtUtc = now.AddHours(4), AssignedToUserId = DemoIds.AdminUser
            });
        await db.SaveChangesAsync(cancellationToken);
    }
}
