using System.Reflection;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging.Abstractions;
using ParkingManagement.SharedKernel.Contracts;
using ParkingManagement.SharedKernel.Domain;
using ParkingManagement.SharedKernel.Enums;
using ParkingManagement.SharedKernel.Exceptions;
using ParkingService.API.Controllers;
using ParkingService.Application.Abstractions;
using ParkingService.Application.Features.OperatingHours;
using ParkingService.Domain.Entities;

namespace ParkingService.Test;

public class LotOperatingHourTests
{
    private static T WithId<T>(T entity, int id) where T : BaseEntity
    {
        typeof(BaseEntity).GetProperty(nameof(BaseEntity.Id), BindingFlags.Public | BindingFlags.Instance)!
            .SetValue(entity, id);
        return entity;
    }

    private static ParkingLot CreateTestLot(
        int id = 1,
        int ownerProfileId = 10,
        TimeOnly? openTime = null,
        TimeOnly? closeTime = null,
        ParkingLotStatus status = ParkingLotStatus.Active)
    {
        var lot = new ParkingLot(
            id: id,
            name: "Bãi xe Bitexco Test",
            address: "2 Hải Triều, Bến Nghé, Quận 1",
            latitude: 10.7719,
            longitude: 106.7044,
            totalSlots: 100,
            availableSlots: 80,
            maxHeightCm: 220,
            openTime: openTime ?? new TimeOnly(6, 0),
            closeTime: closeTime ?? new TimeOnly(22, 0),
            status: status,
            ownerProfileId: ownerProfileId,
            integrationTier: IntegrationTier.Manual);

        return WithId(lot, id);
    }

    // ==================== 1. GET LOT OPERATING HOURS USE CASE ====================

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public async Task GetLotOperatingHours_WithInvalidLotId_ThrowsValidationException(int lotId)
    {
        var lotRepo = new FakeParkingLotRepo();
        var hourRepo = new FakeOperatingHourRepo();
        var useCase = new GetLotOperatingHoursUseCase(lotRepo, hourRepo, NullLogger<GetLotOperatingHoursUseCase>.Instance);

        await Assert.ThrowsAsync<ValidationException>(() => useCase.ExecuteAsync(lotId));
    }

    [Fact]
    public async Task GetLotOperatingHours_WhenLotNotFound_ThrowsNotFoundException()
    {
        var lotRepo = new FakeParkingLotRepo();
        var hourRepo = new FakeOperatingHourRepo();
        var useCase = new GetLotOperatingHoursUseCase(lotRepo, hourRepo, NullLogger<GetLotOperatingHoursUseCase>.Instance);

        await Assert.ThrowsAsync<NotFoundException>(() => useCase.ExecuteAsync(999));
    }

    [Fact]
    public async Task GetLotOperatingHours_WhenCallerNotOwner_ThrowsConflictException()
    {
        var lotRepo = new FakeParkingLotRepo();
        var hourRepo = new FakeOperatingHourRepo();
        var lot = CreateTestLot(id: 1, ownerProfileId: 10);
        await lotRepo.AddAsync(lot);

        var useCase = new GetLotOperatingHoursUseCase(lotRepo, hourRepo, NullLogger<GetLotOperatingHoursUseCase>.Instance);

        await Assert.ThrowsAsync<ConflictException>(() => useCase.ExecuteAsync(1, ownerProfileId: 99));
    }

    [Fact]
    public async Task GetLotOperatingHours_WhenNoCustomSchedule_ReturnsAll7DaysWithIsDefaultTrue()
    {
        var lotRepo = new FakeParkingLotRepo();
        var hourRepo = new FakeOperatingHourRepo();
        var lot = CreateTestLot(id: 1, ownerProfileId: 10, openTime: new TimeOnly(6, 0), closeTime: new TimeOnly(22, 0));
        await lotRepo.AddAsync(lot);

        var useCase = new GetLotOperatingHoursUseCase(lotRepo, hourRepo, NullLogger<GetLotOperatingHoursUseCase>.Instance);

        var result = await useCase.ExecuteAsync(1, ownerProfileId: 10);

        Assert.NotNull(result);
        Assert.Equal(1, result.ParkingLotId);
        Assert.Equal("Bãi xe Bitexco Test", result.ParkingLotName);
        Assert.Equal(new TimeOnly(6, 0), result.DefaultOpenTime);
        Assert.Equal(new TimeOnly(22, 0), result.DefaultCloseTime);
        Assert.Equal(7, result.Schedule.Count);

        // Thứ tự từ Thứ Hai đến Chủ Nhật
        Assert.Equal(DayOfWeek.Monday, result.Schedule[0].DayOfWeek);
        Assert.Equal("Thứ Hai", result.Schedule[0].DayName);
        Assert.Equal(DayOfWeek.Sunday, result.Schedule[6].DayOfWeek);
        Assert.Equal("Chủ Nhật", result.Schedule[6].DayName);

        // Tất cả 7 ngày đều dùng mặc định
        Assert.All(result.Schedule, day =>
        {
            Assert.True(day.IsDefault);
            Assert.False(day.IsClosed);
            Assert.Equal(new TimeOnly(6, 0), day.OpenTime);
            Assert.Equal(new TimeOnly(22, 0), day.CloseTime);
            Assert.False(day.IsOvernight);
            Assert.False(day.Is24Hours);
        });
    }

