-- Database cua AdminService: PM_AdminDb. Mo trong SSMS va bam F5 (chay lai nhieu lan duoc).
IF DB_ID(N'PM_AdminDb') IS NULL CREATE DATABASE [PM_AdminDb];
GO
USE [PM_AdminDb];
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
    WHERE [MigrationId] = N'20261002080120_InitialCreate'
)
BEGIN
    CREATE TABLE [AuditLogs] (
        [Id] int NOT NULL IDENTITY,
        [SourceService] nvarchar(50) NOT NULL,
        [UserId] int NULL,
        [OwnerProfileId] int NULL,
        [Action] nvarchar(100) NOT NULL,
        [EntityName] nvarchar(100) NOT NULL,
        [EntityId] nvarchar(50) NULL,
        [OldValuesJson] nvarchar(max) NULL,
        [NewValuesJson] nvarchar(max) NULL,
        [Reason] nvarchar(1000) NULL,
        [IpAddress] nvarchar(45) NULL,
        [CreatedAtUtc] datetime2 NOT NULL,
        [UpdatedAtUtc] datetime2 NULL,
        CONSTRAINT [PK_AuditLogs] PRIMARY KEY ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261002080120_InitialCreate'
)
BEGIN
    CREATE TABLE [FeatureFlags] (
        [Id] int NOT NULL IDENTITY,
        [Key] nvarchar(100) NOT NULL,
        [IsEnabled] bit NOT NULL,
        [Description] nvarchar(500) NULL,
        [UpdatedByUserId] int NULL,
        [CreatedAtUtc] datetime2 NOT NULL,
        [UpdatedAtUtc] datetime2 NULL,
        CONSTRAINT [PK_FeatureFlags] PRIMARY KEY ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261002080120_InitialCreate'
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
    WHERE [MigrationId] = N'20261002080120_InitialCreate'
)
BEGIN
    CREATE TABLE [Sanctions] (
        [Id] int NOT NULL IDENTITY,
        [OwnerProfileId] int NOT NULL,
        [ParkingLotId] int NULL,
        [Level] nvarchar(40) NOT NULL,
        [Reason] nvarchar(1000) NOT NULL,
        [EvidenceJson] nvarchar(max) NULL,
        [StartsAtUtc] datetime2 NOT NULL,
        [EndsAtUtc] datetime2 NULL,
        [Status] nvarchar(40) NOT NULL,
        [IssuedByUserId] int NOT NULL,
        [CreatedAtUtc] datetime2 NOT NULL,
        [UpdatedAtUtc] datetime2 NULL,
        CONSTRAINT [PK_Sanctions] PRIMARY KEY ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261002080120_InitialCreate'
)
BEGIN
    CREATE TABLE [SystemConfigs] (
        [Id] int NOT NULL IDENTITY,
        [Key] nvarchar(100) NOT NULL,
        [Value] nvarchar(1000) NOT NULL,
        [DataType] nvarchar(20) NOT NULL,
        [Description] nvarchar(500) NULL,
        [UpdatedByUserId] int NULL,
        [CreatedAtUtc] datetime2 NOT NULL,
        [UpdatedAtUtc] datetime2 NULL,
        CONSTRAINT [PK_SystemConfigs] PRIMARY KEY ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261002080120_InitialCreate'
)
BEGIN
    IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'Id', N'CreatedAtUtc', N'Description', N'IsEnabled', N'Key', N'UpdatedAtUtc', N'UpdatedByUserId') AND [object_id] = OBJECT_ID(N'[FeatureFlags]'))
        SET IDENTITY_INSERT [FeatureFlags] ON;
    EXEC(N'INSERT INTO [FeatureFlags] ([Id], [CreatedAtUtc], [Description], [IsEnabled], [Key], [UpdatedAtUtc], [UpdatedByUserId])
    VALUES (1, ''2026-10-01T00:00:00.0000000Z'', N''Nhận diện biển số OCR tại cổng (có trong demo MVP)'', CAST(1 AS bit), N''FEATURE_AI_LPR'', NULL, NULL),
    (2, ''2026-10-01T00:00:00.0000000Z'', N''Sơ đồ 2.5D/3D – stretch'', CAST(0 AS bit), N''FEATURE_3D_MAP'', NULL, NULL),
    (3, ''2026-10-01T00:00:00.0000000Z'', N''Tìm bãi bằng giọng nói – stretch'', CAST(0 AS bit), N''FEATURE_AI_VOICE_SEARCH'', NULL, NULL),
    (4, ''2026-10-01T00:00:00.0000000Z'', N''Chatbot FAQ – stretch'', CAST(0 AS bit), N''FEATURE_AI_CHATBOT'', NULL, NULL),
    (5, ''2026-10-01T00:00:00.0000000Z'', N''Bãi quảng cáo trên kết quả tìm kiếm – Phase 2'', CAST(0 AS bit), N''FEATURE_SPONSORED_LISTING'', NULL, NULL),
    (6, ''2026-10-01T00:00:00.0000000Z'', N''Vé tháng – Phase 2'', CAST(0 AS bit), N''FEATURE_MONTHLY_PASS'', NULL, NULL),
    (7, ''2026-10-01T00:00:00.0000000Z'', N''Thanh toán online qua VNPAY sandbox'', CAST(1 AS bit), N''FEATURE_VNPAY'', NULL, NULL),
    (8, ''2026-10-01T00:00:00.0000000Z'', N''Thanh toán VietQR động tại cổng ra'', CAST(1 AS bit), N''FEATURE_VIETQR_AT_GATE'', NULL, NULL)');
    IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'Id', N'CreatedAtUtc', N'Description', N'IsEnabled', N'Key', N'UpdatedAtUtc', N'UpdatedByUserId') AND [object_id] = OBJECT_ID(N'[FeatureFlags]'))
        SET IDENTITY_INSERT [FeatureFlags] OFF;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261002080120_InitialCreate'
)
BEGIN
    IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'Id', N'CreatedAtUtc', N'DataType', N'Description', N'Key', N'UpdatedAtUtc', N'UpdatedByUserId', N'Value') AND [object_id] = OBJECT_ID(N'[SystemConfigs]'))
        SET IDENTITY_INSERT [SystemConfigs] ON;
    EXEC(N'INSERT INTO [SystemConfigs] ([Id], [CreatedAtUtc], [DataType], [Description], [Key], [UpdatedAtUtc], [UpdatedByUserId], [Value])
    VALUES (1, ''2026-10-01T00:00:00.0000000Z'', N''int'', N''Thời gian giữ chỗ chờ thanh toán'', N''BOOKING_HOLD_MINUTES'', NULL, NULL, N''15''),
    (2, ''2026-10-01T00:00:00.0000000Z'', N''int'', N''Ân hạn chờ tài xế đến trễ'', N''CHECKIN_GRACE_PERIOD_MINUTES'', NULL, NULL, N''15''),
    (3, ''2026-10-01T00:00:00.0000000Z'', N''int'', N''Ân hạn miễn phí tính cước mỗi lượt'', N''PARKING_BILLING_GRACE_PERIOD_MINUTES'', NULL, NULL, N''15''),
    (4, ''2026-10-01T00:00:00.0000000Z'', N''int'', N''Hủy trước ≥ 60 phút hoàn 100%, sau đó hoàn 0%'', N''CANCELLATION_WINDOW_MINUTES'', NULL, NULL, N''60''),
    (5, ''2026-10-01T00:00:00.0000000Z'', N''int'', N''Staff được hủy no-show sau mốc này'', N''NO_SHOW_CANCEL_AFTER_MINUTES'', NULL, NULL, N''30''),
    (6, ''2026-10-01T00:00:00.0000000Z'', N''int'', N''Bãi Mức 0 phải duyệt booking trong thời gian này'', N''MANUAL_LOT_APPROVAL_MINUTES'', NULL, NULL, N''10''),
    (7, ''2026-10-01T00:00:00.0000000Z'', N''decimal'', N''Hoa hồng mặc định trên giao dịch hoàn tất'', N''DEFAULT_COMMISSION_RATE'', NULL, NULL, N''0.10''),
    (8, ''2026-10-01T00:00:00.0000000Z'', N''string'', N''Chu kỳ quyết toán cho chủ bãi'', N''SETTLEMENT_CYCLE'', NULL, NULL, N''WEEKLY''),
    (9, ''2026-10-01T00:00:00.0000000Z'', N''int'', N''Hiệu lực mã OTP'', N''OTP_EXPIRY_SECONDS'', NULL, NULL, N''300''),
    (10, ''2026-10-01T00:00:00.0000000Z'', N''int'', N''Số lần nhập sai OTP trước khi khóa'', N''OTP_MAX_ATTEMPTS'', NULL, NULL, N''3''),
    (11, ''2026-10-01T00:00:00.0000000Z'', N''int'', N''Thời gian khóa sau khi sai OTP'', N''OTP_LOCK_MINUTES'', NULL, NULL, N''15''),
    (12, ''2026-10-01T00:00:00.0000000Z'', N''int'', N''Hiệu lực access token'', N''JWT_ACCESS_TOKEN_HOURS'', NULL, NULL, N''24''),
    (13, ''2026-10-01T00:00:00.0000000Z'', N''int'', N''Hiệu lực refresh token'', N''JWT_REFRESH_TOKEN_DAYS'', NULL, NULL, N''7''),
    (14, ''2026-10-01T00:00:00.0000000Z'', N''int'', N''Số xe tối đa trong Garage cá nhân'', N''MAX_VEHICLES_PER_USER'', NULL, NULL, N''10''),
    (15, ''2026-10-01T00:00:00.0000000Z'', N''decimal'', N''Bán kính tìm bãi mặc định'', N''SEARCH_RADIUS_KM'', NULL, NULL, N''5''),
    (16, ''2026-10-01T00:00:00.0000000Z'', N''int'', N''Biên an toàn chiều cao xe so với trần'', N''HEIGHT_CLEARANCE_MARGIN_CM'', NULL, NULL, N''10''),
    (17, ''2026-10-01T00:00:00.0000000Z'', N''int'', N''Chu kỳ kiểm tra kết nối bãi'', N''HEARTBEAT_INTERVAL_SECONDS'', NULL, NULL, N''60''),
    (18, ''2026-10-01T00:00:00.0000000Z'', N''int'', N''Số lần mất kết nối trước khi đánh dấu Stale'', N''HEARTBEAT_MAX_FAILURES'', NULL, NULL, N''3''),
    (19, ''2026-10-01T00:00:00.0000000Z'', N''decimal'', N''Ngưỡng tự mở barie theo kết quả OCR'', N''OCR_AUTO_OPEN_MIN_CONFIDENCE'', NULL, NULL, N''0.90''),
    (20, ''2026-10-01T00:00:00.0000000Z'', N''int'', N''Hạn chủ bãi phản hồi tranh chấp'', N''OWNER_DISPUTE_RESPONSE_HOURS'', NULL, NULL, N''48''),
    (21, ''2026-10-01T00:00:00.0000000Z'', N''int'', N''Hạn gửi khiếu nại sau sự việc'', N''COMPLAINT_WINDOW_DAYS'', NULL, NULL, N''7''),
    (22, ''2026-10-01T00:00:00.0000000Z'', N''int'', N''Hạn cập nhật sơ đồ sau thay đổi thực tế'', N''LAYOUT_UPDATE_DEADLINE_HOURS'', NULL, NULL, N''48''),
    (23, ''2026-10-01T00:00:00.0000000Z'', N''decimal'', N''Phí chiếm dụng trụ sạc sau 30 phút sạc đầy'', N''EV_IDLE_FEE_PER_15_MINUTES'', NULL, NULL, N''20000''),
    (24, ''2026-10-01T00:00:00.0000000Z'', N''int'', N''Giới hạn request/phút cho mỗi client'', N''API_RATE_LIMIT_PER_MINUTE'', NULL, NULL, N''100'')');
    IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'Id', N'CreatedAtUtc', N'DataType', N'Description', N'Key', N'UpdatedAtUtc', N'UpdatedByUserId', N'Value') AND [object_id] = OBJECT_ID(N'[SystemConfigs]'))
        SET IDENTITY_INSERT [SystemConfigs] OFF;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261002080120_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_AuditLogs_EntityName_EntityId] ON [AuditLogs] ([EntityName], [EntityId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261002080120_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_AuditLogs_OwnerProfileId_CreatedAtUtc] ON [AuditLogs] ([OwnerProfileId], [CreatedAtUtc]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261002080120_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_AuditLogs_UserId_CreatedAtUtc] ON [AuditLogs] ([UserId], [CreatedAtUtc]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261002080120_InitialCreate'
)
BEGIN
    CREATE UNIQUE INDEX [IX_FeatureFlags_Key] ON [FeatureFlags] ([Key]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261002080120_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_OutboxMessages_ProcessedAtUtc_OccurredAtUtc] ON [OutboxMessages] ([ProcessedAtUtc], [OccurredAtUtc]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261002080120_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_Sanctions_OwnerProfileId_Status] ON [Sanctions] ([OwnerProfileId], [Status]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261002080120_InitialCreate'
)
BEGIN
    EXEC(N'CREATE INDEX [IX_Sanctions_ParkingLotId] ON [Sanctions] ([ParkingLotId]) WHERE [ParkingLotId] IS NOT NULL');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261002080120_InitialCreate'
)
BEGIN
    CREATE UNIQUE INDEX [IX_SystemConfigs_Key] ON [SystemConfigs] ([Key]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261002080120_InitialCreate'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20261002080120_InitialCreate', N'10.0.12');
