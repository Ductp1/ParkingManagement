using System.Reflection;
using ParkingManagement.SharedKernel.Domain;
using ParkingManagement.SharedKernel.Enums;
using ParkingManagement.SharedKernel.Exceptions;
using ParkingService.Application.Abstractions;
using ParkingService.Application.Features.PhysicalChanges;
using ParkingService.Domain.Entities;

namespace ParkingService.Test;

public class LotPhysicalChangeTests
{
    private static T WithId<T>(T entity, int id) where T : BaseEntity
    {
        typeof(BaseEntity).GetProperty(nameof(BaseEntity.Id), BindingFlags.Public | BindingFlags.Instance)!
            .SetValue(entity, id);
        return entity;
    }

    private static ParkingLot CreateTestLot(int id = 1, int ownerProfileId = 1)
    {
        return new ParkingLot(
            id: id,
            name: "Bãi xe Trung tâm",
            address: "123 Lê Lợi, Q.1",
            latitude: 10.77,
            longitude: 106.70,
            totalSlots: 50,
            availableSlots: 50,
            maxHeightCm: 220,
            openTime: new TimeOnly(6, 0),
            closeTime: new TimeOnly(22, 0),
            status: ParkingLotStatus.Active,
            ownerProfileId: ownerProfileId);
    }

    private sealed class FakeLotPhysicalChangeRepo : ILotPhysicalChangeRepository
    {
        public List<ParkingLot> Lots { get; } = [];
        public List<LotPhysicalChange> Changes { get; } = [];
        private int _changeIdCounter = 1;

        public Task<LotPhysicalChange?> GetByIdAsync(int id, CancellationToken cancellationToken = default)
        {
            var item = Changes.FirstOrDefault(c => c.Id == id);
            return Task.FromResult(item);
        }

        public Task<IReadOnlyList<LotPhysicalChange>> GetByLotIdAsync(int parkingLotId, CancellationToken cancellationToken = default)
        {
            var list = Changes.Where(c => c.ParkingLotId == parkingLotId)
                .OrderByDescending(c => c.OccurredAtUtc)
                .ThenByDescending(c => c.Id)
                .ToList();
            return Task.FromResult<IReadOnlyList<LotPhysicalChange>>(list);
        }

        public Task<IReadOnlyList<LotPhysicalChange>> GetActiveByLotIdAsync(int parkingLotId, CancellationToken cancellationToken = default)
        {
            var list = Changes.Where(c => c.ParkingLotId == parkingLotId &&
                (c.Status == PhysicalChangeStatus.InProgress || c.Status == PhysicalChangeStatus.Overdue))
                .OrderByDescending(c => c.OccurredAtUtc)
                .ToList();
            return Task.FromResult<IReadOnlyList<LotPhysicalChange>>(list);
        }

        public Task<IReadOnlyList<LotPhysicalChange>> GetOverdueCandidatesAsync(DateTime nowUtc, CancellationToken cancellationToken = default)
        {
            var list = Changes.Where(c => c.Status == PhysicalChangeStatus.InProgress && c.DeadlineUtc < nowUtc).ToList();
            return Task.FromResult<IReadOnlyList<LotPhysicalChange>>(list);
        }

        public Task AddAsync(LotPhysicalChange change, CancellationToken cancellationToken = default)
        {
            if (change.Id == 0)
            {
                _changeIdCounter = Math.Max(_changeIdCounter, (Changes.Count > 0 ? Changes.Max(c => c.Id) : 0) + 1);
                WithId(change, _changeIdCounter++);
            }
            Changes.Add(change);
            return Task.CompletedTask;
        }

        public Task UpdateAsync(LotPhysicalChange change, CancellationToken cancellationToken = default)
        {
            var idx = Changes.FindIndex(c => c.Id == change.Id);
            if (idx >= 0) Changes[idx] = change;
            return Task.CompletedTask;
        }

        public Task<ParkingLot?> GetLotByIdAsync(int parkingLotId, CancellationToken cancellationToken = default)
        {
            var lot = Lots.FirstOrDefault(l => l.Id == parkingLotId && !l.IsDeleted);
            return Task.FromResult(lot);
        }