    [Fact]
    public async Task GetLotOperatingHours_WhenPartialCustomSchedule_ReturnsMergedSchedule()
    {
        var lotRepo = new FakeParkingLotRepo();
        var hourRepo = new FakeOperatingHourRepo();
        var lot = CreateTestLot(id: 1, ownerProfileId: 10, openTime: new TimeOnly(6, 0), closeTime: new TimeOnly(22, 0));
        await lotRepo.AddAsync(lot);

        // Cấu hình riêng cho Chủ Nhật: 07:00 - 21:00
        var sundayHour = WithId(new LotOperatingHour
        {
            ParkingLotId = 1,
            DayOfWeek = DayOfWeek.Sunday,
            OpenTime = new TimeOnly(7, 0),
            CloseTime = new TimeOnly(21, 0),
            IsClosed = false
        }, 1);
        await hourRepo.ReplaceScheduleAsync(1, [sundayHour]);

        var useCase = new GetLotOperatingHoursUseCase(lotRepo, hourRepo, NullLogger<GetLotOperatingHoursUseCase>.Instance);

        var result = await useCase.ExecuteAsync(1, ownerProfileId: 10);

        Assert.Equal(7, result.Schedule.Count);

        // T2-T7 dùng mặc định
        var monday = result.Schedule.First(s => s.DayOfWeek == DayOfWeek.Monday);
        Assert.True(monday.IsDefault);
        Assert.Equal(new TimeOnly(6, 0), monday.OpenTime);

        // Chủ nhật dùng cấu hình riêng
        var sunday = result.Schedule.First(s => s.DayOfWeek == DayOfWeek.Sunday);
        Assert.False(sunday.IsDefault);
        Assert.Equal(new TimeOnly(7, 0), sunday.OpenTime);
        Assert.Equal(new TimeOnly(21, 0), sunday.CloseTime);
    }

    [Fact]
    public async Task GetLotOperatingHours_WhenLotIs24Hours_CorrectlyFlagsIs24Hours()
    {
        var lotRepo = new FakeParkingLotRepo();
        var hourRepo = new FakeOperatingHourRepo();
        var lot = CreateTestLot(id: 1, ownerProfileId: 10, openTime: new TimeOnly(0, 0), closeTime: new TimeOnly(0, 0));
        await lotRepo.AddAsync(lot);

        var useCase = new GetLotOperatingHoursUseCase(lotRepo, hourRepo, NullLogger<GetLotOperatingHoursUseCase>.Instance);

        var result = await useCase.ExecuteAsync(1);

        Assert.All(result.Schedule, day =>
        {
            Assert.True(day.Is24Hours);
            Assert.False(day.IsOvernight);
        });
    }

    [Fact]
    public async Task GetLotOperatingHours_WhenCustomDayIsClosed_CorrectlyFlagsIsClosed()
    {
        var lotRepo = new FakeParkingLotRepo();
        var hourRepo = new FakeOperatingHourRepo();
        var lot = CreateTestLot(id: 1, ownerProfileId: 10);
        await lotRepo.AddAsync(lot);

        var sundayHour = WithId(new LotOperatingHour
        {
            ParkingLotId = 1,
            DayOfWeek = DayOfWeek.Sunday,
            OpenTime = new TimeOnly(0, 0),
            CloseTime = new TimeOnly(0, 0),
            IsClosed = true
        }, 1);
        await hourRepo.ReplaceScheduleAsync(1, [sundayHour]);

        var useCase = new GetLotOperatingHoursUseCase(lotRepo, hourRepo, NullLogger<GetLotOperatingHoursUseCase>.Instance);

        var result = await useCase.ExecuteAsync(1);
        var sunday = result.Schedule.First(s => s.DayOfWeek == DayOfWeek.Sunday);

        Assert.True(sunday.IsClosed);
        Assert.False(sunday.IsDefault);
        Assert.False(sunday.Is24Hours);
        Assert.False(sunday.IsOvernight);
    }

