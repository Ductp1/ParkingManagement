using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using ParkingManagement.ServiceDefaults.Persistence;
using ParkingManagement.SharedKernel.Contracts;
using ParkingManagement.SharedKernel.Enums;
using ParkingService.Domain.Entities;

namespace ParkingService.Infrastructure.Persistence.Seeding;

/// <summary>
/// Dữ liệu demo của ParkingService: 4 bãi, 192 slot kèm sơ đồ layout JSON.
/// Mỗi bãi được lưu riêng theo thứ tự để Id khớp DemoIds (Vincom = 1, slot 1..80; TSN = 2, slot 81..140...).
/// </summary>
public sealed class ParkingDataSeeder(ILogger<ParkingDataSeeder> logger) : IDataSeeder<ParkingDbContext>
{
    public async Task SeedAsync(ParkingDbContext db, CancellationToken cancellationToken)
    {
        await SeedCoreAsync(db, cancellationToken);
        await SeedExtrasAsync(db, cancellationToken);
    }

    /// <summary>Dữ liệu demo ban đầu (migration InitialCreate).</summary>
    private async Task SeedCoreAsync(ParkingDbContext db, CancellationToken cancellationToken)
    {
        if (await db.ParkingLots.IgnoreQueryFilters().AnyAsync(cancellationToken)) return;
        var now = DateTime.UtcNow;

        var lots = new (ParkingLot Lot, string ZoneCode, string ZoneName, bool Outdoor, (string, int, int)[] Floors, int OversizedEvery)[]
        {
            (new ParkingLot(0, "Bãi xe Vincom Đồng Khởi", "72 Lê Thánh Tôn, Q.1, TP.HCM", 10.7781, 106.7019, 1, 1, 210,
                new TimeOnly(6, 0), new TimeOnly(23, 0), ParkingLotStatus.Active, DemoIds.OwnerVincom, IntegrationTier.PmsApi,
                "Hầm 2 tầng ngay trung tâm Quận 1, có bảo vệ 24/7.", "02838000001"),
             "B", "Hầm B", false, [("B1", -1, 10), ("B2", -2, 10)], 0),
            (new ParkingLot(0, "Bãi xe Sân bay Tân Sơn Nhất (P2)", "Trường Sơn, Q.Tân Bình, TP.HCM", 10.8136, 106.6621, 1, 1, 250,
                new TimeOnly(0, 0), new TimeOnly(0, 0), ParkingLotStatus.Active, DemoIds.OwnerTsn, IntegrationTier.EdgeIot,
                "Bãi ngoài trời 24/7, nhận xe quá khổ, có camera ANPR.", "02838000002"),
             "P2", "Bãi P2", true, [("Mặt đất", 0, 15)], 15),
            (new ParkingLot(0, "Bãi xe Landmark 81 (B2)", "720A Điện Biên Phủ, Q.Bình Thạnh, TP.HCM", 10.7950, 106.7218, 1, 1, 190,
                new TimeOnly(7, 0), new TimeOnly(22, 0), ParkingLotStatus.PendingApproval, DemoIds.OwnerVincom, IntegrationTier.Manual,
                "Đang chờ duyệt KYB.", "02838000003"),
             "B2", "Hầm B2", false, [("B2", -2, 8)], 0),
            (new ParkingLot(0, "Bãi xe Chợ Bến Thành (đêm)", "Lê Lợi, Q.1, TP.HCM", 10.7725, 106.6980, 1, 1, 300,
                new TimeOnly(18, 0), new TimeOnly(7, 0), ParkingLotStatus.Suspended, DemoIds.OwnerTsn, IntegrationTier.Manual,
                "Bãi đêm – đang bị tạm dừng do vi phạm SLA cấp 3.", "02838000004"),
             "N", "Bãi đêm", true, [("Mặt đất", 0, 5)], 0),
        };

        foreach (var (lot, zoneCode, zoneName, outdoor, floors, oversizedEvery) in lots)
        {
            BuildZone(lot, zoneCode, zoneName, outdoor, floors, oversizedEvery, now);
            var slots = lot.Zones.SelectMany(z => z.Floors).SelectMany(f => f.Slots).ToList();

            // Đồng bộ với dữ liệu demo của Gate/Booking: MD-A01 có xe vãng lai đang đỗ, MD-A03 đang được giữ chỗ.
            foreach (var s in slots)
            {
                if (lot.Name.Contains("Vincom") && s.Code is "B1-A01" or "B1-A02") s.State = SlotState.Available;
                if (lot.Name.Contains("Tân Sơn Nhất") && s.Code == "MD-A01") s.State = SlotState.Occupied;
                if (lot.Name.Contains("Tân Sơn Nhất") && s.Code == "MD-A03") s.State = SlotState.Reserved;
            }
            lot.SetSlotCounters(slots.Count, slots.Count(s => s.State == SlotState.Available));

            var manual = lot.IntegrationTier == IntegrationTier.Manual;
            var approved = lot.Status != ParkingLotStatus.PendingApproval;
            db.ParkingLots.Add(lot);
            db.LotCapacityConfigs.Add(new LotCapacityConfig
            {
                ParkingLot = lot, OnlineQuota = manual ? lot.TotalSlots / 2 : lot.TotalSlots,
                WalkInBufferPercent = manual ? 50 : 0, OnlineLockThreshold = 5, LastHeartbeatAtUtc = now,
                IsEmergencyStopped = lot.Status == ParkingLotStatus.Suspended,
                EmergencyReason = lot.Status == ParkingLotStatus.Suspended ? "Chế tài SLA cấp 3" : null
            });
            db.KybApplications.Add(new KybApplication
            {
                ParkingLot = lot, Status = approved ? KybStatus.Approved : KybStatus.Submitted,
                BusinessLicenseUrl = "/uploads/kyb/giay-phep-kinh-doanh.pdf",
                FireSafetyCertificateUrl = "/uploads/kyb/chung-nhan-pccc.pdf",
                SitePhotoUrlsJson = JsonSerializer.Serialize(new[] { "/uploads/kyb/hien-truong-1.jpg" }),
                PhotoLatitude = lot.Latitude, PhotoLongitude = lot.Longitude,
                FieldSurveyRequired = lot.TotalSlots > 50,
                SubmittedAtUtc = now.AddDays(approved ? -30 : -1),
                ReviewedByUserId = approved ? DemoIds.AdminUser : null,
                ReviewedAtUtc = approved ? now.AddDays(-28) : null
            });
            await db.SaveChangesAsync(cancellationToken);   // lưu từng bãi để Id tăng theo đúng thứ tự
        }

        var a01 = await db.Slots.Where(s => s.Code == "MD-A01" && s.Floor.Zone.ParkingLotId == DemoIds.LotTsn).Select(s => s.Id).SingleAsync(cancellationToken);
        if (a01 != DemoIds.SlotTsnMdA01)
            logger.LogWarning("Slot MD-A01 có Id {Actual}, khác DemoIds ({Expected}) – dữ liệu demo các service sẽ lệch.", a01, DemoIds.SlotTsnMdA01);

        logger.LogInformation("Seed ParkingService xong: {Lots} bãi, {Slots} slot.", lots.Length, await db.Slots.CountAsync(cancellationToken));
    }