        public Task SaveChangesAsync(CancellationToken cancellationToken = default)
            => Task.CompletedTask;
    }

    // ==================== TIÊU CHÍ 1: KHAI BÁO, ĐẾM NGƯỢC 48H & NHẮC NHỞ ====================

    [Fact]
    public async Task DeclarePhysicalChange_Starts48hCountdown_WithAffectedSlots()
    {
        var repo = new FakeLotPhysicalChangeRepo();
        var lot = CreateTestLot(id: 1, ownerProfileId: 5);
        repo.Lots.Add(lot);

        var useCase = new DeclarePhysicalChangeUseCase(repo);
        var request = new DeclarePhysicalChangeRequest(
            Scope: "Floor",
            TargetId: 10,
            AffectedSlotCodes: ["A-01", "a-02", "A-01"], // Duplicate / case test
            ChangeType: "LayoutRenovation",
            Title: "Sơn lại ô đỗ và làm trạm sạc xe điện",
            Description: "Chuyển 2 ô đỗ xe A-01, A-02 sang khu vực sạc EV");

        var result = await useCase.ExecuteAsync(1, request, ownerProfileId: 5);

        Assert.NotNull(result);
        Assert.Equal(1, result.ParkingLotId);
        Assert.Equal("Floor", result.Scope);
        Assert.Equal(10, result.TargetId);
        Assert.Equal("LayoutRenovation", result.ChangeType);
        Assert.Equal("Sơn lại ô đỗ và làm trạm sạc xe điện", result.Title);
        Assert.Equal(2, result.AffectedSlotCodes.Count);
        Assert.Contains("A-01", result.AffectedSlotCodes);
        Assert.Contains("A-02", result.AffectedSlotCodes);
        Assert.Equal("InProgress", result.Status);
        Assert.False(result.IsOverdue);
        Assert.True(result.RemainingHours > 47.0 && result.RemainingHours <= 48.0);
        Assert.Equal(result.OccurredAtUtc.AddHours(48), result.DeadlineUtc);
        Assert.Single(repo.Changes);
    }

    [Fact]
    public async Task DeclarePhysicalChange_ThrowsValidation_WhenTitleEmpty()
    {
        var repo = new FakeLotPhysicalChangeRepo();
        var lot = CreateTestLot(id: 1, ownerProfileId: 5);
        repo.Lots.Add(lot);

        var useCase = new DeclarePhysicalChangeUseCase(repo);
        var request = new DeclarePhysicalChangeRequest("Floor", 1, null, "LayoutRenovation", "   ", null);

        await Assert.ThrowsAsync<ValidationException>(() =>
            useCase.ExecuteAsync(1, request, ownerProfileId: 5));
    }

    [Fact]
    public async Task DeclarePhysicalChange_ThrowsForbidden_WhenNotOwner()
    {
        var repo = new FakeLotPhysicalChangeRepo();
        var lot = CreateTestLot(id: 1, ownerProfileId: 5);
        repo.Lots.Add(lot);

        var useCase = new DeclarePhysicalChangeUseCase(repo);
        var request = new DeclarePhysicalChangeRequest("Floor", 1, null, "LayoutRenovation", "Sửa chữa", null);

        await Assert.ThrowsAsync<ForbiddenException>(() =>
            useCase.ExecuteAsync(1, request, ownerProfileId: 99)); // Wrong owner
    }

    [Fact]
    public async Task DeclarePhysicalChange_ThrowsNotFound_WhenLotNotFound()
    {
        var repo = new FakeLotPhysicalChangeRepo();
        var useCase = new DeclarePhysicalChangeUseCase(repo);
        var request = new DeclarePhysicalChangeRequest("Floor", 1, null, "LayoutRenovation", "Sửa chữa", null);

        await Assert.ThrowsAsync<NotFoundException>(() =>
            useCase.ExecuteAsync(999, request, ownerProfileId: 5));
    }