    [Fact]
    public async Task GetLotOperatingHours_WhenCustomDayIsOvernight_CorrectlyFlagsIsOvernight()
    {
        var lotRepo = new FakeParkingLotRepo();
        var hourRepo = new FakeOperatingHourRepo();
        var lot = CreateTestLot(id: 1, ownerProfileId: 10);
        await lotRepo.AddAsync(lot);

        // Thứ Sáu mở xuyên đêm 18:00 - 05:00 hôm sau
        var fridayHour = WithId(new LotOperatingHour
        {
            ParkingLotId = 1,
            DayOfWeek = DayOfWeek.Friday,
            OpenTime = new TimeOnly(18, 0),
            CloseTime = new TimeOnly(5, 0),
            IsClosed = false
        }, 1);
        await hourRepo.ReplaceScheduleAsync(1, [fridayHour]);

        var useCase = new GetLotOperatingHoursUseCase(lotRepo, hourRepo, NullLogger<GetLotOperatingHoursUseCase>.Instance);

        var result = await useCase.ExecuteAsync(1);
        var friday = result.Schedule.First(s => s.DayOfWeek == DayOfWeek.Friday);

        Assert.True(friday.IsOvernight);
        Assert.False(friday.Is24Hours);
        Assert.False(friday.IsClosed);
        Assert.False(friday.IsDefault);
    }

    // ==================== 2. UPDATE LOT OPERATING HOURS USE CASE ====================

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public async Task UpdateLotOperatingHours_WithInvalidLotId_ThrowsValidationException(int lotId)
    {
        var lotRepo = new FakeParkingLotRepo();
        var hourRepo = new FakeOperatingHourRepo();
        var getUseCase = new GetLotOperatingHoursUseCase(lotRepo, hourRepo, NullLogger<GetLotOperatingHoursUseCase>.Instance);
        var updateUseCase = new UpdateLotOperatingHoursUseCase(lotRepo, hourRepo, getUseCase, NullLogger<UpdateLotOperatingHoursUseCase>.Instance);

        var command = new UpdateLotOperatingHoursCommand(lotId, 10, []);

        await Assert.ThrowsAsync<ValidationException>(() => updateUseCase.ExecuteAsync(command));
    }

    [Fact]
    public async Task UpdateLotOperatingHours_WithNullSchedule_ThrowsValidationException()
    {
        var lotRepo = new FakeParkingLotRepo();
        var hourRepo = new FakeOperatingHourRepo();
        var getUseCase = new GetLotOperatingHoursUseCase(lotRepo, hourRepo, NullLogger<GetLotOperatingHoursUseCase>.Instance);
        var updateUseCase = new UpdateLotOperatingHoursUseCase(lotRepo, hourRepo, getUseCase, NullLogger<UpdateLotOperatingHoursUseCase>.Instance);

        var command = new UpdateLotOperatingHoursCommand(1, 10, null!);

        await Assert.ThrowsAsync<ValidationException>(() => updateUseCase.ExecuteAsync(command));
    }

    [Fact]
    public async Task UpdateLotOperatingHours_WhenLotNotFound_ThrowsNotFoundException()
    {
        var lotRepo = new FakeParkingLotRepo();
        var hourRepo = new FakeOperatingHourRepo();
        var getUseCase = new GetLotOperatingHoursUseCase(lotRepo, hourRepo, NullLogger<GetLotOperatingHoursUseCase>.Instance);
        var updateUseCase = new UpdateLotOperatingHoursUseCase(lotRepo, hourRepo, getUseCase, NullLogger<UpdateLotOperatingHoursUseCase>.Instance);

        var command = new UpdateLotOperatingHoursCommand(999, 10, []);

        await Assert.ThrowsAsync<NotFoundException>(() => updateUseCase.ExecuteAsync(command));
    }

