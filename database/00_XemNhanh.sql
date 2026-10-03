-- =====================================================================
-- XEM NHANH 9 DATABASE CỦA PARKINGMANAGEMENT (mở trong SSMS, bấm F5)
-- Mỗi service 1 database: PM_UserDb, PM_VehicleDb, PM_ParkingDb, PM_BookingDb, PM_PaymentDb,
-- PM_NotificationDb, PM_GateDb, PM_AdminDb, PM_SupportDb.
-- Muốn xem riêng 1 phần: bôi đen khối đó rồi bấm F5.
-- =====================================================================
SET NOCOUNT ON;

-- 0) Danh sách 9 database + số bảng + số khóa ngoại + số dòng dữ liệu ----------------------------
DECLARE @sql nvarchar(max) = N'';
SELECT @sql += N'UNION ALL SELECT N''' + name + N''' AS [Database],
    (SELECT COUNT(*) FROM ' + QUOTENAME(name) + N'.sys.tables WHERE name NOT IN (''__EFMigrationsHistory'',''OutboxMessages'')) AS [Số bảng],
    (SELECT COUNT(*) FROM ' + QUOTENAME(name) + N'.sys.foreign_keys) AS [Khóa ngoại],
    (SELECT SUM(p.rows) FROM ' + QUOTENAME(name) + N'.sys.partitions p JOIN ' + QUOTENAME(name) + N'.sys.tables t ON t.object_id = p.object_id
       WHERE p.index_id IN (0,1) AND t.name NOT IN (''__EFMigrationsHistory'',''OutboxMessages'')) AS [Tổng số dòng] '
FROM sys.databases WHERE name LIKE 'PM[_]%Db' ORDER BY name;
SET @sql = STUFF(@sql, 1, 10, N'');
EXEC (@sql);

-- 1) Từng bảng trong 9 database + số dòng --------------------------------------------------------
SET @sql = N'';
SELECT @sql += N'UNION ALL SELECT N''' + name + N''' AS [Database], t.name AS [Bảng], SUM(p.rows) AS [Số dòng]
    FROM ' + QUOTENAME(name) + N'.sys.tables t JOIN ' + QUOTENAME(name) + N'.sys.partitions p ON p.object_id = t.object_id AND p.index_id IN (0,1)
    WHERE t.name <> ''__EFMigrationsHistory'' GROUP BY t.name '
FROM sys.databases WHERE name LIKE 'PM[_]%Db' ORDER BY name;
SET @sql = STUFF(@sql, 1, 10, N'') + N' ORDER BY [Database], [Bảng]';
EXEC (@sql);

-- 2) UserService – PM_UserDb ----------------------------------------------------------------------
SELECT u.Id, u.FullName, u.Email, u.Status, u.KycStatus, STRING_AGG(r.Role, ', ') AS Roles
FROM PM_UserDb.dbo.Users u LEFT JOIN PM_UserDb.dbo.UserRoles r ON r.UserId = u.Id
GROUP BY u.Id, u.FullName, u.Email, u.Status, u.KycStatus ORDER BY u.Id;

SELECT o.Id AS OwnerProfileId, o.BusinessName, u.Email AS TaiKhoan, o.BankName, o.BankAccountNumber, o.CommissionRateOverride
FROM PM_UserDb.dbo.OwnerProfiles o JOIN PM_UserDb.dbo.Users u ON u.Id = o.UserId;

-- 3) VehicleService – PM_VehicleDb ----------------------------------------------------------------
SELECT Id, UserId, PlateDisplay, VehicleType, FuelType, Brand, Model, HeightCm, IsDefault FROM PM_VehicleDb.dbo.Vehicles;

-- 4) ParkingService – PM_ParkingDb: bãi → khu → tầng → slot ---------------------------------------
SELECT Id, Name, Status, IntegrationTier, OwnerProfileId, TotalSlots, AvailableSlots, MaxHeightCm, OpenTime, CloseTime
FROM PM_ParkingDb.dbo.ParkingLots;

SELECT l.Name AS Bai, z.Name AS Khu, f.Name AS Tang, s.State, COUNT(*) AS SoSlot
FROM PM_ParkingDb.dbo.Slots s
JOIN PM_ParkingDb.dbo.Floors f ON f.Id = s.FloorId
JOIN PM_ParkingDb.dbo.Zones z ON z.Id = f.ZoneId
JOIN PM_ParkingDb.dbo.ParkingLots l ON l.Id = z.ParkingLotId
GROUP BY l.Name, z.Name, f.Name, s.State ORDER BY l.Name, f.Name, s.State;

SELECT k.ParkingLotId, l.Name, k.Status, k.SubmittedAtUtc, k.ReviewedAtUtc
FROM PM_ParkingDb.dbo.KybApplications k JOIN PM_ParkingDb.dbo.ParkingLots l ON l.Id = k.ParkingLotId;

-- 5) BookingService – PM_BookingDb ---------------------------------------------------------------
SELECT b.Code, b.Status, b.UserId, b.PlateNumber, b.ParkingLotName, b.SlotCode, b.StartAtUtc, b.EndAtUtc,
       b.TotalAmount, b.DiscountAmount, b.PaidAmount, b.PromotionCode, ps.FinalAmount AS GiaDaKhoa
FROM PM_BookingDb.dbo.Bookings b LEFT JOIN PM_BookingDb.dbo.PriceSnapshots ps ON ps.BookingId = b.Id;

SELECT b.Code, l.FromStatus, l.ToStatus, l.Reason, l.CreatedAtUtc
FROM PM_BookingDb.dbo.BookingStatusLogs l JOIN PM_BookingDb.dbo.Bookings b ON b.Id = l.BookingId ORDER BY b.Code, l.Id;

-- 6) PaymentService – PM_PaymentDb ---------------------------------------------------------------
SELECT rc.ParkingLotId, rr.VehicleType, rr.FromMinute, rr.ToMinute, rr.BlockMinutes, rr.PricePerBlock
FROM PM_PaymentDb.dbo.RateCards rc JOIN PM_PaymentDb.dbo.RateRules rr ON rr.RateCardId = rc.Id
ORDER BY rc.ParkingLotId, rr.VehicleType, rr.FromMinute;

SELECT p.Code, p.BookingCode, p.Method, p.Status, p.Amount, p.ProviderTransactionId, i.InvoiceNumber, p.PaidAtUtc
FROM PM_PaymentDb.dbo.Payments p LEFT JOIN PM_PaymentDb.dbo.Invoices i ON i.PaymentId = p.Id;

SELECT Code, Name, Sponsor, DiscountType, DiscountValue, MaxDiscountAmount, UsedCount, UsageLimit FROM PM_PaymentDb.dbo.Promotions;

-- 7) GateService – PM_GateDb ---------------------------------------------------------------------
SELECT Code, ParkingLotId, PlateNumber, IsWalkIn, BookingCode, SlotCode, Status, EntryAtUtc, ExitAtUtc, CheckInMethod, Fee
FROM PM_GateDb.dbo.ParkingSessions;

SELECT e.EventType, e.PlateNumber, e.OcrRaw, e.OcrConfidence, s.Code AS LuotGui, e.CreatedAtUtc
FROM PM_GateDb.dbo.GateEvents e LEFT JOIN PM_GateDb.dbo.ParkingSessions s ON s.Id = e.ParkingSessionId;

-- 8) NotificationService – PM_NotificationDb ------------------------------------------------------
SELECT n.UserId, n.Channel, n.Title, n.Body, n.Status, n.IsRead, t.[Key] AS MauThongBao
FROM PM_NotificationDb.dbo.Notifications n LEFT JOIN PM_NotificationDb.dbo.NotificationTemplates t ON t.Id = n.TemplateId;

-- 9) AdminService – PM_AdminDb -------------------------------------------------------------------
SELECT [Key], Value, DataType, Description FROM PM_AdminDb.dbo.SystemConfigs ORDER BY [Key];
SELECT [Key], IsEnabled, Description FROM PM_AdminDb.dbo.FeatureFlags ORDER BY [Key];
SELECT OwnerProfileId, ParkingLotId, Level, Reason, StartsAtUtc, EndsAtUtc, Status FROM PM_AdminDb.dbo.Sanctions;

-- 10) SupportService – PM_SupportDb ---------------------------------------------------------------
SELECT Code, Status, Category, UserId, BookingCode, ParkingLotId, Description, OwnerResponseDueAtUtc FROM PM_SupportDb.dbo.Complaints;
SELECT ParkingLotId, ReviewerName, Rating, Comment, CreatedAtUtc FROM PM_SupportDb.dbo.Reviews;

-- 11) Ghép dữ liệu giữa các service – CHỈ ĐỂ XEM / DEBUG trong SSMS ------------------------------
-- Trong code, service KHÔNG được JOIN sang database của service khác; phải gọi API qua Gateway.
-- Câu này kiểm tra ID lưu ở BookingDb có khớp với UserDb, VehicleDb, ParkingDb, PaymentDb hay không.
SELECT b.Code            AS Booking,
       u.FullName        AS TaiXe,          -- PM_UserDb
       v.PlateDisplay    AS Xe,             -- PM_VehicleDb
       l.Name            AS Bai,            -- PM_ParkingDb
       s.Code            AS Slot,           -- PM_ParkingDb
       b.Status,
       p.Code            AS ThanhToan,      -- PM_PaymentDb
       p.Amount          AS SoTien,
       g.Code            AS LuotGuiXe       -- PM_GateDb
FROM PM_BookingDb.dbo.Bookings b
LEFT JOIN PM_UserDb.dbo.Users          u ON u.Id = b.UserId
LEFT JOIN PM_VehicleDb.dbo.Vehicles    v ON v.Id = b.VehicleId
LEFT JOIN PM_ParkingDb.dbo.ParkingLots l ON l.Id = b.ParkingLotId
LEFT JOIN PM_ParkingDb.dbo.Slots       s ON s.Id = b.SlotId
LEFT JOIN PM_PaymentDb.dbo.Payments    p ON p.BookingId = b.Id
LEFT JOIN PM_GateDb.dbo.ParkingSessions g ON g.BookingId = b.Id
ORDER BY b.Code;

-- 12) Các bảng bổ sung ở migration AddDocumentCoverage ----------------------------------------------
SELECT l.Name AS Bai, h.DayOfWeek, h.OpenTime, h.CloseTime FROM PM_ParkingDb.dbo.LotOperatingHours h JOIN PM_ParkingDb.dbo.ParkingLots l ON l.Id = h.ParkingLotId;
SELECT l.Name AS Bai, STRING_AGG(a.Name, N', ') AS TienIch FROM PM_ParkingDb.dbo.LotAmenities a JOIN PM_ParkingDb.dbo.ParkingLots l ON l.Id = a.ParkingLotId GROUP BY l.Name;
SELECT ParkingLotId, TargetType, TargetId, StartsAtUtc, EndsAtUtc, IsEmergency, Reason FROM PM_ParkingDb.dbo.ClosureSchedules;
SELECT s.Code AS Slot, g.FromState, g.ToState, g.Source, g.Reason, g.CreatedAtUtc FROM PM_ParkingDb.dbo.SlotStateLogs g JOIN PM_ParkingDb.dbo.Slots s ON s.Id = g.SlotId;
SELECT ParkingLotId, ProviderName, ApiKeyPrefix, Status, LastSyncAtUtc FROM PM_ParkingDb.dbo.LotIntegrations;
SELECT Name, Address, ReferencePricePerHour, OpeningHoursText FROM PM_ParkingDb.dbo.ExternalParkingLots;
SELECT b.Code, m.ModificationType, m.OldEndAtUtc, m.NewEndAtUtc, m.PriceDifference FROM PM_BookingDb.dbo.BookingModifications m JOIN PM_BookingDb.dbo.Bookings b ON b.Id = m.BookingId;
SELECT Code, PlateNumber, ParkingLotId, ValidFrom, ValidTo, Price, Status FROM PM_BookingDb.dbo.MonthlyPasses;
SELECT Date, Name FROM PM_PaymentDb.dbo.Holidays ORDER BY Date;
SELECT p.Code AS MaKM, r.BookingId, r.DiscountAmount FROM PM_PaymentDb.dbo.PromotionRedemptions r JOIN PM_PaymentDb.dbo.Promotions p ON p.Id = r.PromotionId;
SELECT PaymentId, Provider, ProviderTransactionId, SignatureValid, IsDuplicate, ResultCode, CreatedAtUtc FROM PM_PaymentDb.dbo.PaymentCallbackLogs;
SELECT Code, UserId, Amount, Reason, Status, ExpiresAtUtc FROM PM_PaymentDb.dbo.CompensationVouchers;
SELECT ParkingLotId, Code, Name, DeviceType, Position, Status, LastSeenAtUtc FROM PM_GateDb.dbo.GateDevices;
SELECT UserId, TemplateKey, Channel, IsEnabled, QuietFrom, QuietTo FROM PM_NotificationDb.dbo.NotificationPreferences;
SELECT RuleCode, SubjectType, SubjectId, Severity, Status, Description, DueAtUtc FROM PM_AdminDb.dbo.RiskFlags;
SELECT c.Code, m.SenderParty, m.IsInternal, m.Message FROM PM_SupportDb.dbo.ComplaintMessages m JOIN PM_SupportDb.dbo.Complaints c ON c.Id = m.ComplaintId;
SELECT Category, Audience, Question FROM PM_SupportDb.dbo.FaqArticles WHERE IsPublished = 1 ORDER BY SortOrder;
SELECT UserId, EventType, Identifier, IpAddress, CreatedAtUtc FROM PM_UserDb.dbo.SecurityEvents;
SELECT UserId, RequestType, Status, DueAtUtc FROM PM_UserDb.dbo.DataSubjectRequests;
