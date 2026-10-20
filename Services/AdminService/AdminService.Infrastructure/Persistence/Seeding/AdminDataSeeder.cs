using System.Text.Json;
using AdminService.Domain.Entities;
using AdminService.Domain.Enums;
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
        await SeedContractTemplateAsync(db, cancellationToken);
        await SeedPartnerOnboardingAsync(db, cancellationToken);
    }

    /// <summary>
    /// US-116 (dev): bảo đảm mẫu hợp đồng bãi v3 (theo bản mẫu HD-SP-2026-052) tồn tại và đang áp dụng.
    /// Mẫu cũ (v1, v2) thôi áp dụng trong cùng SaveChanges – hợp đồng đã sinh vẫn giữ TemplateVersion cũ.
    /// </summary>
    private static async Task SeedContractTemplateAsync(AdminDbContext db, CancellationToken cancellationToken)
    {
        if (await db.ContractTemplates.AnyAsync(t => t.Version == PartnerContractTemplates.V3Version, cancellationToken)) return;

        foreach (var active in await db.ContractTemplates.Where(t => t.IsActive).ToListAsync(cancellationToken))
            active.IsActive = false;
        db.ContractTemplates.Add(new ContractTemplate
        {
            Version = PartnerContractTemplates.V3Version,
            Name = PartnerContractTemplates.V3Name,
            Body = PartnerContractTemplates.V3Body,
            EffectiveFromUtc = DateTime.UtcNow,
            IsActive = true,
        });
        await db.SaveChangesAsync(cancellationToken);
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

    /// <summary>
    /// US-116 (dev): 9 hồ sơ đối tác đi đúng đường chuyển trạng thái của domain. Hai chủ bãi demo của UserService (Vincom, Tân Sơn Nhất)
    /// có hồ sơ Activated kèm tài khoản nhận tiền khớp UserService seed, để lập được hợp đồng cho các bãi của họ.
    /// Database đã seed trước đó: chỉ bổ sung phần còn thiếu của hai chủ bãi demo (SeedDemoOwnerPartnersAsync).
    /// </summary>
    private static async Task SeedPartnerOnboardingAsync(AdminDbContext db, CancellationToken cancellationToken)
    {
        if (await db.PartnerApplications.AnyAsync(cancellationToken))
        {
            await SeedDemoOwnerPartnersAsync(db, cancellationToken);
            return;
        }
        var now = DateTime.UtcNow;

        var submitted = DemoApplication(1, "Công ty TNHH Bãi xe Bình Dương", "3702345671", "Trương Văn Phúc", "phuc.truong@bdparking.vn",
            "0912311223", "15 Đại lộ Bình Dương, Thủ Dầu Một", 2, now.AddDays(-1));

        var underReview = DemoApplication(2, "Công ty CP Bãi xe Thủ Đức", "0316789012", "Nguyễn Thị Lan", "lan.nguyen@thuducparking.vn",
            "0903456789", "216 Võ Văn Ngân, TP. Thủ Đức", 2, now.AddDays(-10));
        underReview.MoveTo(PartnerApplicationStatus.UnderReview);
        underReview.ReviewerAdminId = DemoIds.AdminUser;

        var needsInfo = DemoApplication(3, "Công ty TNHH Đỗ xe Hà Đông", "0109876543", "Lê Minh Tuấn", "tuan.le@hadongparking.vn",
            "0987654321", "88 Quang Trung, Hà Đông, Hà Nội", 1, now.AddDays(-12));
        needsInfo.MoveTo(PartnerApplicationStatus.UnderReview);
        needsInfo.MoveTo(PartnerApplicationStatus.NeedsMoreInfo);
        needsInfo.ReviewerAdminId = DemoIds.AdminUser;
        needsInfo.InfoRequestNote = "Bổ sung bản sao giấy phép kinh doanh có dấu đỏ.";

        var underReview2 = DemoApplication(4, "Công ty TNHH Bãi đỗ xe Gò Vấp", "0314567890", "Phạm Quốc Lâm", "lam.pham@govapparking.vn",
            "0938111222", "12 Quang Trung, Gò Vấp", 1, now.AddDays(-15));
        underReview2.MoveTo(PartnerApplicationStatus.UnderReview);
        underReview2.ReviewerAdminId = DemoIds.AdminUser;

        // Đã cấp tài khoản nhưng chưa gắn tài khoản UserService thật (U2 chưa có) – dùng để xem màn hình chờ kích hoạt.
        var accountIssued = DemoApplication(5, "Công ty TNHH Bãi xe Tân Bình", "0312233445", "Đỗ Thị Hạnh", "hanh.do@tanbinhparking.vn",
            "0909222333", "45 Cộng Hòa, Tân Bình", 1, now.AddDays(-20));
        accountIssued.MoveTo(PartnerApplicationStatus.UnderReview);
        accountIssued.MoveTo(PartnerApplicationStatus.AccountIssued);
        accountIssued.ReviewerAdminId = DemoIds.AdminUser;
        SetPayoutBank(accountIssued, "970436", "Vietcombank", "0071000452100", "CONG TY TNHH BAI XE TAN BINH");

        var rejected = DemoApplication(7, "Công ty TNHH Bãi xe Long Biên", "0105556667", "Hoàng Văn Nam", "nam.hoang@longbienparking.vn",
            "0977000111", "1 Nguyễn Văn Cừ, Long Biên, Hà Nội", 1, now.AddDays(-25));
        rejected.MoveTo(PartnerApplicationStatus.UnderReview);
        rejected.MoveTo(PartnerApplicationStatus.Rejected);
        rejected.ReviewerAdminId = DemoIds.AdminUser;
        rejected.RejectReason = "Giấy phép kinh doanh không có ngành nghề kinh doanh bãi đỗ xe.";

        var cancelled = DemoApplication(8, "Công ty TNHH Đỗ xe Quận 7", "0318889990", "Vũ Thị Hương", "huong.vu@q7parking.vn",
            "0911555666", "99 Nguyễn Thị Thập, Quận 7", 1, now.AddDays(-18));
        cancelled.MoveTo(PartnerApplicationStatus.UnderReview);
        cancelled.MoveTo(PartnerApplicationStatus.Cancelled);
        cancelled.ReviewerAdminId = DemoIds.AdminUser;
        cancelled.CancelReason = "Đối tác xin rút hồ sơ.";

        db.PartnerApplications.AddRange(submitted, underReview, needsInfo, underReview2, accountIssued, rejected, cancelled);
        await db.SaveChangesAsync(cancellationToken);
        await SeedDemoOwnerPartnersAsync(db, cancellationToken);
    }

    /// <summary>
    /// Hồ sơ Activated của 2 chủ bãi demo (Vincom = PA-2026-0006, Tân Sơn Nhất = PA-2026-0009), tài khoản nhận tiền khớp UserService seed.
    /// Idempotent: thiếu thì thêm, có rồi mà chưa có tài khoản nhận tiền thì bổ sung.
    /// </summary>
    private static async Task SeedDemoOwnerPartnersAsync(AdminDbContext db, CancellationToken cancellationToken)
    {
        var now = DateTime.UtcNow;
        (int Seq, string Business, string TaxCode, string Person, string Email, string Phone, string Address, int Lots, int UserId,
            int OwnerProfileId, string Bin, string Bank, string Account, string Holder)[] owners =
        [
            (6, "Công ty TNHH Vincom Parking", "0312345678", "Công ty Vincom Parking", "owner.vincom@smartparking.vn", "0900000002",
                "72 Lê Thánh Tôn, Q.1, TP.HCM", 2, DemoIds.OwnerVincomUser, DemoIds.OwnerVincom, "970436", "Vietcombank",
                "0011000000001", "CONG TY TNHH VINCOM PARKING"),
            (9, "Công ty CP Bãi xe Tân Sơn Nhất", "0398765432", "Công ty Bãi xe Tân Sơn Nhất", "owner.tsn@smartparking.vn", "0900000003",
                "Trường Sơn, Q.Tân Bình, TP.HCM", 2, DemoIds.OwnerTsnUser, DemoIds.OwnerTsn, "970415", "VietinBank",
                "1010000000002", "CONG TY CP BAI XE TAN SON NHAT"),
        ];

        foreach (var o in owners)
        {
            var code = $"PA-2026-{o.Seq:0000}";
            var app = await db.PartnerApplications.FirstOrDefaultAsync(a => a.Code == code, cancellationToken);
            if (app is null)
            {
                app = DemoApplication(o.Seq, o.Business, o.TaxCode, o.Person, o.Email, o.Phone, o.Address, o.Lots, now.AddDays(-60));
                app.MoveTo(PartnerApplicationStatus.UnderReview);
                app.MoveTo(PartnerApplicationStatus.AccountIssued);
                app.MoveTo(PartnerApplicationStatus.Activated);
                app.ReviewerAdminId = DemoIds.AdminUser;
                db.PartnerApplications.Add(app);
            }
            app.CreatedUserId ??= o.UserId;
            app.CreatedOwnerProfileId ??= o.OwnerProfileId;
            if (app.PayoutBankBin is null) SetPayoutBank(app, o.Bin, o.Bank, o.Account, o.Holder);
        }
        await db.SaveChangesAsync(cancellationToken);
    }

    private static PartnerApplication DemoApplication(int seq, string business, string taxCode, string person, string email, string phone,
        string address, int lotCount, DateTime consentAtUtc)
    {
        var app = PartnerApplication.Submit($"PA-2026-{seq:0000}", consentAtUtc);
        app.FullName = person; app.Email = email; app.Phone = phone; app.Position = "Giám đốc";
        app.BusinessType = PartnerBusinessType.Company; app.BusinessName = business; app.TaxCode = taxCode;
        app.BusinessAddress = address; app.LegalRepresentativeName = person; app.LegalRepresentativePosition = "Giám đốc";
        app.BusinessLicenseUrl = $"/uploads/partner/{seq:0000}/giay-phep-kinh-doanh.pdf";
        app.LotCount = lotCount; app.PrimaryLotAddress = address; app.EstimatedCarSlots = 120 * lotCount;
        app.EstimatedMotorbikeSlots = 300 * lotCount; app.OperatingHours = "06:00 – 23:00";
        return app;
    }

    private static void SetPayoutBank(PartnerApplication app, string bin, string bank, string account, string holder)
    {
        app.PayoutBankBin = bin; app.PayoutBankName = bank; app.PayoutBankAccountNumber = account; app.PayoutBankAccountName = holder;
    }
}
