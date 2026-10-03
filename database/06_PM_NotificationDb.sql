-- Database cua NotificationService: PM_NotificationDb. Mo trong SSMS va bam F5 (chay lai nhieu lan duoc).
IF DB_ID(N'PM_NotificationDb') IS NULL CREATE DATABASE [PM_NotificationDb];
GO
USE [PM_NotificationDb];
GO
SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
GO
IF OBJECT_ID(N'[__EFMigrationsHistory]') IS NULL
BEGIN
    CREATE TABLE [__EFMigrationsHistory] (
        [MigrationId] nvarchar(150) NOT NULL,
        [ProductVersion] nvarchar(32) NOT NULL,
        CONSTRAINT [PK___EFMigrationsHistory] PRIMARY KEY ([MigrationId])
    );
END;
GO

BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261002080108_InitialCreate'
)
BEGIN
    CREATE TABLE [NotificationTemplates] (
        [Id] int NOT NULL IDENTITY,
        [Key] nvarchar(50) NOT NULL,
        [Channel] nvarchar(40) NOT NULL,
        [TitleTemplate] nvarchar(200) NOT NULL,
        [BodyTemplate] nvarchar(2000) NOT NULL,
        [IsActive] bit NOT NULL,
        [CreatedAtUtc] datetime2 NOT NULL,
        [UpdatedAtUtc] datetime2 NULL,
        CONSTRAINT [PK_NotificationTemplates] PRIMARY KEY ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261002080108_InitialCreate'
)
BEGIN
    CREATE TABLE [OutboxMessages] (
        [Id] uniqueidentifier NOT NULL,
        [EventType] nvarchar(200) NOT NULL,
        [PayloadJson] nvarchar(max) NOT NULL,
        [OccurredAtUtc] datetime2 NOT NULL,
        [ProcessedAtUtc] datetime2 NULL,
        [AttemptCount] int NOT NULL,
        [LastError] nvarchar(2000) NULL,
        CONSTRAINT [PK_OutboxMessages] PRIMARY KEY ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261002080108_InitialCreate'
)
BEGIN
    CREATE TABLE [Notifications] (
        [Id] int NOT NULL IDENTITY,
        [UserId] int NOT NULL,
        [TemplateId] int NULL,
        [Channel] nvarchar(40) NOT NULL,
        [TemplateKey] nvarchar(50) NOT NULL,
        [Title] nvarchar(200) NOT NULL,
        [Body] nvarchar(2000) NOT NULL,
        [DataJson] nvarchar(max) NULL,
        [Status] nvarchar(40) NOT NULL,
        [RetryCount] int NOT NULL,
        [LastError] nvarchar(1000) NULL,
        [SentAtUtc] datetime2 NULL,
        [IsRead] bit NOT NULL,
        [ReadAtUtc] datetime2 NULL,
        [CreatedAtUtc] datetime2 NOT NULL,
        [UpdatedAtUtc] datetime2 NULL,
        CONSTRAINT [PK_Notifications] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_Notifications_NotificationTemplates_TemplateId] FOREIGN KEY ([TemplateId]) REFERENCES [NotificationTemplates] ([Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261002080108_InitialCreate'
)
BEGIN
    IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'Id', N'BodyTemplate', N'Channel', N'CreatedAtUtc', N'IsActive', N'Key', N'TitleTemplate', N'UpdatedAtUtc') AND [object_id] = OBJECT_ID(N'[NotificationTemplates]'))
        SET IDENTITY_INSERT [NotificationTemplates] ON;
    EXEC(N'INSERT INTO [NotificationTemplates] ([Id], [BodyTemplate], [Channel], [CreatedAtUtc], [IsActive], [Key], [TitleTemplate], [UpdatedAtUtc])
    VALUES (1, N''Mã OTP Smart Parking của bạn là {Code}, hiệu lực 5 phút.'', N''Sms'', ''2026-10-01T00:00:00.0000000Z'', CAST(1 AS bit), N''OTP_CODE'', N''Mã xác thực'', NULL),
    (2, N''Booking {BookingCode} tại {LotName} lúc {StartAt} đã được xác nhận.'', N''InApp'', ''2026-10-01T00:00:00.0000000Z'', CAST(1 AS bit), N''BOOKING_CONFIRMED'', N''Đặt chỗ thành công'', NULL),
    (3, N''Bạn đã đặt chỗ tại {LotName}, slot {SlotCode}, từ {StartAt} đến {EndAt}. Số tiền: {Amount}.'', N''Email'', ''2026-10-01T00:00:00.0000000Z'', CAST(1 AS bit), N''BOOKING_CONFIRMED'', N''Xác nhận đặt chỗ {BookingCode}'', NULL),
    (4, N''Booking {BookingCode} đã hết 15 phút giữ chỗ do chưa thanh toán.'', N''InApp'', ''2026-10-01T00:00:00.0000000Z'', CAST(1 AS bit), N''BOOKING_HOLD_EXPIRED'', N''Hết thời gian giữ chỗ'', NULL),
    (5, N''Booking {BookingCode} bắt đầu lúc {StartAt}. Bạn có 15 phút ân hạn khi đến trễ.'', N''InApp'', ''2026-10-01T00:00:00.0000000Z'', CAST(1 AS bit), N''BOOKING_REMINDER'', N''Sắp đến giờ đỗ xe'', NULL),
    (6, N''Booking {BookingCode} đã hủy. Hoàn tiền: {RefundAmount}.'', N''InApp'', ''2026-10-01T00:00:00.0000000Z'', CAST(1 AS bit), N''BOOKING_CANCELLED'', N''Đã hủy booking'', NULL),
    (7, N''Booking {BookingCode} đã bị hủy vì quá 30 phút chưa check-in.'', N''InApp'', ''2026-10-01T00:00:00.0000000Z'', CAST(1 AS bit), N''BOOKING_NO_SHOW'', N''Booking bị hủy do không đến'', NULL),
    (8, N''Xe {PlateNumber} đã vào {LotName} lúc {EntryAt}.'', N''InApp'', ''2026-10-01T00:00:00.0000000Z'', CAST(1 AS bit), N''CHECKIN_SUCCESS'', N''Xe đã vào bãi'', NULL),
    (9, N''Xe {PlateNumber} ra lúc {ExitAt}. Tổng phí: {Amount}.'', N''InApp'', ''2026-10-01T00:00:00.0000000Z'', CAST(1 AS bit), N''CHECKOUT_RECEIPT'', N''Xe đã ra bãi'', NULL),
    (10, N''Thanh toán cho {BookingCode} không thành công. Vui lòng thử lại.'', N''InApp'', ''2026-10-01T00:00:00.0000000Z'', CAST(1 AS bit), N''PAYMENT_FAILED'', N''Thanh toán thất bại'', NULL),
    (11, N''Bãi {LotName} đã được duyệt và hiển thị trên Smart Parking.'', N''Email'', ''2026-10-01T00:00:00.0000000Z'', CAST(1 AS bit), N''KYB_APPROVED'', N''Bãi xe đã được duyệt'', NULL),
    (12, N''Hồ sơ bãi {LotName} chưa được duyệt. Lý do: {Reason}.'', N''Email'', ''2026-10-01T00:00:00.0000000Z'', CAST(1 AS bit), N''KYB_REJECTED'', N''Hồ sơ bãi xe cần bổ sung'', NULL),
    (13, N''Khiếu nại {ComplaintCode} cho bãi {LotName}. Vui lòng phản hồi trong 48 giờ.'', N''InApp'', ''2026-10-01T00:00:00.0000000Z'', CAST(1 AS bit), N''COMPLAINT_CREATED'', N''Có khiếu nại mới'', NULL),
    (14, N''Bãi {LotName} tạm đóng. Booking {BookingCode} sẽ được hoàn tiền hoặc đổi bãi.'', N''Sms'', ''2026-10-01T00:00:00.0000000Z'', CAST(1 AS bit), N''EMERGENCY_CLOSURE'', N''Bãi đóng khẩn cấp'', NULL)');
    IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'Id', N'BodyTemplate', N'Channel', N'CreatedAtUtc', N'IsActive', N'Key', N'TitleTemplate', N'UpdatedAtUtc') AND [object_id] = OBJECT_ID(N'[NotificationTemplates]'))
        SET IDENTITY_INSERT [NotificationTemplates] OFF;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261002080108_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_Notifications_Status_RetryCount] ON [Notifications] ([Status], [RetryCount]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261002080108_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_Notifications_TemplateId] ON [Notifications] ([TemplateId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261002080108_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_Notifications_UserId_IsRead_CreatedAtUtc] ON [Notifications] ([UserId], [IsRead], [CreatedAtUtc]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261002080108_InitialCreate'
)
BEGIN
    CREATE UNIQUE INDEX [IX_NotificationTemplates_Key_Channel] ON [NotificationTemplates] ([Key], [Channel]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261002080108_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_OutboxMessages_ProcessedAtUtc_OccurredAtUtc] ON [OutboxMessages] ([ProcessedAtUtc], [OccurredAtUtc]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261002080108_InitialCreate'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20261002080108_InitialCreate', N'10.0.12');
END;

COMMIT;
GO

BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261002083857_AddDocumentCoverage'
)
BEGIN
    CREATE TABLE [DeviceTokens] (
        [Id] int NOT NULL IDENTITY,
        [UserId] int NOT NULL,
        [Token] nvarchar(512) NOT NULL,
        [Platform] nvarchar(40) NOT NULL,
        [DeviceName] nvarchar(100) NULL,
        [LastUsedAtUtc] datetime2 NOT NULL,
        [IsRevoked] bit NOT NULL,
        [CreatedAtUtc] datetime2 NOT NULL,
        [UpdatedAtUtc] datetime2 NULL,
        CONSTRAINT [PK_DeviceTokens] PRIMARY KEY ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261002083857_AddDocumentCoverage'
)
BEGIN
    CREATE TABLE [NotificationPreferences] (
        [Id] int NOT NULL IDENTITY,
        [UserId] int NOT NULL,
        [TemplateKey] nvarchar(50) NOT NULL,
        [Channel] nvarchar(40) NOT NULL,
        [IsEnabled] bit NOT NULL,
        [QuietFrom] time NULL,
        [QuietTo] time NULL,
        [CreatedAtUtc] datetime2 NOT NULL,
        [UpdatedAtUtc] datetime2 NULL,
        CONSTRAINT [PK_NotificationPreferences] PRIMARY KEY ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261002083857_AddDocumentCoverage'
)
BEGIN
    CREATE UNIQUE INDEX [IX_DeviceTokens_Token] ON [DeviceTokens] ([Token]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261002083857_AddDocumentCoverage'
)
BEGIN
    CREATE INDEX [IX_DeviceTokens_UserId_IsRevoked] ON [DeviceTokens] ([UserId], [IsRevoked]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261002083857_AddDocumentCoverage'
)
BEGIN
    CREATE UNIQUE INDEX [IX_NotificationPreferences_UserId_TemplateKey_Channel] ON [NotificationPreferences] ([UserId], [TemplateKey], [Channel]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261002083857_AddDocumentCoverage'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20261002083857_AddDocumentCoverage', N'10.0.12');
END;

COMMIT;
GO