    /// <summary>
    /// Sinh 1 zone với các tầng; mỗi tầng là lưới 6 hàng: Slot / Đường / Slot / Slot / Đường / Slot
    /// → 4 hàng slot × số cột. Đồng thời sinh LayoutVersion JSON đã publish cho tầng đó.
    /// </summary>
    private static void BuildZone(ParkingLot lot, string zoneCode, string zoneName, bool outdoor,
        (string Name, int Level, int Columns)[] floors, int oversizedEvery, DateTime now)
    {
        string[] rowPattern = ["slot", "road", "slot", "slot", "road", "slot"];
        var zone = new Zone { Code = zoneCode, Name = zoneName, IsOutdoor = outdoor };

        foreach (var (name, level, columns) in floors)
        {
            var floor = new Floor
            {
                Name = name, Level = level, GridColumns = columns, GridRows = rowPattern.Length,
                MaxHeightCm = lot.MaxHeightCm, MaxWeightKg = 2500
            };
            var prefix = level < 0 ? $"B{-level}" : "MD";
            var cells = new List<object>();
            var slotRow = 0;
            var index = 0;

            for (var y = 0; y < rowPattern.Length; y++)
            {
                if (rowPattern[y] == "road")
                {
                    for (var x = 0; x < columns; x++)
                    {
                        var type = (y == 1 && x == 0) ? "entry" : (y == 4 && x == columns - 1) ? "exit" : "road";
                        cells.Add(new { x, y, type });
                    }
                    continue;
                }

                var rowLetter = (char)('A' + slotRow++);
                for (var x = 0; x < columns; x++)
                {
                    index++;
                    var code = $"{prefix}-{rowLetter}{x + 1:00}";
                    var oversized = oversizedEvery > 0 && index % oversizedEvery == 0;
                    var state = (index % 7 == 0) ? SlotState.Reserved
                              : (index % 3 == 0) ? SlotState.Occupied
                              : (index == 2 * columns) ? SlotState.Maintenance
                              : SlotState.Available;
                    floor.Slots.Add(new Slot
                    {
                        Code = code, GridX = x, GridY = y, State = state, StateChangedAtUtc = now,
                        SlotType = oversized ? SlotType.Oversized : SlotType.Standard,
                        MaxVehicleType = oversized ? VehicleType.Oversized : VehicleType.Suv
                    });
                    cells.Add(new { x, y, type = "slot", code });
                }
            }

            floor.LayoutVersions.Add(new LayoutVersion
            {
                VersionNo = 1, IsPublished = true, PublishedAtUtc = now, CreatedByUserId = DemoIds.AdminUser,
                ChangeNote = "Sơ đồ khởi tạo",
                LayoutJson = JsonSerializer.Serialize(new { columns, rows = rowPattern.Length, cells })
            });
            zone.Floors.Add(floor);
        }

        lot.Zones.Add(zone);
    }

