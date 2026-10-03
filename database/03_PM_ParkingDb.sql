-- Database cua ParkingService: PM_ParkingDb. Mo trong SSMS va bam F5 (chay lai nhieu lan duoc).
IF DB_ID(N'PM_ParkingDb') IS NULL CREATE DATABASE [PM_ParkingDb];
GO
USE [PM_ParkingDb];
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
    WHERE [MigrationId] = N'20261002080050_InitialCreate'
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
    WHERE [MigrationId] = N'20261002080050_InitialCreate'
)
BEGIN
    CREATE TABLE [ParkingLots] (
        [Id] int NOT NULL IDENTITY,
        [OwnerProfileId] int NOT NULL,
        [Name] nvarchar(200) NOT NULL,
        [Address] nvarchar(500) NOT NULL,
        [Description] nvarchar(2000) NULL,
        [HotlinePhone] nvarchar(20) NULL,
        [CoverImageUrl] nvarchar(512) NULL,
        [Latitude] float NOT NULL,
        [Longitude] float NOT NULL,
        [TotalSlots] int NOT NULL,
        [AvailableSlots] int NOT NULL,
        [MaxHeightCm] int NOT NULL,
        [OpenTime] time NOT NULL,
        [CloseTime] time NOT NULL,
        [Status] nvarchar(40) NOT NULL,
        [IntegrationTier] nvarchar(40) NOT NULL,
        [RatingAverage] decimal(3,2) NOT NULL,
        [RatingCount] int NOT NULL,
        [IsDeleted] bit NOT NULL,
        [DeletedAtUtc] datetime2 NULL,
        [CreatedAtUtc] datetime2 NOT NULL,
        [UpdatedAtUtc] datetime2 NULL,
        CONSTRAINT [PK_ParkingLots] PRIMARY KEY ([Id]),
        CONSTRAINT [CK_ParkingLots_Latitude] CHECK ([Latitude] BETWEEN -90 AND 90),
        CONSTRAINT [CK_ParkingLots_Longitude] CHECK ([Longitude] BETWEEN -180 AND 180),
        CONSTRAINT [CK_ParkingLots_Slots] CHECK ([AvailableSlots] >= 0 AND [AvailableSlots] <= [TotalSlots])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261002080050_InitialCreate'
)
BEGIN
    CREATE TABLE [KybApplications] (
        [Id] int NOT NULL IDENTITY,
        [ParkingLotId] int NOT NULL,
        [Status] nvarchar(40) NOT NULL,
        [BusinessLicenseUrl] nvarchar(512) NULL,
        [SitePhotoUrlsJson] nvarchar(max) NULL,
        [PhotoLatitude] float NULL,
        [PhotoLongitude] float NULL,
        [FireSafetyCertificateUrl] nvarchar(512) NULL,
        [FieldSurveyRequired] bit NOT NULL,
        [FieldSurveyAtUtc] datetime2 NULL,
        [FieldSurveyNote] nvarchar(2000) NULL,
        [SubmittedAtUtc] datetime2 NULL,
        [ReviewedByUserId] int NULL,
        [ReviewedAtUtc] datetime2 NULL,
        [RejectReason] nvarchar(1000) NULL,
        [CreatedAtUtc] datetime2 NOT NULL,
        [UpdatedAtUtc] datetime2 NULL,
        CONSTRAINT [PK_KybApplications] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_KybApplications_ParkingLots_ParkingLotId] FOREIGN KEY ([ParkingLotId]) REFERENCES [ParkingLots] ([Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261002080050_InitialCreate'
)
BEGIN
    CREATE TABLE [LotCapacityConfigs] (
        [Id] int NOT NULL IDENTITY,
        [ParkingLotId] int NOT NULL,
        [OnlineQuota] int NOT NULL,
        [WalkInBufferPercent] int NOT NULL,
        [OnlineLockThreshold] int NOT NULL,
        [InstantBookingPaused] bit NOT NULL,
        [IsEmergencyStopped] bit NOT NULL,
        [EmergencyReason] nvarchar(500) NULL,
        [LastHeartbeatAtUtc] datetime2 NULL,
        [ConsecutiveHeartbeatFailures] int NOT NULL,
        [IsStale] bit NOT NULL,
        [CreatedAtUtc] datetime2 NOT NULL,
        [UpdatedAtUtc] datetime2 NULL,
        CONSTRAINT [PK_LotCapacityConfigs] PRIMARY KEY ([Id]),
        CONSTRAINT [CK_LotCapacity_Buffer] CHECK ([WalkInBufferPercent] BETWEEN 0 AND 100),
        CONSTRAINT [FK_LotCapacityConfigs_ParkingLots_ParkingLotId] FOREIGN KEY ([ParkingLotId]) REFERENCES [ParkingLots] ([Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261002080050_InitialCreate'
)
BEGIN
    CREATE TABLE [Zones] (
        [Id] int NOT NULL IDENTITY,
        [ParkingLotId] int NOT NULL,
        [Code] nvarchar(20) NOT NULL,
        [Name] nvarchar(100) NOT NULL,
        [IsOutdoor] bit NOT NULL,
        [IsClosed] bit NOT NULL,
        [ClosedReason] nvarchar(500) NULL,
        [SortOrder] int NOT NULL,
        [CreatedAtUtc] datetime2 NOT NULL,
        [UpdatedAtUtc] datetime2 NULL,
        CONSTRAINT [PK_Zones] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_Zones_ParkingLots_ParkingLotId] FOREIGN KEY ([ParkingLotId]) REFERENCES [ParkingLots] ([Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261002080050_InitialCreate'
)
BEGIN
    CREATE TABLE [Floors] (
        [Id] int NOT NULL IDENTITY,
        [ZoneId] int NOT NULL,
        [Name] nvarchar(50) NOT NULL,
        [Level] int NOT NULL,
        [MaxHeightCm] int NULL,
        [MaxWeightKg] int NULL,
        [GridColumns] int NOT NULL,
        [GridRows] int NOT NULL,
        [IsClosed] bit NOT NULL,
        [CreatedAtUtc] datetime2 NOT NULL,
        [UpdatedAtUtc] datetime2 NULL,
        CONSTRAINT [PK_Floors] PRIMARY KEY ([Id]),
        CONSTRAINT [CK_Floors_Grid] CHECK ([GridColumns] > 0 AND [GridRows] > 0),
        CONSTRAINT [FK_Floors_Zones_ZoneId] FOREIGN KEY ([ZoneId]) REFERENCES [Zones] ([Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261002080050_InitialCreate'
)
BEGIN
    CREATE TABLE [LayoutVersions] (
        [Id] int NOT NULL IDENTITY,
        [FloorId] int NOT NULL,
        [VersionNo] int NOT NULL,
        [LayoutJson] nvarchar(max) NOT NULL,
        [IsPublished] bit NOT NULL,
        [PublishedAtUtc] datetime2 NULL,
        [CreatedByUserId] int NOT NULL,
        [ChangeNote] nvarchar(500) NULL,
        [CreatedAtUtc] datetime2 NOT NULL,
        [UpdatedAtUtc] datetime2 NULL,
        CONSTRAINT [PK_LayoutVersions] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_LayoutVersions_Floors_FloorId] FOREIGN KEY ([FloorId]) REFERENCES [Floors] ([Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261002080050_InitialCreate'
)
BEGIN
    CREATE TABLE [Slots] (
        [Id] int NOT NULL IDENTITY,
        [FloorId] int NOT NULL,
        [Code] nvarchar(20) NOT NULL,
        [SlotType] nvarchar(40) NOT NULL,
        [MaxVehicleType] nvarchar(40) NOT NULL,
        [GridX] int NOT NULL,
        [GridY] int NOT NULL,
        [WidthCells] int NOT NULL,
        [HeightCells] int NOT NULL,
        [State] nvarchar(40) NOT NULL,
        [StateChangedAtUtc] datetime2 NULL,
        [DedicatedVehicleId] int NULL,
        [IsActive] bit NOT NULL,
        [RowVersion] rowversion NOT NULL,
        [CreatedAtUtc] datetime2 NOT NULL,
        [UpdatedAtUtc] datetime2 NULL,
        CONSTRAINT [PK_Slots] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_Slots_Floors_FloorId] FOREIGN KEY ([FloorId]) REFERENCES [Floors] ([Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261002080050_InitialCreate'
)
BEGIN
    CREATE UNIQUE INDEX [IX_Floors_ZoneId_Name] ON [Floors] ([ZoneId], [Name]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261002080050_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_KybApplications_ParkingLotId_Status] ON [KybApplications] ([ParkingLotId], [Status]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261002080050_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_KybApplications_Status] ON [KybApplications] ([Status]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261002080050_InitialCreate'
)
BEGIN
    CREATE UNIQUE INDEX [IX_LayoutVersions_FloorId_VersionNo] ON [LayoutVersions] ([FloorId], [VersionNo]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261002080050_InitialCreate'
)
BEGIN
    CREATE UNIQUE INDEX [IX_LotCapacityConfigs_ParkingLotId] ON [LotCapacityConfigs] ([ParkingLotId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261002080050_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_OutboxMessages_ProcessedAtUtc_OccurredAtUtc] ON [OutboxMessages] ([ProcessedAtUtc], [OccurredAtUtc]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261002080050_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_ParkingLots_Latitude_Longitude] ON [ParkingLots] ([Latitude], [Longitude]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261002080050_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_ParkingLots_OwnerProfileId] ON [ParkingLots] ([OwnerProfileId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261002080050_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_ParkingLots_Status] ON [ParkingLots] ([Status]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261002080050_InitialCreate'
)
BEGIN
    EXEC(N'CREATE INDEX [IX_Slots_DedicatedVehicleId] ON [Slots] ([DedicatedVehicleId]) WHERE [DedicatedVehicleId] IS NOT NULL');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261002080050_InitialCreate'
)
BEGIN
    CREATE UNIQUE INDEX [IX_Slots_FloorId_Code] ON [Slots] ([FloorId], [Code]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261002080050_InitialCreate'
)
BEGIN
    CREATE UNIQUE INDEX [IX_Slots_FloorId_GridX_GridY] ON [Slots] ([FloorId], [GridX], [GridY]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261002080050_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_Slots_FloorId_State] ON [Slots] ([FloorId], [State]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261002080050_InitialCreate'
)
BEGIN
    CREATE UNIQUE INDEX [IX_Zones_ParkingLotId_Code] ON [Zones] ([ParkingLotId], [Code]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261002080050_InitialCreate'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20261002080050_InitialCreate', N'10.0.12');
END;

COMMIT;
GO

BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261002083844_AddDocumentCoverage'
)
BEGIN
    ALTER TABLE [ParkingLots] ADD [City] nvarchar(100) NOT NULL DEFAULT N'TP.HCM';
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261002083844_AddDocumentCoverage'
)
BEGIN
    ALTER TABLE [ParkingLots] ADD [District] nvarchar(100) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261002083844_AddDocumentCoverage'
)
BEGIN
    CREATE TABLE [ClosureSchedules] (
        [Id] int NOT NULL IDENTITY,
        [ParkingLotId] int NOT NULL,
        [TargetType] nvarchar(40) NOT NULL,
        [TargetId] int NOT NULL,
        [StartsAtUtc] datetime2 NOT NULL,
        [EndsAtUtc] datetime2 NULL,
        [Reason] nvarchar(500) NOT NULL,
        [IsEmergency] bit NOT NULL,
        [AffectedBookingsNotified] bit NOT NULL,
        [CreatedByUserId] int NOT NULL,
        [CancelledAtUtc] datetime2 NULL,
        [CreatedAtUtc] datetime2 NOT NULL,
        [UpdatedAtUtc] datetime2 NULL,
        CONSTRAINT [PK_ClosureSchedules] PRIMARY KEY ([Id]),
        CONSTRAINT [CK_ClosureSchedules_Period] CHECK ([EndsAtUtc] IS NULL OR [EndsAtUtc] > [StartsAtUtc]),
        CONSTRAINT [FK_ClosureSchedules_ParkingLots_ParkingLotId] FOREIGN KEY ([ParkingLotId]) REFERENCES [ParkingLots] ([Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261002083844_AddDocumentCoverage'
)
BEGIN
    CREATE TABLE [ExternalParkingLots] (
        [Id] int NOT NULL IDENTITY,
        [Name] nvarchar(200) NOT NULL,
        [Address] nvarchar(500) NOT NULL,
        [City] nvarchar(100) NULL,
        [Latitude] float NOT NULL,
        [Longitude] float NOT NULL,
        [ReferencePricePerHour] decimal(18,2) NULL,
        [OpeningHoursText] nvarchar(200) NULL,
        [Source] nvarchar(200) NULL,
        [ImportedByUserId] int NOT NULL,
        [IsDeleted] bit NOT NULL,
        [DeletedAtUtc] datetime2 NULL,
        [CreatedAtUtc] datetime2 NOT NULL,
        [UpdatedAtUtc] datetime2 NULL,
        CONSTRAINT [PK_ExternalParkingLots] PRIMARY KEY ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261002083844_AddDocumentCoverage'
)
BEGIN
    CREATE TABLE [LotAmenities] (
        [Id] int NOT NULL IDENTITY,
        [ParkingLotId] int NOT NULL,
        [Code] nvarchar(40) NOT NULL,
        [Name] nvarchar(100) NOT NULL,
        [Note] nvarchar(200) NULL,
        [CreatedAtUtc] datetime2 NOT NULL,
        [UpdatedAtUtc] datetime2 NULL,
        CONSTRAINT [PK_LotAmenities] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_LotAmenities_ParkingLots_ParkingLotId] FOREIGN KEY ([ParkingLotId]) REFERENCES [ParkingLots] ([Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261002083844_AddDocumentCoverage'
)
BEGIN
    CREATE TABLE [LotIntegrations] (
        [Id] int NOT NULL IDENTITY,
        [ParkingLotId] int NOT NULL,
        [ProviderName] nvarchar(100) NOT NULL,
        [ApiKeyPrefix] nvarchar(12) NOT NULL,
        [ApiKeyHash] nvarchar(200) NOT NULL,
        [WebhookUrl] nvarchar(512) NULL,
        [WebhookSecretHash] nvarchar(200) NULL,
        [Status] nvarchar(40) NOT NULL,
        [LastSyncAtUtc] datetime2 NULL,
        [LastError] nvarchar(1000) NULL,
        [CreatedAtUtc] datetime2 NOT NULL,
        [UpdatedAtUtc] datetime2 NULL,
        CONSTRAINT [PK_LotIntegrations] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_LotIntegrations_ParkingLots_ParkingLotId] FOREIGN KEY ([ParkingLotId]) REFERENCES [ParkingLots] ([Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261002083844_AddDocumentCoverage'
)
BEGIN
    CREATE TABLE [LotOperatingHours] (
        [Id] int NOT NULL IDENTITY,
        [ParkingLotId] int NOT NULL,
        [DayOfWeek] nvarchar(40) NOT NULL,
        [OpenTime] time NOT NULL,
        [CloseTime] time NOT NULL,
        [IsClosed] bit NOT NULL,
        [CreatedAtUtc] datetime2 NOT NULL,
        [UpdatedAtUtc] datetime2 NULL,
        CONSTRAINT [PK_LotOperatingHours] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_LotOperatingHours_ParkingLots_ParkingLotId] FOREIGN KEY ([ParkingLotId]) REFERENCES [ParkingLots] ([Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261002083844_AddDocumentCoverage'
)
BEGIN
    CREATE TABLE [LotPhotos] (
        [Id] int NOT NULL IDENTITY,
        [ParkingLotId] int NOT NULL,
        [Url] nvarchar(512) NOT NULL,
        [Caption] nvarchar(200) NULL,
        [SortOrder] int NOT NULL,
        [IsCover] bit NOT NULL,
        [CreatedAtUtc] datetime2 NOT NULL,
        [UpdatedAtUtc] datetime2 NULL,
        CONSTRAINT [PK_LotPhotos] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_LotPhotos_ParkingLots_ParkingLotId] FOREIGN KEY ([ParkingLotId]) REFERENCES [ParkingLots] ([Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261002083844_AddDocumentCoverage'
)
BEGIN
    CREATE TABLE [SlotStateLogs] (
        [Id] int NOT NULL IDENTITY,
        [SlotId] int NOT NULL,
        [FromState] nvarchar(40) NOT NULL,
        [ToState] nvarchar(40) NOT NULL,
        [Source] nvarchar(40) NOT NULL,
        [BookingId] int NULL,
        [ParkingSessionId] int NULL,
        [ChangedByUserId] int NULL,
        [Reason] nvarchar(500) NULL,
        [CreatedAtUtc] datetime2 NOT NULL,
        [UpdatedAtUtc] datetime2 NULL,
        CONSTRAINT [PK_SlotStateLogs] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_SlotStateLogs_Slots_SlotId] FOREIGN KEY ([SlotId]) REFERENCES [Slots] ([Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261002083844_AddDocumentCoverage'
)
BEGIN
    CREATE INDEX [IX_ParkingLots_City_District] ON [ParkingLots] ([City], [District]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261002083844_AddDocumentCoverage'
)
BEGIN
    CREATE INDEX [IX_ClosureSchedules_ParkingLotId_StartsAtUtc_EndsAtUtc] ON [ClosureSchedules] ([ParkingLotId], [StartsAtUtc], [EndsAtUtc]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261002083844_AddDocumentCoverage'
)
BEGIN
    CREATE INDEX [IX_ClosureSchedules_TargetType_TargetId] ON [ClosureSchedules] ([TargetType], [TargetId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261002083844_AddDocumentCoverage'
)
BEGIN
    CREATE INDEX [IX_ExternalParkingLots_Latitude_Longitude] ON [ExternalParkingLots] ([Latitude], [Longitude]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261002083844_AddDocumentCoverage'
)
BEGIN
    CREATE INDEX [IX_LotAmenities_Code] ON [LotAmenities] ([Code]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261002083844_AddDocumentCoverage'
)
BEGIN
    CREATE UNIQUE INDEX [IX_LotAmenities_ParkingLotId_Code] ON [LotAmenities] ([ParkingLotId], [Code]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261002083844_AddDocumentCoverage'
)
BEGIN
    CREATE UNIQUE INDEX [IX_LotIntegrations_ApiKeyPrefix] ON [LotIntegrations] ([ApiKeyPrefix]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261002083844_AddDocumentCoverage'
)
BEGIN
    CREATE UNIQUE INDEX [IX_LotIntegrations_ParkingLotId] ON [LotIntegrations] ([ParkingLotId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261002083844_AddDocumentCoverage'
)
BEGIN
    CREATE UNIQUE INDEX [IX_LotOperatingHours_ParkingLotId_DayOfWeek] ON [LotOperatingHours] ([ParkingLotId], [DayOfWeek]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261002083844_AddDocumentCoverage'
)
BEGIN
    CREATE INDEX [IX_LotPhotos_ParkingLotId_SortOrder] ON [LotPhotos] ([ParkingLotId], [SortOrder]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261002083844_AddDocumentCoverage'
)
BEGIN
    EXEC(N'CREATE UNIQUE INDEX [UX_LotPhotos_OneCover] ON [LotPhotos] ([ParkingLotId]) WHERE [IsCover] = 1');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261002083844_AddDocumentCoverage'
)
BEGIN
    CREATE INDEX [IX_SlotStateLogs_SlotId_CreatedAtUtc] ON [SlotStateLogs] ([SlotId], [CreatedAtUtc]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261002083844_AddDocumentCoverage'
)
BEGIN
    CREATE INDEX [IX_SlotStateLogs_Source_CreatedAtUtc] ON [SlotStateLogs] ([Source], [CreatedAtUtc]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261002083844_AddDocumentCoverage'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20261002083844_AddDocumentCoverage', N'10.0.12');
END;

COMMIT;
GO