    [Fact]
    public async Task UpdateLotOperatingHours_WhenCallerNotOwner_ThrowsConflictException()
    {
        var lotRepo = new FakeParkingLotRepo();
        var hourRepo = new FakeOperatingHourRepo();
        var lot = CreateTestLot(id: 1, ownerProfileId: 10);
        await lotRepo.AddAsync(lot);

        var getUseCase = new GetLotOperatingHoursUseCase(lotRepo, hourRepo, NullLogger<GetLotOperatingHoursUseCase>.Instance);
        var updateUseCase = new UpdateLotOperatingHoursUseCase(lotRepo, hourRepo, getUseCase, NullLogger<UpdateLotOperatingHoursUseCase>.Instance);

        var command = new UpdateLotOperatingHoursCommand(1, OwnerProfileId: 99, []);

        await Assert.ThrowsAsync<ConflictException>(() => updateUseCase.ExecuteAsync(command));
    }

    [Fact]
    public async Task UpdateLotOperatingHours_WhenDuplicateDaysInRequest_ThrowsValidationException()
    {
        var lotRepo = new FakeParkingLotRepo();
        var hourRepo = new FakeOperatingHourRepo();
        var lot = CreateTestLot(id: 1, ownerProfileId: 10);
        await lotRepo.AddAsync(lot);

        var getUseCase = new GetLotOperatingHoursUseCase(lotRepo, hourRepo, NullLogger<GetLotOperatingHoursUseCase>.Instance);
        var updateUseCase = new UpdateLotOperatingHoursUseCase(lotRepo, hourRepo, getUseCase, NullLogger<UpdateLotOperatingHoursUseCase>.Instance);

        var command = new UpdateLotOperatingHoursCommand(1, 10,
        [
            new UpdateLotOperatingHourItemRequest(DayOfWeek.Monday, new TimeOnly(6, 0), new TimeOnly(22, 0)),
            new UpdateLotOperatingHourItemRequest(DayOfWeek.Monday, new TimeOnly(7, 0), new TimeOnly(23, 0)) // Trùng Monday
        ]);

        var ex = await Assert.ThrowsAsync<ValidationException>(() => updateUseCase.ExecuteAsync(command));
        Assert.Contains("trùng lặp", ex.Message);
    }

    [Fact]
    public async Task UpdateLotOperatingHours_WithValidSchedule_ReplacesAndSavesSchedule()
    {
        var lotRepo = new FakeParkingLotRepo();
        var hourRepo = new FakeOperatingHourRepo();
        var lot = CreateTestLot(id: 1, ownerProfileId: 10, openTime: new TimeOnly(6, 0), closeTime: new TimeOnly(22, 0));
        await lotRepo.AddAsync(lot);

        var getUseCase = new GetLotOperatingHoursUseCase(lotRepo, hourRepo, NullLogger<GetLotOperatingHoursUseCase>.Instance);
        var updateUseCase = new UpdateLotOperatingHoursUseCase(lotRepo, hourRepo, getUseCase, NullLogger<UpdateLotOperatingHoursUseCase>.Instance);

        var command = new UpdateLotOperatingHoursCommand(1, 10,
        [
            new UpdateLotOperatingHourItemRequest(DayOfWeek.Saturday, new TimeOnly(7, 0), new TimeOnly(23, 30)),
            new UpdateLotOperatingHourItemRequest(DayOfWeek.Sunday, new TimeOnly(0, 0), new TimeOnly(0, 0), IsClosed: true)
        ]);

        var result = await updateUseCase.ExecuteAsync(command);

        Assert.NotNull(result);
        Assert.Equal(7, result.Schedule.Count);

        var saturday = result.Schedule.First(s => s.DayOfWeek == DayOfWeek.Saturday);
        Assert.False(saturday.IsDefault);
        Assert.Equal(new TimeOnly(7, 0), saturday.OpenTime);
        Assert.Equal(new TimeOnly(23, 30), saturday.CloseTime);
        Assert.False(saturday.IsClosed);

        var sunday = result.Schedule.First(s => s.DayOfWeek == DayOfWeek.Sunday);
        Assert.False(sunday.IsDefault);
        Assert.True(sunday.IsClosed);

        var monday = result.Schedule.First(s => s.DayOfWeek == DayOfWeek.Monday);
        Assert.True(monday.IsDefault);
        Assert.Equal(new TimeOnly(6, 0), monday.OpenTime);

        // Kiểm tra trong repo đã được lưu
        var inRepo = await hourRepo.GetByParkingLotIdAsync(1);
        Assert.Equal(2, inRepo.Count);
    }

