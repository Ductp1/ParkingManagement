using AdminService.Application.Features.Sanctions;
using AdminService.Domain.Entities;
using AdminService.Domain.Enums;
using ParkingManagement.SharedKernel.Enums;
using static AdminService.Test.SanctionFixtures;

namespace AdminService.Test;

// US-097: quy tắc thuần – hạn khiếu nại 5 ngày làm việc (ICT), entity Sanction (tạo / gỡ / thay thế).
public class SanctionRulesTests
{
    private static DateTime Utc(int month, int day, int hour, int minute = 0) => new(2026, month, day, hour, minute, 0, DateTimeKind.Utc);

    [Theory]
    // Thứ Hai 02/11 09:00 ICT → T3..T7(bỏ)..: T3, T4, T5, T6, T2 → Thứ Hai 09/11 23:59:59 ICT.
    [InlineData(11, 2, 2, 0, 11, 9)]
    // Thứ Sáu 06/11 15:00 ICT → T2..T6 tuần sau → Thứ Sáu 13/11.
    [InlineData(11, 6, 8, 0, 11, 13)]
    // Thứ Sáu 06/11 23:30 ICT (16:30 UTC) vẫn là ngày Thứ Sáu theo giờ Việt Nam.
    [InlineData(11, 6, 16, 30, 11, 13)]
    // 18:00 UTC Thứ Sáu = 01:00 ICT Thứ Bảy 07/11 → đếm từ Thứ Hai → Thứ Sáu 13/11.
    [InlineData(11, 6, 18, 0, 11, 13)]
    // Chủ nhật 08/11 → Thứ Sáu 13/11.
    [InlineData(11, 8, 3, 0, 11, 13)]
    // Qua tháng: Thứ Năm 26/11 → T6 27, T2 30, T3 01, T4 02, T5 03/12.
    [InlineData(11, 26, 3, 0, 12, 3)]
    public void Appeal_deadline_is_end_of_5th_business_day_in_ict(int month, int day, int hour, int minute, int dueMonth, int dueDay)
    {
        var deadline = SanctionRules.AppealDeadlineUtc(Utc(month, day, hour, minute), 5);

        // 23:59:59 ICT = 16:59:59 UTC cùng ngày.
        Assert.Equal(new DateTime(2026, dueMonth, dueDay, 16, 59, 59, DateTimeKind.Utc), deadline);
        Assert.Equal(DateTimeKind.Utc, deadline.Kind);
    }

    [Fact]
    public void Issue_cannot_create_an_owner_lock()
    {
        var extras = new SanctionExtras(OwnerTsnUser, null, null, NowUtc);

        Assert.Throws<InvalidOperationException>(() => Sanction.Issue(OwnerTsn, null, SanctionLevel.TemporarySuspension, "r", Admin,
            NowUtc, NowUtc.AddDays(1), null, extras));
        Assert.Throws<InvalidOperationException>(() => Sanction.Issue(OwnerTsn, LotBenThanh, SanctionLevel.PermanentBan, "r", Admin,
            NowUtc, null, null, extras));
    }

    [Fact]
    public void Us096_owner_lock_without_extras_keeps_notification_not_required()
    {
        var sanction = Sanction.CreateOwnerLock(OwnerTsn, "r", Admin, NowUtc, null);

        Assert.Equal(SanctionSyncStatus.Pending, sanction.UserServiceSyncStatus);
        Assert.Equal(SanctionSyncStatus.NotRequired, sanction.NotificationSyncStatus);
        Assert.Equal(SanctionSyncStatus.NotRequired, sanction.ParkingSyncStatus);
        Assert.Null(sanction.AppealDeadlineUtc);
    }

    [Fact]
    public void Revoke_with_reason_of_us096_lock_only_queues_user_service()
    {
        var sanction = OwnerLock(1, SanctionLevel.TemporarySuspension, NowUtc.AddDays(3));

        sanction.Revoke("Mở khóa", Admin, NowUtc);

        Assert.Equal(SanctionStatus.Revoked, sanction.Status);
        Assert.Equal(SanctionSyncStatus.Pending, sanction.UserServiceSyncStatus);
        Assert.Null(sanction.UserServiceSyncedAtUtc);
        Assert.Equal(SanctionSyncStatus.NotRequired, sanction.NotificationSyncStatus);
        Assert.Equal(SanctionSyncStatus.NotRequired, sanction.ParkingSyncStatus);
    }

    [Fact]
    public void Supersede_closes_the_lock_without_anything_left_to_sync()
    {
        var sanction = OwnerLock(1, SanctionLevel.TemporarySuspension, NowUtc.AddDays(3));

        sanction.Supersede("Nâng lên cấp 4", Admin, NowUtc);

        Assert.Equal((SanctionStatus.Revoked, SanctionSyncStatus.NotRequired), (sanction.Status, sanction.UserServiceSyncStatus));
        Assert.Equal("Nâng lên cấp 4", sanction.RevokeReason);
    }

    [Fact]
    public void Risk_flag_can_only_be_resolved_once()
    {
        var flag = new RiskFlag { Status = RiskFlagStatus.Open, RuleCode = "R", Description = "d" };

        flag.ResolveWithSanction(9, NowUtc);

        Assert.Equal((9, RiskFlagStatus.Resolved), (flag.SanctionId!.Value, flag.Status));
        Assert.Throws<InvalidOperationException>(() => flag.ResolveWithSanction(10, NowUtc));
    }
}
