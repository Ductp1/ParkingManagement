using GateService.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using ParkingManagement.ServiceDefaults.Persistence;
using ParkingManagement.SharedKernel.Contracts;
using ParkingManagement.SharedKernel.Enums;

namespace GateService.Infrastructure.Persistence.Seeding;

/// <summary>1 ca trực đã đóng, PS-0001 (lượt của BK-0001 đã xong), PS-0002 (xe vãng lai 51A-999.99 đang đỗ ở bãi TSN).</summary>
public sealed class GateDataSeeder(ILogger<GateDataSeeder> logger) : IDataSeeder<GateDbContext>
{
    public async Task SeedAsync(GateDbContext db, CancellationToken cancellationToken)
    {
        await SeedCoreAsync(db, cancellationToken);
        await SeedExtrasAsync(db, cancellationToken);
    }

    /// <summary>Dữ liệu demo ban đầu (migration InitialCreate).</summary>
    private async Task SeedCoreAsync(GateDbContext db, CancellationToken cancellationToken)
    {
        if (await db.ParkingSessions.AnyAsync(cancellationToken)) return;
        var now = DateTime.UtcNow;
        var yesterday = now.Date.AddDays(-1);

        var shift = new Shift
        {
            ParkingLotId = DemoIds.LotVincom, OwnerProfileId = DemoIds.OwnerVincom, StaffUserId = DemoIds.StaffVincomUser,
            StartedAtUtc = yesterday, EndedAtUtc = yesterday.AddHours(8), Status = ShiftStatus.Closed,
            CheckInCount = 1, CheckOutCount = 1, HandoverNote = "Ca sáng bình thường."
        };
        var entry = yesterday.AddHours(1).AddMinutes(5);
        var exit = yesterday.AddHours(2).AddMinutes(50);
        var finished = new ParkingSession
        {
            Code = "PS-0001", ParkingLotId = DemoIds.LotVincom, OwnerProfileId = DemoIds.OwnerVincom,
            BookingId = DemoIds.BookingCompleted, BookingCode = "BK-0001", UserId = DemoIds.Driver1User, VehicleId = DemoIds.Driver1Car,
            SlotId = DemoIds.SlotVincomB1A01, SlotCode = "B1-A01", PlateNumber = "51F12345", VehicleType = VehicleType.Sedan,
            EntryAtUtc = entry, CheckInMethod = GateMethod.Qr, CheckedInByStaffId = DemoIds.StaffVincomUser,
            ExitAtUtc = exit, CheckOutMethod = GateMethod.Ocr, ExitOcrRaw = "51F-123.45", ExitOcrConfidence = 0.96,
            CheckedOutByStaffId = DemoIds.StaffVincomUser, Fee = 50000, Status = ParkingSessionStatus.Completed
        };
        finished.Events.Add(new GateEvent { ParkingLotId = DemoIds.LotVincom, Shift = shift, EventType = GateEventType.CheckIn, PlateNumber = "51F12345", PerformedByUserId = DemoIds.StaffVincomUser });
        finished.Events.Add(new GateEvent { ParkingLotId = DemoIds.LotVincom, Shift = shift, EventType = GateEventType.CheckOut, PlateNumber = "51F12345", OcrRaw = "51F-123.45", OcrConfidence = 0.96 });

        var walkIn = new ParkingSession
        {
            Code = "PS-0002", ParkingLotId = DemoIds.LotTsn, OwnerProfileId = DemoIds.OwnerTsn, IsWalkIn = true,
            SlotId = DemoIds.SlotTsnMdA01, SlotCode = "MD-A01", PlateNumber = "51A99999", VehicleType = VehicleType.Suv,
            EntryAtUtc = now.AddMinutes(-40), CheckInMethod = GateMethod.Ocr, EntryOcrRaw = "51A-999.99", EntryOcrConfidence = 0.93,
            Status = ParkingSessionStatus.Active
        };

        db.ParkingSessions.Add(finished);
        await db.SaveChangesAsync(cancellationToken);
        db.ParkingSessions.Add(walkIn);
        await db.SaveChangesAsync(cancellationToken);
        logger.LogInformation("Seed GateService xong: 2 lượt gửi xe, 1 ca trực.");
    }

    /// <summary>Dữ liệu demo cho các bảng thêm ở migration AddDocumentCoverage.</summary>
    private static async Task SeedExtrasAsync(GateDbContext db, CancellationToken cancellationToken)
    {
        if (await db.GateDevices.AnyAsync(cancellationToken)) return;
        var now = DateTime.UtcNow;

        db.GateDevices.AddRange(
            // Vincom – Mức 1: nhân viên quét QR, webcam chụp biển số cổng ra
            new GateDevice { ParkingLotId = DemoIds.LotVincom, Code = "VC-QR-IN-01", Name = "Máy quét QR cổng vào", DeviceType = GateDeviceType.QrScanner, Position = "Entry", LastSeenAtUtc = now },
            new GateDevice { ParkingLotId = DemoIds.LotVincom, Code = "VC-CAM-OUT-01", Name = "Webcam cổng ra", DeviceType = GateDeviceType.Webcam, Position = "Exit", LastSeenAtUtc = now },
            new GateDevice { ParkingLotId = DemoIds.LotVincom, Code = "VC-BAR-01", Name = "Barie cổng chung", DeviceType = GateDeviceType.Barrier, Position = "Entry", LastSeenAtUtc = now },
            // TSN – Mức 2: camera ANPR + barie tự động + kiosk
            new GateDevice { ParkingLotId = DemoIds.LotTsn, Code = "TSN-ANPR-IN-01", Name = "Camera ANPR cổng vào", DeviceType = GateDeviceType.AnprCamera, Position = "Entry", FirmwareVersion = "4.2.1", LastSeenAtUtc = now },
            new GateDevice { ParkingLotId = DemoIds.LotTsn, Code = "TSN-ANPR-OUT-01", Name = "Camera ANPR cổng ra", DeviceType = GateDeviceType.AnprCamera, Position = "Exit", FirmwareVersion = "4.2.1", LastSeenAtUtc = now },
            new GateDevice { ParkingLotId = DemoIds.LotTsn, Code = "TSN-KIOSK-01", Name = "Kiosk thanh toán VietQR", DeviceType = GateDeviceType.Kiosk, Position = "Exit",
                             Status = GateDeviceStatus.Offline, LastSeenAtUtc = now.AddMinutes(-12) });
        await db.SaveChangesAsync(cancellationToken);
    }
}