    [Fact]
    public async Task GetLotPhysicalChanges_ReturnsList_AndAutoMarksOverdue()
    {
        var repo = new FakeLotPhysicalChangeRepo();
        var lot = CreateTestLot(id: 1);
        repo.Lots.Add(lot);

        var now = DateTime.UtcNow;
        // Đợt 1: còn trong hạn
        var c1 = WithId(new LotPhysicalChange
        {
            ParkingLotId = 1,
            Title = "Đang sửa",
            OccurredAtUtc = now.AddHours(-10),
            DeadlineUtc = now.AddHours(-10).AddHours(48), // Còn 38h
            Status = PhysicalChangeStatus.InProgress
        }, 1);

        // Đợt 2: đã quá hạn 48h
        var c2 = WithId(new LotPhysicalChange
        {
            ParkingLotId = 1,
            Title = "Đã quá hạn 48h",
            OccurredAtUtc = now.AddHours(-50),
            DeadlineUtc = now.AddHours(-50).AddHours(48), // Quá hạn 2h
            Status = PhysicalChangeStatus.InProgress
        }, 2);

        repo.Changes.Add(c1);
        repo.Changes.Add(c2);

        var useCase = new GetLotPhysicalChangesUseCase(repo);
        var result = await useCase.ExecuteAsync(1);

        Assert.Equal(2, result.Count);
        var item1 = result.First(r => r.Id == 1);
        var item2 = result.First(r => r.Id == 2);

        Assert.False(item1.IsOverdue);
        Assert.Equal("InProgress", item1.Status);
        Assert.True(item1.RemainingHours > 37.0);

        Assert.True(item2.IsOverdue);
        Assert.Equal("Overdue", item2.Status); // Tự động đánh dấu Overdue
        Assert.Equal(0, item2.RemainingHours);
    }

    [Fact]
    public async Task GetPhysicalChangeById_ReturnsDetail_WhenExists()
    {
        var repo = new FakeLotPhysicalChangeRepo();
        var lot = CreateTestLot(id: 1);
        repo.Lots.Add(lot);

        var change = WithId(new LotPhysicalChange
        {
            ParkingLotId = 1,
            Title = "Sửa hầm",
            OccurredAtUtc = DateTime.UtcNow.AddHours(-5),
            DeadlineUtc = DateTime.UtcNow.AddHours(43),
            Status = PhysicalChangeStatus.InProgress
        }, 10);
        repo.Changes.Add(change);

        var useCase = new GetPhysicalChangeByIdUseCase(repo);
        var result = await useCase.ExecuteAsync(1, 10);

        Assert.NotNull(result);
        Assert.Equal(10, result.Id);
        Assert.Equal("Sửa hầm", result.Title);
    }

    [Fact]
    public async Task SendReminder_IncrementsReminderCount_AndReturnsMessage()
    {
        var repo = new FakeLotPhysicalChangeRepo();
        var lot = CreateTestLot(id: 1, ownerProfileId: 5);
        repo.Lots.Add(lot);

        var change = WithId(new LotPhysicalChange
        {
            ParkingLotId = 1,
            Scope = PhysicalChangeScope.Floor,
            Title = "Sửa sàn tầng 2",
            OccurredAtUtc = DateTime.UtcNow.AddHours(-10),
            DeadlineUtc = DateTime.UtcNow.AddHours(38),
            Status = PhysicalChangeStatus.InProgress,
            ReminderCount = 0
        }, 1);
        repo.Changes.Add(change);

        var useCase = new SendPhysicalChangeReminderUseCase(repo);
        var result = await useCase.ExecuteAsync(1, 1, ownerProfileId: 5);

        Assert.NotNull(result);
        Assert.Equal(1, result.ReminderCount);
        Assert.True(result.RemainingHours > 37.0);
        Assert.Contains("NHẮC NHỞ SLA 48H", result.Message);
        Assert.Equal(1, change.ReminderCount);
        Assert.NotNull(change.LastRemindedAtUtc);
    }