    [Fact]
    public async Task UpdateLotOperatingHours_WhenRemovingCustomDay_FallsBackToDefault()
    {
        var lotRepo = new FakeParkingLotRepo();
        var hourRepo = new FakeOperatingHourRepo();
        var lot = CreateTestLot(id: 1, ownerProfileId: 10, openTime: new TimeOnly(6, 0), closeTime: new TimeOnly(22, 0));
        await lotRepo.AddAsync(lot);

        // Ban đầu có cấu hình riêng cho T7 và CN
        await hourRepo.ReplaceScheduleAsync(1,
        [
            new LotOperatingHour { ParkingLotId = 1, DayOfWeek = DayOfWeek.Saturday, OpenTime = new TimeOnly(7, 0), CloseTime = new TimeOnly(23, 0) },
            new LotOperatingHour { ParkingLotId = 1, DayOfWeek = DayOfWeek.Sunday, OpenTime = new TimeOnly(8, 0), CloseTime = new TimeOnly(21, 0) }
        ]);

        var getUseCase = new GetLotOperatingHoursUseCase(lotRepo, hourRepo, NullLogger<GetLotOperatingHoursUseCase>.Instance);
        var updateUseCase = new UpdateLotOperatingHoursUseCase(lotRepo, hourRepo, getUseCase, NullLogger<UpdateLotOperatingHoursUseCase>.Instance);

        // Cập nhật lại chỉ giữ T7, bỏ CN để CN quay về giờ mặc định
        var command = new UpdateLotOperatingHoursCommand(1, 10,
        [
            new UpdateLotOperatingHourItemRequest(DayOfWeek.Saturday, new TimeOnly(7, 0), new TimeOnly(23, 0))
        ]);

        var result = await updateUseCase.ExecuteAsync(command);

        var saturday = result.Schedule.First(s => s.DayOfWeek == DayOfWeek.Saturday);
        Assert.False(saturday.IsDefault);

        var sunday = result.Schedule.First(s => s.DayOfWeek == DayOfWeek.Sunday);
        Assert.True(sunday.IsDefault); // Đã quay về dùng giờ mặc định
        Assert.Equal(new TimeOnly(6, 0), sunday.OpenTime);
    }

    // ==================== 3. DOMAIN LOGIC TESTS (ParkingLot.IsOpenAt) ====================

    [Fact]
    public void Domain_IsOpenAt_WithDayOfWeek_UsesCustomHoursWhenConfigured()
    {
        var lot = CreateTestLot(id: 1, openTime: new TimeOnly(6, 0), closeTime: new TimeOnly(22, 0));

        // Cấu hình Chủ Nhật mở muộn hơn: 08:00 - 20:00
        lot.OperatingHours.Add(new LotOperatingHour
        {
            ParkingLotId = 1,
            DayOfWeek = DayOfWeek.Sunday,
            OpenTime = new TimeOnly(8, 0),
            CloseTime = new TimeOnly(20, 0),
            IsClosed = false
        });

        // Chủ nhật lúc 07:00 -> đóng cửa (vì custom 8:00 mới mở)
        Assert.False(lot.IsOpenAt(DayOfWeek.Sunday, new TimeOnly(7, 0)));
        // Chủ nhật lúc 09:00 -> mở cửa
        Assert.True(lot.IsOpenAt(DayOfWeek.Sunday, new TimeOnly(9, 0)));
        // Chủ nhật lúc 21:00 -> đóng cửa
        Assert.False(lot.IsOpenAt(DayOfWeek.Sunday, new TimeOnly(21, 0)));

        // Thứ Hai không có custom -> dùng giờ mặc định (06:00 - 22:00)
        Assert.True(lot.IsOpenAt(DayOfWeek.Monday, new TimeOnly(7, 0)));
        Assert.True(lot.IsOpenAt(DayOfWeek.Monday, new TimeOnly(21, 0)));
        Assert.False(lot.IsOpenAt(DayOfWeek.Monday, new TimeOnly(23, 0)));
    }

