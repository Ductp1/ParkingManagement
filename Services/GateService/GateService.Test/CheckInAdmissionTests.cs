using GateService.Application.Features.GateConsole;
using GateService.Application.Features.LotCapacity;
using GateService.Domain.Entities;
using GateService.Domain.Enums;
using ParkingManagement.SharedKernel.Enums;
using ParkingManagement.SharedKernel.Exceptions;

namespace GateService.Test;

/// <summary>
/// US-066 – tích hợp admission vào Check-in/Check-out: từ chối kèm message đúng theo lý do
/// (V >= B / T >= C); ghi AdmissionType vào lượt; lỗi tạo lượt sau khi nhận chỗ → trả lại bộ đếm;
/// check-out trừ đúng loại (luật 6). Fakes ở CheckInAdmissionFakes.cs.
/// </summary>
public class CheckInAdmissionTests
{
    [Fact]
    public async Task Transient_denied_when_buffer_full_conflicts_with_clear_message()
    {
        var gate = new FakeAdmissionGate(admitted: false, AdmissionRejectReasons.TransientBufferFull);
        var useCase = CheckInAdmissionFakes.CreateCheckIn(gate);

        var ex = await Assert.ThrowsAsync<ConflictException>(
            () => useCase.ExecuteAsync(new CheckInCommand(3, GateMethod.Manual, "51F-123.45")));

        Assert.Contains("vùng đệm vãng lai", ex.Message);
        var admit = Assert.Single(gate.AdmitCalls);
        Assert.Equal(3, admit.lotId);
        Assert.Equal(AdmissionType.Transient, admit.type);
    }

    [Fact]
    public async Task Lot_full_denial_conflicts_with_clear_message()
    {
        var gate = new FakeAdmissionGate(admitted: false, AdmissionRejectReasons.LotFull);
        var useCase = CheckInAdmissionFakes.CreateCheckIn(gate);

        var ex = await Assert.ThrowsAsync<ConflictException>(
            () => useCase.ExecuteAsync(new CheckInCommand(3, GateMethod.Manual, "51F-123.45")));

        Assert.Contains("Bãi đã đầy", ex.Message);
    }

    [Fact]
    public async Task Walk_in_check_in_records_transient_and_publishes()
    {
        var gate = new FakeAdmissionGate(admitted: true);
        var writer = new RecordingSessionWriter();
        var publisher = new RecordingCapacityPublisher();
        var useCase = CheckInAdmissionFakes.CreateCheckIn(gate, writer, publisher);

        var dto = await useCase.ExecuteAsync(new CheckInCommand(3, GateMethod.Manual, "51F-123.45"));

        Assert.Equal(AdmissionType.Transient, writer.Sessions.Single().AdmissionType);
        Assert.Contains(3, publisher.Calls);
        Assert.Equal("51F12345", dto.PlateNumber);
    }

    [Fact]
    public async Task Booking_check_in_records_subscriber_type()
    {
        var gate = new FakeAdmissionGate(admitted: true);
        var writer = new RecordingSessionWriter();
        var useCase = CheckInAdmissionFakes.CreateCheckIn(gate, writer);

        var dto = await useCase.ExecuteAsync(new CheckInCommand(3, GateMethod.BookingCode, "30A-123.45", BookingCode: "bk-0009"));

        Assert.Equal("BK-0009", dto.BookingCode);
        Assert.Equal(AdmissionType.Subscriber, writer.Sessions.Single().AdmissionType);
    }

    [Fact]
    public async Task Session_write_failure_after_admission_releases_counter()
    {
        var gate = new FakeAdmissionGate(admitted: true);
        var useCase = CheckInAdmissionFakes.CreateCheckIn(gate, new ThrowingSessionWriter());

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => useCase.ExecuteAsync(new CheckInCommand(3, GateMethod.Manual, "51F-123.45")));

        var release = Assert.Single(gate.ReleaseCalls);
        Assert.Equal(3, release.lotId);
        Assert.Equal(AdmissionType.Transient, release.type);
    }

    [Fact]
    public async Task Check_out_releases_counter_by_recorded_admission_type()
    {
        var gate = new FakeAdmissionGate(admitted: true);
        var writer = new RecordingSessionWriter();
        writer.Sessions.Add(new ParkingSession
        {
            ParkingLotId = 3, PlateNumber = "51F12345", IsWalkIn = false, BookingCode = "BK-0009",
            EntryAtUtc = DateTime.UtcNow.AddHours(-1), CheckInMethod = GateMethod.BookingCode,
            Status = ParkingSessionStatus.Active, AdmissionType = AdmissionType.Subscriber,
        });
        var useCase = CheckInAdmissionFakes.CreateCheckOut(gate, writer);

        await useCase.ExecuteAsync(new CheckOutCommand(3, "51F12345"));

        var release = Assert.Single(gate.ReleaseCalls);
        Assert.Equal(AdmissionType.Subscriber, release.type);
    }
}