    [Fact]
    public async Task SendReminder_WhenOverdue_ReturnsEmergencyWarningMessage()
    {
        var repo = new FakeLotPhysicalChangeRepo();
        var lot = CreateTestLot(id: 1, ownerProfileId: 5);
        repo.Lots.Add(lot);

        var change = WithId(new LotPhysicalChange
        {
            ParkingLotId = 1,
            Title = "Quá hạn",
            OccurredAtUtc = DateTime.UtcNow.AddHours(-55),
            DeadlineUtc = DateTime.UtcNow.AddHours(-7),
            Status = PhysicalChangeStatus.InProgress
        }, 1);
        repo.Changes.Add(change);

        var useCase = new SendPhysicalChangeReminderUseCase(repo);
        var result = await useCase.ExecuteAsync(1, 1, ownerProfileId: 5);

        Assert.Contains("CẢNH BÁO KHẨN CẤP", result.Message);
        Assert.Contains("SLA Cấp 2", result.Message);
        Assert.Contains("100% bồi thường", result.Message);
        Assert.Equal(PhysicalChangeStatus.Overdue, change.Status);
    }

    [Fact]
    public async Task SendReminder_ThrowsConflict_WhenChangeAlreadyFinished()
    {
        var repo = new FakeLotPhysicalChangeRepo();
        var lot = CreateTestLot(id: 1, ownerProfileId: 5);
        repo.Lots.Add(lot);

        var change = WithId(new LotPhysicalChange
        {
            ParkingLotId = 1,
            Title = "Đã xong",
            Status = PhysicalChangeStatus.Completed
        }, 1);
        repo.Changes.Add(change);

        var useCase = new SendPhysicalChangeReminderUseCase(repo);

        await Assert.ThrowsAsync<ConflictException>(() =>
            useCase.ExecuteAsync(1, 1, ownerProfileId: 5));
    }

    [Fact]
    public async Task CancelPhysicalChange_CancelsSuccessfully_WhenInProgress()
    {
        var repo = new FakeLotPhysicalChangeRepo();
        var lot = CreateTestLot(id: 1, ownerProfileId: 5);
        repo.Lots.Add(lot);

        var change = WithId(new LotPhysicalChange
        {
            ParkingLotId = 1,
            Title = "Hoãn sửa chữa",
            Status = PhysicalChangeStatus.InProgress
        }, 1);
        repo.Changes.Add(change);

        var useCase = new CancelPhysicalChangeUseCase(repo);
        var result = await useCase.ExecuteAsync(1, 1, new CancelPhysicalChangeRequest("Tòa nhà hoãn thi công"), ownerProfileId: 5);

        Assert.Equal("Cancelled", result.Status);
        Assert.Equal("Tòa nhà hoãn thi công", change.CompletionNote);
    }

    // ==================== TIÊU CHÍ 2: QUÁ 48H CHƯA CẬP NHẬT MÀ CÓ KHÁCH ĐẶT NHẦM ====================

    [Fact]
    public async Task RecordBookingError_WhenOverdue_Applies100PercentCompensation_AndLevel2Violation()
    {
        var repo = new FakeLotPhysicalChangeRepo();
        var lot = CreateTestLot(id: 1);
        repo.Lots.Add(lot);

        // Đợt thay đổi đã quá hạn 48 giờ
        var change = WithId(new LotPhysicalChange
        {
            ParkingLotId = 1,
            Title = "Cải tạo sàn hầm",
            OccurredAtUtc = DateTime.UtcNow.AddHours(-50),
            DeadlineUtc = DateTime.UtcNow.AddHours(-2), // Quá 2 giờ
            Status = PhysicalChangeStatus.InProgress,
            HasBookingIncident = false,
            BookingIncidentCount = 0
        }, 1);
        repo.Changes.Add(change);

        var useCase = new RecordPhysicalChangeBookingErrorUseCase(repo);
        var request = new RecordPhysicalChangeBookingErrorRequest(
            BookingCode: "BK-2026-9999",
            SlotCode: "A-01",
            CompensationAmount: 350000m,
            Reason: "Khách đến nơi nhưng ô A-01 đã bị dỡ bỏ biến thành trụ sạc, sơ đồ chưa cập nhật");

        var result = await useCase.ExecuteAsync(1, 1, request);

        // Xác thực kết quả Tiêu chí 2
        Assert.NotNull(result);
        Assert.True(result.HasBookingIncident);
        Assert.Equal(1, result.BookingIncidentCount);
        Assert.Equal(100, result.OwnerLiabilityPercent); // CHỦ BÃI CHỊU 100% BỒI THƯỜNG!
        Assert.Equal(350000m, result.TotalCompensationAmount);
        Assert.Equal("RankingPenalty", result.ViolationLevel); // GHI NHẬN VI PHẠM SLA CẤP 2 (RankingPenalty)!
        Assert.Equal("Overdue", result.Status);
    }

