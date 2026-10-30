using System.Reflection;
using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using ParkingManagement.SharedKernel.Contracts;
using ParkingManagement.SharedKernel.Domain;
using ParkingManagement.SharedKernel.Enums;
using ParkingManagement.SharedKernel.Exceptions;
using ParkingService.API.Controllers;
using ParkingService.Application.Abstractions;
using ParkingService.Application.Features.KybMisdeclaration;
using ParkingService.Domain.Entities;
using ParkingService.Domain.Enums;

namespace ParkingService.Test;

/// <summary>US-056: cảnh báo kèm hạn sửa, tạm khóa kèm hạn giải trình 24h, chuyển cấp, khôi phục bãi, audit + outbox.</summary>
public class LotMisdeclarationTests
{
    private const int AdminId = 1;
    private const int LotId = 7;
    private static readonly DateTime Now = new(2026, 10, 30, 3, 0, 0, DateTimeKind.Utc);

    // ==================== Helpers ====================

    private static T WithId<T>(T entity, int id) where T : BaseEntity
    {
        typeof(BaseEntity).GetProperty(nameof(BaseEntity.Id), BindingFlags.Public | BindingFlags.Instance)!.SetValue(entity, id);
        return entity;
    }

    private static ParkingLot Lot(ParkingLotStatus status = ParkingLotStatus.Active, int id = LotId) => new(
        id: id, name: "Bãi xe Thủ Đức", address: "12 Võ Văn Ngân", latitude: 10.85, longitude: 106.77, totalSlots: 30,
        availableSlots: 10, maxHeightCm: 210, openTime: new TimeOnly(6, 0), closeTime: new TimeOnly(22, 0), status: status,
        ownerProfileId: 57);

    private static LotMisdeclarationCase Warned(ParkingLot lot, int days = 7, int id = 3)
        => WithId(LotMisdeclarationCase.Warn(lot, MisdeclarationCategory.Capacity, "Khai 30 chỗ, thực tế 22 chỗ", days, AdminId,
            Now.AddDays(-10)), id);

    private static LotMisdeclarationCase SuspendedCase(ParkingLot lot, int id = 3)
        => WithId(LotMisdeclarationCase.Suspend(lot, MisdeclarationCategory.Photos, "Ảnh hiện trường lấy từ bãi khác", AdminId,
            Now.AddHours(-2)), id);