    [Fact]
    public void Domain_IsOpenAt_WhenCustomDayIsClosed_ReturnsFalse()
    {
        var lot = CreateTestLot(id: 1, openTime: new TimeOnly(6, 0), closeTime: new TimeOnly(22, 0));
        lot.OperatingHours.Add(new LotOperatingHour
        {
            ParkingLotId = 1,
            DayOfWeek = DayOfWeek.Sunday,
            IsClosed = true
        });

        Assert.False(lot.IsOpenAt(DayOfWeek.Sunday, new TimeOnly(12, 0)));
    }

    [Fact]
    public void Domain_IsOpenAt_WhenCustomDayIs24Hours_ReturnsTrueAtAnyTime()
    {
        var lot = CreateTestLot(id: 1, openTime: new TimeOnly(6, 0), closeTime: new TimeOnly(22, 0));
        lot.OperatingHours.Add(new LotOperatingHour
        {
            ParkingLotId = 1,
            DayOfWeek = DayOfWeek.Saturday,
            OpenTime = new TimeOnly(0, 0),
            CloseTime = new TimeOnly(0, 0),
            IsClosed = false
        });

        Assert.True(lot.IsOpenAt(DayOfWeek.Saturday, new TimeOnly(2, 0)));
        Assert.True(lot.IsOpenAt(DayOfWeek.Saturday, new TimeOnly(14, 0)));
    }

    [Fact]
    public void Domain_IsOpenAt_WhenCustomDayIsOvernight_ReturnsTrueDuringNight()
    {
        var lot = CreateTestLot(id: 1, openTime: new TimeOnly(6, 0), closeTime: new TimeOnly(22, 0));
        lot.OperatingHours.Add(new LotOperatingHour
        {
            ParkingLotId = 1,
            DayOfWeek = DayOfWeek.Friday,
            OpenTime = new TimeOnly(19, 0),
            CloseTime = new TimeOnly(5, 0),
            IsClosed = false
        });

        Assert.True(lot.IsOpenAt(DayOfWeek.Friday, new TimeOnly(21, 0)));
        Assert.True(lot.IsOpenAt(DayOfWeek.Friday, new TimeOnly(3, 0)));
        Assert.False(lot.IsOpenAt(DayOfWeek.Friday, new TimeOnly(12, 0)));
    }

    [Fact]
    public void Domain_IsOpenAt_WithDateTime_EvaluatesCorrectDayAndHour()
    {
        var lot = CreateTestLot(id: 1, openTime: new TimeOnly(6, 0), closeTime: new TimeOnly(22, 0));
        lot.OperatingHours.Add(new LotOperatingHour
        {
            ParkingLotId = 1,
            DayOfWeek = DayOfWeek.Sunday,
            IsClosed = true
        });

        // Ngày 08/11/2026 là Chủ Nhật
        var sundayNoon = new DateTime(2026, 11, 8, 12, 0, 0, DateTimeKind.Utc);
        Assert.Equal(DayOfWeek.Sunday, sundayNoon.DayOfWeek);
        Assert.False(lot.IsOpenAt(sundayNoon));

        // Ngày 09/11/2026 là Thứ Hai
        var mondayNoon = new DateTime(2026, 11, 9, 12, 0, 0, DateTimeKind.Utc);
        Assert.Equal(DayOfWeek.Monday, mondayNoon.DayOfWeek);
        Assert.True(lot.IsOpenAt(mondayNoon));
    }

    // ==================== 4. REST CONTROLLER INTEGRATION TESTS ====================

    [Fact]
    public async Task Controller_Get_WhenCurrentUserIsOwner_ReturnsOkWithWeeklySchedule()
    {
        var lotRepo = new FakeParkingLotRepo();
        var hourRepo = new FakeOperatingHourRepo();
        var lot = CreateTestLot(id: 1, ownerProfileId: 10);
        await lotRepo.AddAsync(lot);

        var getUseCase = new GetLotOperatingHoursUseCase(lotRepo, hourRepo, NullLogger<GetLotOperatingHoursUseCase>.Instance);
        var updateUseCase = new UpdateLotOperatingHoursUseCase(lotRepo, hourRepo, getUseCase, NullLogger<UpdateLotOperatingHoursUseCase>.Instance);
        var currentUser = new FakeCurrentUser { OwnerProfileId = 10 };

        var controller = new LotOperatingHoursController(getUseCase, updateUseCase, currentUser);

        var actionResult = await controller.Get(lotId: 1, ownerProfileId: null, cancellationToken: CancellationToken.None);

        var okResult = Assert.IsType<OkObjectResult>(actionResult.Result);
        var dto = Assert.IsType<LotOperatingHoursWeeklyDto>(okResult.Value);
        Assert.Equal(1, dto.ParkingLotId);
        Assert.Equal(7, dto.Schedule.Count);
    }