    [Fact]
    public async Task RecordBookingError_ThrowsConflict_WhenChangeIsStillWithin48h()
    {
        var repo = new FakeLotPhysicalChangeRepo();
        var lot = CreateTestLot(id: 1);
        repo.Lots.Add(lot);

        // Đợt thay đổi mới tạo 5 giờ trước (còn hạn 43h)
        var change = WithId(new LotPhysicalChange
        {
            ParkingLotId = 1,
            Title = "Đang trong 48 giờ",
            OccurredAtUtc = DateTime.UtcNow.AddHours(-5),
            DeadlineUtc = DateTime.UtcNow.AddHours(43),
            Status = PhysicalChangeStatus.InProgress
        }, 1);
        repo.Changes.Add(change);

        var useCase = new RecordPhysicalChangeBookingErrorUseCase(repo);
        var request = new RecordPhysicalChangeBookingErrorRequest("BK-123", "A-01", 100000m, "Lỗi đặt");

        // Không cho phép áp chế tài Tiêu chí 2 khi chưa quá 48h
        await Assert.ThrowsAsync<ConflictException>(() =>
            useCase.ExecuteAsync(1, 1, request));
    }

    [Fact]
    public async Task RecordBookingError_ThrowsValidation_WhenBookingCodeEmpty()
    {
        var repo = new FakeLotPhysicalChangeRepo();
        var lot = CreateTestLot(id: 1);
        repo.Lots.Add(lot);

        var change = WithId(new LotPhysicalChange
        {
            ParkingLotId = 1,
            Title = "Quá hạn",
            OccurredAtUtc = DateTime.UtcNow.AddHours(-50),
            DeadlineUtc = DateTime.UtcNow.AddHours(-2),
            Status = PhysicalChangeStatus.Overdue
        }, 1);
        repo.Changes.Add(change);

        var useCase = new RecordPhysicalChangeBookingErrorUseCase(repo);
        var request = new RecordPhysicalChangeBookingErrorRequest("  ", "A-01", 100000m, "Lỗi");

        await Assert.ThrowsAsync<ValidationException>(() =>
            useCase.ExecuteAsync(1, 1, request));
    }

    // ==================== HOÀN TẤT CẬP NHẬT SƠ ĐỒ (COMPLETE) ====================

    [Fact]
    public async Task CompletePhysicalChange_Within48h_MarksAsCompletedOnTime()
    {
        var repo = new FakeLotPhysicalChangeRepo();
        var lot = CreateTestLot(id: 1, ownerProfileId: 5);
        repo.Lots.Add(lot);

        var change = WithId(new LotPhysicalChange
        {
            ParkingLotId = 1,
            Title = "Cải tạo layout",
            OccurredAtUtc = DateTime.UtcNow.AddHours(-20),
            DeadlineUtc = DateTime.UtcNow.AddHours(28), // Còn 28h
            Status = PhysicalChangeStatus.InProgress
        }, 1);
        repo.Changes.Add(change);

        var useCase = new CompletePhysicalChangeUseCase(repo);
        var request = new CompletePhysicalChangeRequest(RelatedLayoutVersionId: 4, CompletionNote: "Đã xuất bản sơ đồ v4");

        var result = await useCase.ExecuteAsync(1, 1, request, ownerProfileId: 5);

        Assert.Equal("Completed", result.Status); // ĐÚNG HẠN SLA!
        Assert.Equal(4, result.RelatedLayoutVersionId);
        Assert.Equal("Đã xuất bản sơ đồ v4", result.CompletionNote);
        Assert.NotNull(result.CompletedAtUtc);
    }