END;

COMMIT;
GO

BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261002083905_AddDocumentCoverage'
)
BEGIN
    ALTER TABLE [Sanctions] ADD [PenaltyAmount] decimal(18,2) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261002083905_AddDocumentCoverage'
)
BEGIN
    CREATE TABLE [RiskFlags] (
        [Id] int NOT NULL IDENTITY,
        [SubjectType] nvarchar(40) NOT NULL,
        [SubjectId] int NOT NULL,
        [RuleCode] nvarchar(60) NOT NULL,
        [Severity] nvarchar(40) NOT NULL,
        [Status] nvarchar(40) NOT NULL,
        [Description] nvarchar(1000) NOT NULL,
        [EvidenceJson] nvarchar(max) NULL,
        [DueAtUtc] datetime2 NOT NULL,
        [AssignedToUserId] int NULL,
        [ResolvedAtUtc] datetime2 NULL,
        [ResolutionNote] nvarchar(1000) NULL,
        [SanctionId] int NULL,
        [CreatedAtUtc] datetime2 NOT NULL,
        [UpdatedAtUtc] datetime2 NULL,
        CONSTRAINT [PK_RiskFlags] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_RiskFlags_Sanctions_SanctionId] FOREIGN KEY ([SanctionId]) REFERENCES [Sanctions] ([Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261002083905_AddDocumentCoverage'
)
BEGIN
    CREATE INDEX [IX_RiskFlags_SanctionId] ON [RiskFlags] ([SanctionId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261002083905_AddDocumentCoverage'
)
BEGIN
    CREATE INDEX [IX_RiskFlags_Status_Severity_DueAtUtc] ON [RiskFlags] ([Status], [Severity], [DueAtUtc]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261002083905_AddDocumentCoverage'
)
BEGIN
    CREATE INDEX [IX_RiskFlags_SubjectType_SubjectId] ON [RiskFlags] ([SubjectType], [SubjectId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261002083905_AddDocumentCoverage'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20261002083905_AddDocumentCoverage', N'10.0.12');
END;

COMMIT;
GO