    [Fact]
    public async Task Controller_Get_WhenOwnerProfileIdSpecifiedInQuery_OverridesCurrentUser()
    {
        var lotRepo = new FakeParkingLotRepo();
        var hourRepo = new FakeOperatingHourRepo();
        var lot = CreateTestLot(id: 1, ownerProfileId: 25);
        await lotRepo.AddAsync(lot);

        var getUseCase = new GetLotOperatingHoursUseCase(lotRepo, hourRepo, NullLogger<GetLotOperatingHoursUseCase>.Instance);
        var updateUseCase = new UpdateLotOperatingHoursUseCase(lotRepo, hourRepo, getUseCase, NullLogger<UpdateLotOperatingHoursUseCase>.Instance);
        var currentUser = new FakeCurrentUser { OwnerProfileId = 999 };

        var controller = new LotOperatingHoursController(getUseCase, updateUseCase, currentUser);

        var actionResult = await controller.Get(lotId: 1, ownerProfileId: 25, cancellationToken: CancellationToken.None);

        var okResult = Assert.IsType<OkObjectResult>(actionResult.Result);
        var dto = Assert.IsType<LotOperatingHoursWeeklyDto>(okResult.Value);
        Assert.Equal(1, dto.ParkingLotId);
    }

    [Fact]
    public async Task Controller_Update_WhenValidRequest_CallsUseCaseAndReturnsOk()
    {
        var lotRepo = new FakeParkingLotRepo();
        var hourRepo = new FakeOperatingHourRepo();
        var lot = CreateTestLot(id: 1, ownerProfileId: 10);
        await lotRepo.AddAsync(lot);

        var getUseCase = new GetLotOperatingHoursUseCase(lotRepo, hourRepo, NullLogger<GetLotOperatingHoursUseCase>.Instance);
        var updateUseCase = new UpdateLotOperatingHoursUseCase(lotRepo, hourRepo, getUseCase, NullLogger<UpdateLotOperatingHoursUseCase>.Instance);
        var currentUser = new FakeCurrentUser { OwnerProfileId = 10 };

        var controller = new LotOperatingHoursController(getUseCase, updateUseCase, currentUser);

        var request = new UpdateLotOperatingHoursRequest(
            OwnerProfileId: null,
            Schedule:
            [
                new UpdateLotOperatingHourItemRequest(DayOfWeek.Sunday, new TimeOnly(7, 0), new TimeOnly(21, 0))
            ]);

        var actionResult = await controller.Update(lotId: 1, request, CancellationToken.None);

        var okResult = Assert.IsType<OkObjectResult>(actionResult.Result);
        var dto = Assert.IsType<LotOperatingHoursWeeklyDto>(okResult.Value);
        Assert.Equal(1, dto.ParkingLotId);
    }

    // ==================== FAKE REPOSITORIES & DEPENDENCIES ====================

    private sealed class FakeParkingLotRepo : IParkingLotManagementRepository
    {
        private readonly List<ParkingLot> _lots = [];
        private int _nextId = 1;

        public Task<ParkingLot?> GetByIdAsync(int id, CancellationToken cancellationToken = default)
            => Task.FromResult(_lots.FirstOrDefault(l => l.Id == id));

        public Task<ParkingLot?> GetHierarchyAsync(int id, CancellationToken cancellationToken = default)
            => Task.FromResult(_lots.FirstOrDefault(l => l.Id == id));