    [Fact]
    public async Task CompletePhysicalChange_After48h_MarksAsCompletedLate()
    {
        var repo = new FakeLotPhysicalChangeRepo();
        var lot = CreateTestLot(id: 1, ownerProfileId: 5);
        repo.Lots.Add(lot);

        var change = WithId(new LotPhysicalChange
        {
            ParkingLotId = 1,
            Title = "Cải tạo trễ",
            OccurredAtUtc = DateTime.UtcNow.AddHours(-60),
            DeadlineUtc = DateTime.UtcNow.AddHours(-12), // Đã trễ 12h
            Status = PhysicalChangeStatus.Overdue
        }, 1);
        repo.Changes.Add(change);

        var useCase = new CompletePhysicalChangeUseCase(repo);
        var request = new CompletePhysicalChangeRequest(RelatedLayoutVersionId: 5, CompletionNote: "Cập nhật muộn");

        var result = await useCase.ExecuteAsync(1, 1, request, ownerProfileId: 5);

        Assert.Equal("CompletedLate", result.Status); // TRỄ HẠN!
    }

    [Fact]
    public async Task CompletePhysicalChange_ThrowsConflict_WhenAlreadyCompleted()
    {
        var repo = new FakeLotPhysicalChangeRepo();
        var lot = CreateTestLot(id: 1, ownerProfileId: 5);
        repo.Lots.Add(lot);

        var change = WithId(new LotPhysicalChange
        {
            ParkingLotId = 1,
            Title = "Đã xong",
            Status = PhysicalChangeStatus.Completed
        }, 1);
        repo.Changes.Add(change);

        var useCase = new CompletePhysicalChangeUseCase(repo);

        await Assert.ThrowsAsync<ConflictException>(() =>
            useCase.ExecuteAsync(1, 1, new CompletePhysicalChangeRequest(null, null), ownerProfileId: 5));
    }

    // ==================== BÁO CÁO TÓM TẮT SLA ====================

    [Fact]
    public async Task GetSlaSummary_CalculatesCorrectRates_AndIncidentCounts()
    {
        var repo = new FakeLotPhysicalChangeRepo();
        var lot = CreateTestLot(id: 1);
        repo.Lots.Add(lot);

        var now = DateTime.UtcNow;

        // 1 Completed on time
        repo.Changes.Add(WithId(new LotPhysicalChange
        {
            ParkingLotId = 1,
            Title = "Đợt 1",
            Status = PhysicalChangeStatus.Completed
        }, 1));

        // 1 Overdue with Level 2 violation and incident
        repo.Changes.Add(WithId(new LotPhysicalChange
        {
            ParkingLotId = 1,
            Title = "Đợt 2",
            Status = PhysicalChangeStatus.Overdue,
            HasBookingIncident = true,
            BookingIncidentCount = 2,
            ViolationLevel = SanctionLevel.RankingPenalty,
            TotalCompensationAmount = 500000m
        }, 2));

        var useCase = new GetPhysicalChangeSlaSummaryUseCase(repo);
        var summary = await useCase.ExecuteAsync(1);

        Assert.Equal(1, summary.ParkingLotId);
        Assert.Equal(2, summary.TotalChanges);
        Assert.Equal(1, summary.CompletedOnTimeCount);
        Assert.Equal(1, summary.OverdueCount);
        Assert.Equal(2, summary.TotalBookingIncidents);
        Assert.Equal(1, summary.Level2ViolationsCount);
        Assert.Equal(500000m, summary.TotalCompensationPaidByOwner);
        Assert.Equal(50.0, summary.SlaComplianceRate); // 1 out of 2 = 50%
    }
}