    private sealed class FixedClock(DateTime utcNow) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => new(utcNow);
    }

    private sealed class FakeRepo(params ParkingLot[] lots) : ILotMisdeclarationRepository
    {
        public List<LotMisdeclarationCase> Cases { get; } = [];
        public List<IntegrationEvent> Events { get; } = [];
        public int SaveCount { get; private set; }
        public bool ConflictOnSave { get; init; }
        public IReadOnlyList<ParkingLot> Lots => lots;

        public Task<ParkingLot?> GetLotAsync(int parkingLotId, CancellationToken cancellationToken = default)
            => Task.FromResult(lots.FirstOrDefault(l => l.Id == parkingLotId));

        public Task<LotMisdeclarationCase?> GetCaseAsync(int parkingLotId, int caseId, CancellationToken cancellationToken = default)
            => Task.FromResult(Cases.FirstOrDefault(c => c.Id == caseId && c.ParkingLotId == parkingLotId));

        public Task<bool> HasOpenCaseAsync(int parkingLotId, CancellationToken cancellationToken = default)
            => Task.FromResult(Cases.Any(c => c.ParkingLotId == parkingLotId && c.IsOpen));

        public List<KybApplication> Kybs { get; } = [];

        public Task<IReadOnlyList<ParkingLot>> GetOwnerLotsAsync(int ownerProfileId, CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<ParkingLot>>(lots.Where(l => l.OwnerProfileId == ownerProfileId).ToList());

        public Task<IReadOnlyList<LotMisdeclarationCase>> GetOpenCasesByOwnerAsync(int ownerProfileId,
            CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<LotMisdeclarationCase>>(
                Cases.Where(c => c.IsOpen && c.ParkingLot.OwnerProfileId == ownerProfileId).ToList());

        public Task<IReadOnlyList<KybApplication>> GetOpenKybApplicationsAsync(IReadOnlyCollection<int> parkingLotIds,
            CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<KybApplication>>(Kybs.Where(k => parkingLotIds.Contains(k.ParkingLotId) && k.IsOpen).ToList());

        public void Add(LotMisdeclarationCase misdeclarationCase) => Cases.Add(misdeclarationCase);

        public Task SaveWithEventsAsync(IReadOnlyList<IntegrationEvent> events, CancellationToken cancellationToken = default)
        {
            if (ConflictOnSave) throw new ConflictException("Case khai báo sai vừa được Admin khác cập nhật.");
            foreach (var c in Cases.Where(c => c.Id == 0)) WithId(c, 100 + Cases.IndexOf(c));
            Events.AddRange(events);
            SaveCount++;
            return Task.CompletedTask;
        }
    }

    private sealed class FakeQueries(FakeRepo repo) : ILotMisdeclarationQueries
    {
        public Task<MisdeclarationLotRow?> GetLotAsync(int parkingLotId, CancellationToken cancellationToken = default)
            => Task.FromResult(repo.Lots.Where(l => l.Id == parkingLotId)
                .Select(l => new MisdeclarationLotRow(l.Id, l.Name, l.Address, l.OwnerProfileId, l.Status)).FirstOrDefault());

        public Task<IReadOnlyList<MisdeclarationCaseRow>> ListCasesAsync(int parkingLotId, CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<MisdeclarationCaseRow>>(repo.Cases.Where(c => c.ParkingLotId == parkingLotId)
                .OrderByDescending(c => c.OpenedAtUtc).ThenByDescending(c => c.Id)
                .Select(Row).ToList());
    }

    private static MisdeclarationCaseRow Row(LotMisdeclarationCase c) => new(c.Id, c.Severity, c.Category, c.Status, c.Finding,
        c.DueAtUtc, c.OpenedByUserId, c.OpenedAtUtc, c.EscalationNote, c.EscalatedAtUtc, c.ClosedByUserId, c.ClosedAtUtc, c.ResolutionNote);

    private static readonly KybMisdeclarationSettings Settings = new();

    private static OpenLotMisdeclarationCaseUseCase OpenUseCase(FakeRepo repo, KybMisdeclarationSettings? settings = null)
        => new(repo, new FakeQueries(repo), settings ?? Settings, new FixedClock(Now));

    private static OpenLotMisdeclarationCaseRequest Minor(int? days = null)
        => new("Minor", "Capacity", "Khai 30 chỗ, thực tế 22 chỗ", days);

    private static OpenLotMisdeclarationCaseRequest Suspected(int? days = null)
        => new("Suspected", "Photos", "Ảnh hiện trường lấy từ bãi khác", days);

    private static JsonElement Json(string? value) => JsonDocument.Parse(value!).RootElement;

    // ==================== Domain: state machine ====================

    [Theory]
    [InlineData(MisdeclarationCaseStatus.Warned, MisdeclarationCaseStatus.Suspended, true)]
    [InlineData(MisdeclarationCaseStatus.Warned, MisdeclarationCaseStatus.Resolved, true)]
    [InlineData(MisdeclarationCaseStatus.Warned, MisdeclarationCaseStatus.Banned, true)]
    [InlineData(MisdeclarationCaseStatus.Suspended, MisdeclarationCaseStatus.Resolved, true)]
    [InlineData(MisdeclarationCaseStatus.Suspended, MisdeclarationCaseStatus.Banned, true)]
    [InlineData(MisdeclarationCaseStatus.Suspended, MisdeclarationCaseStatus.Warned, false)]
    [InlineData(MisdeclarationCaseStatus.Resolved, MisdeclarationCaseStatus.Suspended, false)]
    [InlineData(MisdeclarationCaseStatus.Resolved, MisdeclarationCaseStatus.Warned, false)]
    [InlineData(MisdeclarationCaseStatus.Banned, MisdeclarationCaseStatus.Resolved, false)]
    public void CanMove_FollowsTransitionTable(MisdeclarationCaseStatus from, MisdeclarationCaseStatus to, bool expected)
        => Assert.Equal(expected, LotMisdeclarationCase.CanMove(from, to));

    [Fact]
    public void Warn_KeepsLotActive_DueAfterCorrectionDays()
    {
        var lot = Lot();
        var c = LotMisdeclarationCase.Warn(lot, MisdeclarationCategory.Address, "  Sai số nhà  ", 5, AdminId, Now);

        Assert.Equal(MisdeclarationCaseStatus.Warned, c.Status);
        Assert.Equal(MisdeclarationSeverity.Minor, c.Severity);
        Assert.Equal(Now.AddDays(5), c.DueAtUtc);
        Assert.Equal("Sai số nhà", c.Finding);
        Assert.Equal(57, c.OwnerProfileId);
        Assert.Equal(ParkingLotStatus.Active, lot.Status);
        Assert.True(lot.IsPubliclyVisible);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(31)]
    public void Warn_CorrectionDaysOutOfRange_ThrowsValidation(int days)
        => Assert.Throws<ValidationException>(() =>
            LotMisdeclarationCase.Warn(Lot(), MisdeclarationCategory.Address, "Sai số nhà", days, AdminId, Now));

    [Fact]
    public void Suspend_SuspendsActiveLot_HidesFromDrivers_Due24h()
    {
        var lot = Lot();
        var c = LotMisdeclarationCase.Suspend(lot, MisdeclarationCategory.Photos, "Ảnh giả", AdminId, Now);

        Assert.Equal(MisdeclarationCaseStatus.Suspended, c.Status);
        Assert.Equal(MisdeclarationSeverity.Suspected, c.Severity);
        Assert.Equal(Now.AddHours(24), c.DueAtUtc);
        Assert.Equal(ParkingLotStatus.Suspended, lot.Status);
        Assert.False(lot.IsPubliclyVisible);
    }

    [Theory]
    [InlineData(ParkingLotStatus.PendingApproval)]
    [InlineData(ParkingLotStatus.Suspended)]
    public void Open_WhenLotNotActive_ThrowsConflict_AndKeepsLotStatus(ParkingLotStatus status)
    {
        var lot = Lot(status);
        Assert.Throws<ConflictException>(() => LotMisdeclarationCase.Warn(lot, MisdeclarationCategory.Address, "x", 7, AdminId, Now));
        Assert.Throws<ConflictException>(() => LotMisdeclarationCase.Suspend(lot, MisdeclarationCategory.Address, "x", AdminId, Now));
        Assert.Equal(status, lot.Status);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("   ")]
    public void Open_WithoutFinding_ThrowsValidation_AndKeepsLotActive(string? finding)
    {
        var lot = Lot();
        Assert.Throws<ValidationException>(() => LotMisdeclarationCase.Suspend(lot, MisdeclarationCategory.Photos, finding!, AdminId, Now));
        Assert.Equal(ParkingLotStatus.Active, lot.Status);
    }

    [Fact]
    public void Open_FindingTooLong_ThrowsValidation()
        => Assert.Throws<ValidationException>(() =>
            LotMisdeclarationCase.Warn(Lot(), MisdeclarationCategory.Other, new string('x', 1001), 7, AdminId, Now));

    [Fact]
    public void Escalate_Warned_SuspendsLot_AndRestarts24hWindow()
    {
        var lot = Lot();
        var c = Warned(lot);

        c.Escalate(AdminId, "Quá hạn sửa chưa cập nhật sức chứa", Now);

        Assert.Equal(MisdeclarationCaseStatus.Suspended, c.Status);
        Assert.Equal(MisdeclarationSeverity.Suspected, c.Severity);
        Assert.Equal(Now.AddHours(24), c.DueAtUtc);
        Assert.Equal(Now, c.EscalatedAtUtc);
        Assert.Equal(ParkingLotStatus.Suspended, lot.Status);
    }

    [Fact]
    public void Escalate_WhenAlreadySuspended_ThrowsConflict()
    {
        var c = SuspendedCase(Lot());
        Assert.Throws<ConflictException>(() => c.Escalate(AdminId, "Lần 2", Now));
    }

    [Fact]
    public void Escalate_WithoutNote_ThrowsValidation_AndKeepsLotActive()
    {
        var lot = Lot();
        var c = Warned(lot);
        Assert.Throws<ValidationException>(() => c.Escalate(AdminId, " ", Now));
        Assert.Equal(MisdeclarationCaseStatus.Warned, c.Status);
        Assert.Equal(ParkingLotStatus.Active, lot.Status);
    }

    [Fact]
    public void Resolve_Warned_ClosesCase_LotStaysActive()
    {
        var lot = Lot();
        var c = Warned(lot);

        var reinstated = c.Resolve(AdminId, "Đã cập nhật sức chứa 22 chỗ", Now);

        Assert.False(reinstated);
        Assert.Equal(MisdeclarationCaseStatus.Resolved, c.Status);
        Assert.Equal(AdminId, c.ClosedByUserId);
        Assert.Equal(Now, c.ClosedAtUtc);
        Assert.Equal(ParkingLotStatus.Active, lot.Status);
    }

    [Fact]
    public void Resolve_Suspended_ReinstatesLot()
    {
        var lot = Lot();
        var c = SuspendedCase(lot);

        Assert.True(c.Resolve(AdminId, "Giải trình hợp lệ, ảnh đã chụp lại", Now));
        Assert.Equal(ParkingLotStatus.Active, lot.Status);
        Assert.True(lot.IsPubliclyVisible);
    }

    [Fact]
    public void Resolve_WhenClosed_ThrowsConflict()
    {
        var c = Warned(Lot());
        c.Resolve(AdminId, "Đã sửa", Now);
        Assert.Throws<ConflictException>(() => c.Resolve(AdminId, "Lần 2", Now));
        Assert.Throws<ConflictException>(() => c.Escalate(AdminId, "Lần 2", Now));
    }

    [Fact]
    public void ParkingLot_SuspendListing_And_Reinstate_AreGuarded()
    {
        Assert.Throws<ConflictException>(() => Lot(ParkingLotStatus.PendingApproval).SuspendListing());
        Assert.Throws<ConflictException>(() => Lot(ParkingLotStatus.Suspended).SuspendListing());
        Assert.Throws<ConflictException>(() => Lot(ParkingLotStatus.Active).Reinstate());
        Assert.Throws<ConflictException>(() => Lot(ParkingLotStatus.PendingApproval).Reinstate());
    }

    [Fact]
    public void IsOverdue_OnlyForOpenCasePastDue()
    {
        var c = Warned(Lot(), days: 7);                       // mở lúc Now-10d, hạn Now-3d
        Assert.True(c.IsOverdue(Now));
        Assert.False(c.IsOverdue(Now.AddDays(-4)));
        c.Resolve(AdminId, "Đã sửa", Now);
        Assert.False(c.IsOverdue(Now));
    }

    // ==================== Use case: mở case ====================

    [Fact]
    public async Task Open_Minor_UsesConfiguredDays_WritesWarnedAudit_InOneSave()
    {
        var repo = new FakeRepo(Lot());

        var dto = await OpenUseCase(repo, new KybMisdeclarationSettings(10)).ExecuteAsync(LotId, Minor(), AdminId);

        var c = Assert.Single(repo.Cases);
        Assert.Equal(Now.AddDays(10), c.DueAtUtc);
        Assert.Equal(1, repo.SaveCount);
        var audit = Assert.IsType<AuditRecorded>(Assert.Single(repo.Events));
        Assert.Equal("ParkingLot.MisdeclarationWarned", audit.Action);
        Assert.Equal("ParkingLot", audit.EntityName);
        Assert.Equal(LotId.ToString(), audit.EntityId);
        Assert.Equal(AdminId, audit.UserId);
        Assert.Equal("ParkingService", audit.SourceService);
        Assert.Equal("Khai 30 chỗ, thực tế 22 chỗ", audit.Reason);
        Assert.Equal(Now, audit.OccurredAtUtc);
        Assert.Equal("Warned", Json(audit.NewValuesJson).GetProperty("caseStatus").GetString());
        Assert.Equal("Active", dto.ParkingLot.Status);
        Assert.Equal("Warned", dto.OpenCase!.Status);
        Assert.Equal(10, dto.MinorCorrectionDays);
    }

    [Fact]
    public async Task Open_Minor_AdminOverridesDays()
    {
        var repo = new FakeRepo(Lot());
        await OpenUseCase(repo).ExecuteAsync(LotId, Minor(days: 3), AdminId);
        Assert.Equal(Now.AddDays(3), Assert.Single(repo.Cases).DueAtUtc);
    }

    [Fact]
    public async Task Open_Suspected_SuspendsLot_Due24h_WritesSuspendedAudit()
    {
        var lot = Lot();
        var repo = new FakeRepo(lot);

        var dto = await OpenUseCase(repo).ExecuteAsync(LotId, Suspected(), AdminId);

        Assert.Equal(ParkingLotStatus.Suspended, lot.Status);
        Assert.Equal(Now.AddHours(24), dto.OpenCase!.DueAtUtc);
        Assert.Equal("Suspended", dto.ParkingLot.Status);
        Assert.False(dto.ParkingLot.IsPubliclyVisible);
        var audit = Assert.IsType<AuditRecorded>(Assert.Single(repo.Events));
        Assert.Equal("ParkingLot.MisdeclarationSuspended", audit.Action);
        Assert.Equal("Active", Json(audit.OldValuesJson).GetProperty("lotStatus").GetString());
        Assert.Equal("Suspended", Json(audit.NewValuesJson).GetProperty("lotStatus").GetString());
    }

    [Fact]
    public async Task Open_Suspected_WithCorrectionDays_Throws400_AndSavesNothing()
    {
        var lot = Lot();
        var repo = new FakeRepo(lot);
        await Assert.ThrowsAsync<ValidationException>(() => OpenUseCase(repo).ExecuteAsync(LotId, Suspected(days: 3), AdminId));
        Assert.Equal(0, repo.SaveCount);
        Assert.Equal(ParkingLotStatus.Active, lot.Status);
    }

    [Theory]
    [InlineData(null, "Capacity")]
    [InlineData("Severe", "Capacity")]
    [InlineData("1", "Capacity")]
    [InlineData("Minor", "Price")]
    [InlineData("Minor", null)]
    public async Task Open_InvalidSeverityOrCategory_Throws400(string? severity, string? category)
    {
        var repo = new FakeRepo(Lot());
        await Assert.ThrowsAsync<ValidationException>(() =>
            OpenUseCase(repo).ExecuteAsync(LotId, new OpenLotMisdeclarationCaseRequest(severity, category, "x"), AdminId));
        Assert.Empty(repo.Cases);
    }

    [Fact]
    public async Task Open_AcceptsCaseInsensitiveEnums()
    {
        var repo = new FakeRepo(Lot());
        await OpenUseCase(repo).ExecuteAsync(LotId, new OpenLotMisdeclarationCaseRequest("minor", "address", "Sai số nhà"), AdminId);
        Assert.Equal(MisdeclarationCategory.Address, Assert.Single(repo.Cases).Category);
    }

    [Fact]
    public async Task Open_WhenOpenCaseExists_Throws409_AndSavesNothing()
    {
        var lot = Lot();
        var repo = new FakeRepo(lot);
        repo.Cases.Add(Warned(lot));

        await Assert.ThrowsAsync<ConflictException>(() => OpenUseCase(repo).ExecuteAsync(LotId, Suspected(), AdminId));
        Assert.Equal(0, repo.SaveCount);
        Assert.Equal(ParkingLotStatus.Active, lot.Status);
    }

    [Fact]
    public async Task Open_AfterPreviousCaseResolved_IsAllowed()
    {
        var lot = Lot();
        var repo = new FakeRepo(lot);
        var old = Warned(lot);
        old.Resolve(AdminId, "Đã sửa", Now.AddDays(-1));
        repo.Cases.Add(old);

        var dto = await OpenUseCase(repo).ExecuteAsync(LotId, Minor(), AdminId);

        Assert.Equal(2, dto.Cases.Count);
        Assert.Equal("Warned", dto.Cases[0].Status);      // mới mở trước
        Assert.Equal("Resolved", dto.Cases[1].Status);
    }

    [Theory]
    [InlineData(ParkingLotStatus.PendingApproval)]
    [InlineData(ParkingLotStatus.Suspended)]
    public async Task Open_LotNotActive_Throws409_AndSavesNothing(ParkingLotStatus status)
    {
        var repo = new FakeRepo(Lot(status));
        await Assert.ThrowsAsync<ConflictException>(() => OpenUseCase(repo).ExecuteAsync(LotId, Minor(), AdminId));
        Assert.Equal(0, repo.SaveCount);
    }

    [Fact]
    public async Task Open_UnknownLot_Throws404_InvalidIds_Throw400()
    {
        var repo = new FakeRepo(Lot());
        await Assert.ThrowsAsync<NotFoundException>(() => OpenUseCase(repo).ExecuteAsync(99, Minor(), AdminId));
        await Assert.ThrowsAsync<ValidationException>(() => OpenUseCase(repo).ExecuteAsync(0, Minor(), AdminId));
        await Assert.ThrowsAsync<ValidationException>(() => OpenUseCase(repo).ExecuteAsync(LotId, Minor(), 0));
    }

    // ==================== Use case: chuyển cấp / đóng case ====================

    [Fact]
    public async Task Escalate_WritesSuspendedAudit_WithNoteAsReason()
    {
        var lot = Lot();
        var repo = new FakeRepo(lot);
        repo.Cases.Add(Warned(lot));
        var useCase = new EscalateLotMisdeclarationCaseUseCase(repo, new FakeQueries(repo), Settings, new FixedClock(Now));

        var dto = await useCase.ExecuteAsync(LotId, 3, new EscalateLotMisdeclarationCaseRequest("Quá hạn sửa"), AdminId);

        Assert.Equal(ParkingLotStatus.Suspended, lot.Status);
        Assert.Equal("Suspended", dto.OpenCase!.Status);
        var audit = Assert.IsType<AuditRecorded>(Assert.Single(repo.Events));
        Assert.Equal("ParkingLot.MisdeclarationSuspended", audit.Action);
        Assert.Equal("Quá hạn sửa", audit.Reason);
        Assert.Equal(3, Json(audit.NewValuesJson).GetProperty("caseId").GetInt32());
    }

    [Fact]
    public async Task Resolve_SuspendedCase_ReinstatesLot_WritesResolvedAndReinstatedAudits_InOneSave()
    {
        var lot = Lot();
        var repo = new FakeRepo(lot);
        repo.Cases.Add(SuspendedCase(lot));
        var useCase = new ResolveLotMisdeclarationCaseUseCase(repo, new FakeQueries(repo), Settings, new FixedClock(Now));

        var dto = await useCase.ExecuteAsync(LotId, 3, new ResolveLotMisdeclarationCaseRequest("Giải trình hợp lệ"), AdminId);

        Assert.Equal(ParkingLotStatus.Active, lot.Status);
        Assert.Null(dto.OpenCase);
        Assert.Equal(1, repo.SaveCount);
        Assert.Equal(["ParkingLot.MisdeclarationResolved", "ParkingLot.Reinstated"],
            repo.Events.Cast<AuditRecorded>().Select(a => a.Action));
    }

    [Fact]
    public async Task Resolve_WarnedCase_WritesOnlyResolvedAudit()
    {
        var lot = Lot();
        var repo = new FakeRepo(lot);
        repo.Cases.Add(Warned(lot));
        var useCase = new ResolveLotMisdeclarationCaseUseCase(repo, new FakeQueries(repo), Settings, new FixedClock(Now));

        await useCase.ExecuteAsync(LotId, 3, new ResolveLotMisdeclarationCaseRequest("Đã cập nhật"), AdminId);

        Assert.Equal("ParkingLot.MisdeclarationResolved", Assert.IsType<AuditRecorded>(Assert.Single(repo.Events)).Action);
        Assert.Equal(ParkingLotStatus.Active, lot.Status);
    }

    [Fact]
    public async Task Resolve_MissingNote_Throws400_BeforeSaving()
    {
        var lot = Lot();
        var repo = new FakeRepo(lot);
        repo.Cases.Add(SuspendedCase(lot));
        var useCase = new ResolveLotMisdeclarationCaseUseCase(repo, new FakeQueries(repo), Settings, new FixedClock(Now));

        await Assert.ThrowsAsync<ValidationException>(() =>
            useCase.ExecuteAsync(LotId, 3, new ResolveLotMisdeclarationCaseRequest(null), AdminId));
        Assert.Equal(0, repo.SaveCount);
        Assert.Equal(ParkingLotStatus.Suspended, lot.Status);
    }

    [Fact]
    public async Task CaseActions_UnknownCaseOrOtherLot_Throw404_InvalidIds_Throw400()
    {
        var lot = Lot();
        var other = Lot(id: 8);
        var repo = new FakeRepo(lot, other);
        repo.Cases.Add(Warned(lot));
        var escalate = new EscalateLotMisdeclarationCaseUseCase(repo, new FakeQueries(repo), Settings, new FixedClock(Now));
        var resolve = new ResolveLotMisdeclarationCaseUseCase(repo, new FakeQueries(repo), Settings, new FixedClock(Now));

        await Assert.ThrowsAsync<NotFoundException>(() => escalate.ExecuteAsync(LotId, 99, new("x"), AdminId));
        await Assert.ThrowsAsync<NotFoundException>(() => resolve.ExecuteAsync(8, 3, new("x"), AdminId));   // case của bãi khác
        await Assert.ThrowsAsync<ValidationException>(() => resolve.ExecuteAsync(LotId, 0, new("x"), AdminId));
        await Assert.ThrowsAsync<ValidationException>(() => escalate.ExecuteAsync(LotId, 3, new("x"), 0));
        Assert.Equal(0, repo.SaveCount);
    }

    [Fact]
    public async Task ConcurrentUpdate_SurfacesAs409()
    {
        var lot = Lot();
        var repo = new FakeRepo(lot) { ConflictOnSave = true };
        repo.Cases.Add(Warned(lot));
        var useCase = new ResolveLotMisdeclarationCaseUseCase(repo, new FakeQueries(repo), Settings, new FixedClock(Now));

        await Assert.ThrowsAsync<ConflictException>(() => useCase.ExecuteAsync(LotId, 3, new("Đã sửa"), AdminId));
    }

    // ==================== Cấm chủ bãi (AC2) ====================

    private static ParkingLot OwnerLot(int id, ParkingLotStatus status, int ownerProfileId = 57) => new(
        id: id, name: $"Bãi #{id}", address: "Q.9", latitude: 10.85, longitude: 106.77, totalSlots: 30, availableSlots: 0,
        maxHeightCm: 210, openTime: new TimeOnly(6, 0), closeTime: new TimeOnly(22, 0), status: status, ownerProfileId: ownerProfileId);

    private static KybApplication KybOf(ParkingLot lot, KybStatus status, int id)
        => WithId(new KybApplication { ParkingLotId = lot.Id, ParkingLot = lot, Status = status }, id);

    private static BanLotOwnerUseCase BanUseCase(FakeRepo repo) => new(repo, new FakeQueries(repo), Settings, new FixedClock(Now));

    [Fact]
    public void Ban_FromWarnedOrSuspended_ClosesCase_AndIsTerminal()
    {
        var warned = Warned(Lot());
        warned.Ban(AdminId, "Cố ý khai khống công suất", Now);
        Assert.Equal(MisdeclarationCaseStatus.Banned, warned.Status);
        Assert.Equal("Cố ý khai khống công suất", warned.ResolutionNote);
        Assert.False(warned.IsOpen);
        Assert.Throws<ConflictException>(() => warned.Resolve(AdminId, "Khôi phục", Now));

        var suspended = SuspendedCase(Lot());
        suspended.Ban(AdminId, "Giả mạo giấy phép", Now);
        Assert.Equal(MisdeclarationCaseStatus.Banned, suspended.Status);
    }

    [Fact]
    public void ParkingLot_SuspendForOwnerBan_FromActiveOrPending_IdempotentWhenSuspended()
    {
        var active = Lot(ParkingLotStatus.Active);
        var pending = Lot(ParkingLotStatus.PendingApproval);
        var suspended = Lot(ParkingLotStatus.Suspended);

        Assert.True(active.SuspendForOwnerBan());
        Assert.True(pending.SuspendForOwnerBan());
        Assert.False(suspended.SuspendForOwnerBan());
        Assert.All([active, pending, suspended], l => Assert.Equal(ParkingLotStatus.Suspended, l.Status));
        Assert.False(active.IsDeleted);                    // không soft-delete: dữ liệu giữ lại
    }

    [Fact]
    public async Task Ban_SuspendsAllOwnerLots_RejectsOpenKyb_BansOtherOpenCases_InOneSave()
    {
        var lot = Lot();                                                    // bãi có case gốc (Active)
        var pending = OwnerLot(8, ParkingLotStatus.PendingApproval);        // bãi đang thẩm định
        var otherActive = OwnerLot(9, ParkingLotStatus.Active);             // bãi có case cảnh báo khác
        var alreadySuspended = OwnerLot(10, ParkingLotStatus.Suspended);    // không đổi, không có case → không audit
        var stranger = OwnerLot(11, ParkingLotStatus.Active, ownerProfileId: 99);
        var repo = new FakeRepo(lot, pending, otherActive, alreadySuspended, stranger);
        repo.Cases.Add(Warned(lot, id: 3));
        repo.Cases.Add(Warned(otherActive, id: 4));
        repo.Kybs.Add(KybOf(pending, KybStatus.UnderReview, 21));
        repo.Kybs.Add(KybOf(otherActive, KybStatus.NeedsMoreInfo, 22));
        repo.Kybs.Add(KybOf(lot, KybStatus.Approved, 23));                  // đã duyệt → giữ nguyên
        repo.Kybs.Add(KybOf(stranger, KybStatus.Submitted, 24));            // chủ bãi khác → giữ nguyên

        var result = await BanUseCase(repo).ExecuteAsync(LotId, 3, new BanLotOwnerRequest("Cố ý khai khống công suất"), AdminId);

        Assert.Equal(1, repo.SaveCount);
        Assert.Equal(57, result.OwnerProfileId);
        Assert.Equal([LotId, 8, 9], result.SuspendedLotIds.Order());
        Assert.Equal([21, 22], result.RejectedKybApplicationIds);
        Assert.Equal([3, 4], result.BannedCaseIds);
        Assert.All([lot, pending, otherActive, alreadySuspended], l => Assert.Equal(ParkingLotStatus.Suspended, l.Status));
        Assert.Equal(ParkingLotStatus.Active, stranger.Status);
        Assert.All(repo.Cases, c => Assert.Equal(MisdeclarationCaseStatus.Banned, c.Status));
        Assert.All(repo.Kybs.Where(k => k.Id is 21 or 22), k =>
        {
            Assert.Equal(KybStatus.Rejected, k.Status);
            Assert.Equal("Chủ bãi bị cấm do khai báo gian lận", k.RejectReason);
            Assert.Equal(AdminId, k.ReviewedByUserId);
        });
        Assert.Equal(KybStatus.Approved, repo.Kybs.Single(k => k.Id == 23).Status);
        Assert.Equal(KybStatus.Submitted, repo.Kybs.Single(k => k.Id == 24).Status);
        Assert.Equal("Banned", result.Lot.Cases[0].Status);
        Assert.Null(result.Lot.OpenCase);

        var audits = repo.Events.Cast<AuditRecorded>().ToList();
        Assert.Equal(3, audits.Count(a => a.Action == "ParkingLot.OwnerBanned"));
        Assert.Equal([LotId.ToString(), "8", "9"], audits.Where(a => a.Action == "ParkingLot.OwnerBanned").Select(a => a.EntityId).Order());
        var kybAudits = audits.Where(a => a.Action == "KybApplication.RejectedByBan").ToList();
        Assert.Equal(["21", "22"], kybAudits.Select(a => a.EntityId));
        Assert.All(kybAudits, a => Assert.Equal("KybApplication", a.EntityName));
        Assert.Equal("UnderReview", Json(kybAudits[0].OldValuesJson).GetProperty("status").GetString());
        Assert.Equal("Rejected", Json(kybAudits[0].NewValuesJson).GetProperty("status").GetString());
        Assert.Equal(5, audits.Count);
    }

    [Fact]
    public async Task Ban_FromSuspendedCase_AuditsLotEvenIfStatusUnchanged()
    {
        var lot = Lot();
        var repo = new FakeRepo(lot);
        repo.Cases.Add(SuspendedCase(lot));

        var result = await BanUseCase(repo).ExecuteAsync(LotId, 3, new BanLotOwnerRequest("Giả mạo giấy phép"), AdminId);

        Assert.Empty(result.SuspendedLotIds);
        var audit = Assert.IsType<AuditRecorded>(Assert.Single(repo.Events));
        Assert.Equal("ParkingLot.OwnerBanned", audit.Action);
        Assert.Equal("Suspended", Json(audit.OldValuesJson).GetProperty("caseStatus").GetString());
        Assert.Equal("Banned", Json(audit.NewValuesJson).GetProperty("caseStatus").GetString());
    }

    [Fact]
    public async Task Ban_MissingReason_Throws400_AndChangesNothing()
    {
        var lot = Lot();
        var pending = OwnerLot(8, ParkingLotStatus.PendingApproval);
        var repo = new FakeRepo(lot, pending);
        repo.Cases.Add(Warned(lot));
        repo.Kybs.Add(KybOf(pending, KybStatus.UnderReview, 21));

        await Assert.ThrowsAsync<ValidationException>(() => BanUseCase(repo).ExecuteAsync(LotId, 3, new BanLotOwnerRequest(" "), AdminId));

        Assert.Equal(0, repo.SaveCount);
        Assert.Equal(ParkingLotStatus.Active, lot.Status);
        Assert.Equal(ParkingLotStatus.PendingApproval, pending.Status);
        Assert.Equal(KybStatus.UnderReview, repo.Kybs[0].Status);
        Assert.Equal(MisdeclarationCaseStatus.Warned, repo.Cases[0].Status);
    }

    [Fact]
    public async Task Ban_ClosedCase_Throws409_AndChangesNothing()
    {
        var lot = Lot();
        var pending = OwnerLot(8, ParkingLotStatus.PendingApproval);
        var repo = new FakeRepo(lot, pending);
        var closed = Warned(lot);
        closed.Resolve(AdminId, "Đã sửa", Now.AddDays(-1));
        repo.Cases.Add(closed);

        await Assert.ThrowsAsync<ConflictException>(() => BanUseCase(repo).ExecuteAsync(LotId, 3, new BanLotOwnerRequest("Gian lận"), AdminId));
        Assert.Equal(0, repo.SaveCount);
        Assert.Equal(ParkingLotStatus.Active, lot.Status);
        Assert.Equal(ParkingLotStatus.PendingApproval, pending.Status);
    }

    [Fact]
    public async Task Ban_UnknownCase_Throws404_InvalidIds_Throw400()
    {
        var repo = new FakeRepo(Lot());
        await Assert.ThrowsAsync<NotFoundException>(() => BanUseCase(repo).ExecuteAsync(LotId, 99, new("Gian lận"), AdminId));
        await Assert.ThrowsAsync<ValidationException>(() => BanUseCase(repo).ExecuteAsync(LotId, 3, new("Gian lận"), 0));
    }

    [Fact]
    public async Task AfterBan_LotCannotBeReopenedOrReinstated()
    {
        var lot = Lot();
        var repo = new FakeRepo(lot);
        repo.Cases.Add(SuspendedCase(lot));
        await BanUseCase(repo).ExecuteAsync(LotId, 3, new BanLotOwnerRequest("Gian lận"), AdminId);

        var resolve = new ResolveLotMisdeclarationCaseUseCase(repo, new FakeQueries(repo), Settings, new FixedClock(Now));
        await Assert.ThrowsAsync<ConflictException>(() => resolve.ExecuteAsync(LotId, 3, new("Khôi phục"), AdminId));
        await Assert.ThrowsAsync<ConflictException>(() => OpenUseCase(repo).ExecuteAsync(LotId, Minor(), AdminId));
        Assert.Equal(ParkingLotStatus.Suspended, lot.Status);
    }

    // ==================== Đọc ====================

    [Fact]
    public async Task Get_ReturnsSuspendedLot_OpenCaseWithOverdueFlag_AndDefaults()
    {
        var lot = Lot();
        var repo = new FakeRepo(lot);
        repo.Cases.Add(SuspendedCase(lot));                // mở Now-2h, hạn Now+22h
        var useCase = new GetLotMisdeclarationUseCase(new FakeQueries(repo), Settings, new FixedClock(Now.AddHours(23)));

        var dto = await useCase.ExecuteAsync(LotId);

        Assert.Equal("Suspended", dto.ParkingLot.Status);
        Assert.False(dto.ParkingLot.IsPubliclyVisible);
        Assert.True(dto.OpenCase!.IsOverdue);
        Assert.Equal("Suspected", dto.OpenCase.Severity);
        Assert.Equal("Photos", dto.OpenCase.Category);
        Assert.Equal(7, dto.MinorCorrectionDays);
        Assert.Equal(24, dto.ResponseWindowHours);
    }

    [Fact]
    public async Task Get_UnknownLot_Throws404_InvalidId_Throws400()
    {
        var repo = new FakeRepo(Lot());
        var useCase = new GetLotMisdeclarationUseCase(new FakeQueries(repo), Settings, new FixedClock(Now));
        await Assert.ThrowsAsync<NotFoundException>(() => useCase.ExecuteAsync(99));
        await Assert.ThrowsAsync<ValidationException>(() => useCase.ExecuteAsync(0));
    }

    [Fact]
    public void Mapper_ClosedCaseIsNeverOverdue()
    {
        var c = Warned(Lot());
        c.Resolve(AdminId, "Đã sửa", Now.AddDays(-5));
        Assert.False(MisdeclarationMapper.ToDto(Row(c), Now).IsOverdue);
    }

    // ==================== Cấu hình & phân quyền ====================

    [Theory]
    [InlineData(0)]
    [InlineData(31)]
    public void Settings_OutOfRange_Throws(int days) => Assert.Throws<ValidationException>(() => new KybMisdeclarationSettings(days));

    [Fact]
    public void Settings_DefaultIs7Days() => Assert.Equal(7, new KybMisdeclarationSettings().MinorCorrectionDays);

    [Fact]
    public void Controller_RequiresAdminPolicy()
    {
        var attribute = typeof(AdminLotMisdeclarationController).GetCustomAttribute<AuthorizeAttribute>();
        Assert.NotNull(attribute);
        Assert.Equal("RequireAdmin", attribute.Policy);
    }
}