        public Task<IReadOnlyList<ParkingLot>> GetByOwnerProfileIdAsync(int ownerProfileId, CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<ParkingLot>>(_lots.Where(l => l.OwnerProfileId == ownerProfileId).ToList());

        public Task<bool> ExistsByNameAsync(int ownerProfileId, string name, int? excludeId = null, CancellationToken cancellationToken = default)
            => Task.FromResult(_lots.Any(l => l.OwnerProfileId == ownerProfileId && l.Name == name && (!excludeId.HasValue || l.Id != excludeId.Value)));

        public Task AddAsync(ParkingLot lot, CancellationToken cancellationToken = default)
        {
            if (lot.Id == 0) WithId(lot, _nextId++);
            _lots.Add(lot);
            return Task.CompletedTask;
        }

        public Task UpdateAsync(ParkingLot lot, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task SaveChangesAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
    }

    private sealed class FakeOperatingHourRepo : ILotOperatingHourRepository
    {
        private readonly List<LotOperatingHour> _hours = [];
        private readonly List<LotOperatingHourOverride> _overrides = [];
        private int _nextId = 1;

        public Task<IReadOnlyList<LotOperatingHour>> GetByParkingLotIdAsync(int parkingLotId, bool trackChanges = false, CancellationToken cancellationToken = default)
        {
            return Task.FromResult<IReadOnlyList<LotOperatingHour>>(_hours.Where(h => h.ParkingLotId == parkingLotId).OrderBy(h => h.DayOfWeek).ToList());
        }

        public Task<IReadOnlyList<LotOperatingHourOverride>> GetOverridesByWeekAsync(int parkingLotId, int year, int weekOfYear, bool trackChanges = false, CancellationToken cancellationToken = default)
        {
            return Task.FromResult<IReadOnlyList<LotOperatingHourOverride>>(
                _overrides.Where(o => o.ParkingLotId == parkingLotId && o.Year == year && o.WeekOfYear == weekOfYear).OrderBy(o => o.DayOfWeek).ToList());
        }

        public Task ReplaceScheduleAsync(int parkingLotId, IEnumerable<LotOperatingHour> newSchedule, CancellationToken cancellationToken = default)
        {
            var newScheduleList = newSchedule.ToList();
            var incomingDays = newScheduleList.Select(s => s.DayOfWeek).ToHashSet();

            _hours.RemoveAll(h => h.ParkingLotId == parkingLotId && !incomingDays.Contains(h.DayOfWeek));

            foreach (var item in newScheduleList)
            {
                var matched = _hours.FirstOrDefault(h => h.ParkingLotId == parkingLotId && h.DayOfWeek == item.DayOfWeek);
                if (matched != null)
                {
                    matched.OpenTime = item.OpenTime;
                    matched.CloseTime = item.CloseTime;
                    matched.IsClosed = item.IsClosed;
                }
                else
                {
                    item.ParkingLotId = parkingLotId;
                    if (item.Id == 0) WithId(item, _nextId++);
                    _hours.Add(item);
                }
            }

            return Task.CompletedTask;
        }

        public Task ReplaceWeekOverridesAsync(int parkingLotId, int year, int weekOfYear, IEnumerable<LotOperatingHourOverride> overrides, CancellationToken cancellationToken = default)
        {
            var incoming = overrides.ToList();
            var incomingDays = incoming.Select(s => s.DayOfWeek).ToHashSet();
            _overrides.RemoveAll(o => o.ParkingLotId == parkingLotId && o.Year == year && o.WeekOfYear == weekOfYear && !incomingDays.Contains(o.DayOfWeek));
            foreach (var item in incoming)
            {
                var matched = _overrides.FirstOrDefault(o => o.ParkingLotId == parkingLotId && o.Year == year && o.WeekOfYear == weekOfYear && o.DayOfWeek == item.DayOfWeek);
                if (matched != null)
                {
                    matched.OpenTime = item.OpenTime;
                    matched.CloseTime = item.CloseTime;
                    matched.IsClosed = item.IsClosed;
                    matched.Reason = item.Reason;
                }
                else
                {
                    item.ParkingLotId = parkingLotId;
                    item.Year = year;
                    item.WeekOfYear = weekOfYear;
                    if (item.Id == 0) WithId(item, _nextId++);
                    _overrides.Add(item);
                }
            }
            return Task.CompletedTask;
        }

        public Task DeleteWeekOverridesAsync(int parkingLotId, int year, int weekOfYear, CancellationToken cancellationToken = default)
        {
            _overrides.RemoveAll(o => o.ParkingLotId == parkingLotId && o.Year == year && o.WeekOfYear == weekOfYear);
            return Task.CompletedTask;
        }

        public Task SaveChangesAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
    }

    private sealed class FakeCurrentUser : ICurrentUser
    {
        public int? UserId { get; set; } = 1;
        public string? Email { get; set; } = "test@owner.com";
        public IReadOnlyList<string> Roles { get; set; } = ["ParkingLotOwner"];
        public int? OwnerProfileId { get; set; } = 10;
        public bool IsAuthenticated { get; set; } = true;
    }
}