    /// <summary>Dữ liệu demo cho các bảng thêm ở migration AddDocumentCoverage – mỗi bảng kiểm tra riêng nên chạy được trên DB cũ.</summary>
    private static async Task SeedExtrasAsync(ParkingDbContext db, CancellationToken cancellationToken)
    {
        var now = DateTime.UtcNow;
        var lots = await db.ParkingLots.IgnoreQueryFilters().OrderBy(l => l.Id).ToListAsync(cancellationToken);
        if (lots.Count == 0) return;

        // Quận của 4 bãi demo (cột City/District mới thêm).
        string?[] districts = ["Quận 1", "Quận Tân Bình", "Quận Bình Thạnh", "Quận 1"];
        for (var i = 0; i < lots.Count && i < districts.Length; i++)
            if (lots[i].District is null) lots[i].SetLocation("TP.HCM", districts[i]);

        if (!await db.LotOperatingHours.AnyAsync(cancellationToken))
        {
            // Vincom: thứ 2–7 mở 06:00–23:00, Chủ nhật 07:00–22:00. TSN: 24/7 (để trống = dùng giờ mặc định của bãi).
            foreach (var day in Enum.GetValues<DayOfWeek>())
                db.LotOperatingHours.Add(new LotOperatingHour
                {
                    ParkingLotId = DemoIds.LotVincom, DayOfWeek = day,
                    OpenTime = day == DayOfWeek.Sunday ? new TimeOnly(7, 0) : new TimeOnly(6, 0),
                    CloseTime = day == DayOfWeek.Sunday ? new TimeOnly(22, 0) : new TimeOnly(23, 0)
                });
        }

        if (!await db.LotAmenities.AnyAsync(cancellationToken))
        {
            (int Lot, string Code, string Name)[] amenities =
            [
                (DemoIds.LotVincom, "COVERED", "Có mái che"), (DemoIds.LotVincom, "CCTV", "Camera an ninh"),
                (DemoIds.LotVincom, "SECURITY_24H", "Bảo vệ 24/7"), (DemoIds.LotVincom, "EV_CHARGER", "Trụ sạc xe điện"),
                (DemoIds.LotTsn, "CCTV", "Camera an ninh"), (DemoIds.LotTsn, "OVERSIZED", "Nhận xe quá khổ"),
                (DemoIds.LotTsn, "SHUTTLE", "Xe đưa đón ra nhà ga"),
                (DemoIds.LotLandmark, "COVERED", "Có mái che"), (DemoIds.LotLandmark, "CAR_WASH", "Rửa xe"),
            ];
            foreach (var (lot, code, name) in amenities)
                db.LotAmenities.Add(new LotAmenity { ParkingLotId = lot, Code = code, Name = name });
        }

        if (!await db.LotPhotos.AnyAsync(cancellationToken))
        {
            db.LotPhotos.AddRange(
                new LotPhoto { ParkingLotId = DemoIds.LotVincom, Url = "/uploads/lots/1/cong-vao.jpg", Caption = "Cổng vào hầm B", IsCover = true, SortOrder = 1 },
                new LotPhoto { ParkingLotId = DemoIds.LotVincom, Url = "/uploads/lots/1/ham-b1.jpg", Caption = "Hầm B1", SortOrder = 2 },
                new LotPhoto { ParkingLotId = DemoIds.LotTsn, Url = "/uploads/lots/2/bai-p2.jpg", Caption = "Bãi P2 nhìn từ trên cao", IsCover = true, SortOrder = 1 });
        }

        if (!await db.ClosureSchedules.AnyAsync(cancellationToken))
        {
            var floorB2 = await db.Floors.Where(f => f.Zone.ParkingLotId == DemoIds.LotVincom && f.Name == "B2").Select(f => f.Id).FirstOrDefaultAsync(cancellationToken);
            db.ClosureSchedules.Add(new ClosureSchedule
            {
                ParkingLotId = DemoIds.LotVincom, TargetType = ClosureTargetType.Floor, TargetId = floorB2,
                StartsAtUtc = now.Date.AddDays(7).AddHours(15), EndsAtUtc = now.Date.AddDays(7).AddHours(23),   // 22:00 → 06:00 giờ VN
                Reason = "Bảo trì hệ thống PCCC tầng B2", CreatedByUserId = DemoIds.OwnerVincomUser
            });
            db.ClosureSchedules.Add(new ClosureSchedule
            {
                ParkingLotId = DemoIds.LotBenThanh, TargetType = ClosureTargetType.Lot, TargetId = DemoIds.LotBenThanh,
                StartsAtUtc = now.AddDays(-2), EndsAtUtc = null, IsEmergency = true, AffectedBookingsNotified = true,
                Reason = "Tạm dừng do chế tài SLA cấp 3", CreatedByUserId = DemoIds.AdminUser
            });
        }

        if (!await db.LotIntegrations.AnyAsync(cancellationToken))
        {
            // Vincom là bãi Mức 1 (PMS). API key gốc chỉ hiện 1 lần khi tạo; DB chỉ giữ prefix + hash.
            db.LotIntegrations.Add(new LotIntegration
            {
                ParkingLotId = DemoIds.LotVincom, ProviderName = "ParkPro PMS 3.2", ApiKeyPrefix = "pk_vincom01",
                ApiKeyHash = "SHA256:3f1c9d0a7b5e4c2f8a6d1e9b0c7f5a3d2e1b4c6a8f0d9e7c5b3a1f2e4d6c8b0a",
                WebhookUrl = "https://pms.vincom-parking.example/api/smartparking/bookings",
                Status = IntegrationStatus.Active, LastSyncAtUtc = now.AddMinutes(-1)
            });
        }

        if (!await db.SlotStateLogs.AnyAsync(cancellationToken))
        {
            db.SlotStateLogs.AddRange(
                new SlotStateLog { SlotId = DemoIds.SlotTsnMdA01, FromState = SlotState.Available, ToState = SlotState.Occupied, Source = SlotStateSource.Gate,
                                   ParkingSessionId = DemoIds.SessionWalkIn, Reason = "Xe vãng lai 51A-999.99 vào bãi (ANPR)" },
                new SlotStateLog { SlotId = DemoIds.SlotTsnMdA03, FromState = SlotState.Available, ToState = SlotState.Reserved, Source = SlotStateSource.Booking,
                                   BookingId = DemoIds.BookingPending, Reason = "Giữ chỗ 15 phút cho BK-0003" },
                new SlotStateLog { SlotId = DemoIds.SlotVincomB1A01, FromState = SlotState.Occupied, ToState = SlotState.Available, Source = SlotStateSource.Gate,
                                   BookingId = DemoIds.BookingCompleted, ParkingSessionId = DemoIds.SessionCompleted, Reason = "BK-0001 check-out" });
        }

        if (!await db.ExternalParkingLots.IgnoreQueryFilters().AnyAsync(cancellationToken))
        {
            db.ExternalParkingLots.AddRange(
                new ExternalParkingLot { Name = "Bãi xe Công viên 23/9", Address = "Phạm Ngũ Lão, Q.1, TP.HCM", City = "TP.HCM",
                                         Latitude = 10.7685, Longitude = 106.6935, ReferencePricePerHour = 25000, OpeningHoursText = "06:00 – 22:00",
                                         Source = "Khảo sát thực địa 09/2026", ImportedByUserId = DemoIds.AdminUser },
                new ExternalParkingLot { Name = "Bãi xe Nhà hát Thành phố", Address = "Công trường Lam Sơn, Q.1, TP.HCM", City = "TP.HCM",
                                         Latitude = 10.7766, Longitude = 106.7031, ReferencePricePerHour = 30000, OpeningHoursText = "24/7",
                                         Source = "Khảo sát thực địa 09/2026", ImportedByUserId = DemoIds.AdminUser });
        }

        await db.SaveChangesAsync(cancellationToken);
    }
}
