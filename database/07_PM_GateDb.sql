-- Database cua GateService: PM_GateDb. Mo trong SSMS va bam F5 (chay lai nhieu lan duoc).
IF DB_ID(N'PM_GateDb') IS NULL CREATE DATABASE [PM_GateDb];
GO
USE [PM_GateDb];
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
    WHERE [MigrationId] = N'20261002080114_InitialCreate'
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
    WHERE [MigrationId] = N'20261002080114_InitialCreate'
)
BEGIN
    CREATE TABLE [ParkingSessions] (
        [Id] int NOT NULL IDENTITY,
        [Code] nvarchar(30) NOT NULL,
        [ParkingLotId] int NOT NULL,
        [OwnerProfileId] int NOT NULL,
        [BookingId] int NULL,
        [BookingCode] nvarchar(30) NULL,
        [UserId] int NULL,
        [VehicleId] int NULL,
        [SlotId] int NULL,
        [SlotCode] nvarchar(20) NULL,
        [PlateNumber] nvarchar(15) NOT NULL,
        [VehicleType] nvarchar(40) NOT NULL,
        [IsWalkIn] bit NOT NULL,
        [EntryAtUtc] datetime2 NOT NULL,
        [CheckInMethod] nvarchar(40) NOT NULL,
        [EntryImagePath] nvarchar(512) NULL,
        [EntryOcrRaw] nvarchar(100) NULL,
        [EntryOcrConfidence] float NULL,
        [CheckedInByStaffId] int NULL,
        [ExitAtUtc] datetime2 NULL,
        [CheckOutMethod] nvarchar(40) NULL,
        [ExitImagePath] nvarchar(512) NULL,
        [ExitOcrRaw] nvarchar(100) NULL,
        [ExitOcrConfidence] float NULL,
        [CheckedOutByStaffId] int NULL,
        [Fee] decimal(18,2) NULL,
        [OverstayFee] decimal(18,2) NULL,
        [Status] nvarchar(40) NOT NULL,
        [CreatedAtUtc] datetime2 NOT NULL,
        [UpdatedAtUtc] datetime2 NULL,
        CONSTRAINT [PK_ParkingSessions] PRIMARY KEY ([Id]),
        CONSTRAINT [CK_ParkingSessions_Exit] CHECK ([ExitAtUtc] IS NULL OR [ExitAtUtc] >= [EntryAtUtc])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261002080114_InitialCreate'
)
BEGIN
    CREATE TABLE [Shifts] (
        [Id] int NOT NULL IDENTITY,
        [ParkingLotId] int NOT NULL,
        [OwnerProfileId] int NOT NULL,
        [StaffUserId] int NOT NULL,
        [StartedAtUtc] datetime2 NOT NULL,
        [EndedAtUtc] datetime2 NULL,
        [Status] nvarchar(40) NOT NULL,
        [CheckInCount] int NOT NULL,
        [CheckOutCount] int NOT NULL,
        [NoShowCancelCount] int NOT NULL,
        [ManualExceptionCount] int NOT NULL,
        [CashCollected] decimal(18,2) NOT NULL,
        [HandoverNote] nvarchar(1000) NULL,
        [CreatedAtUtc] datetime2 NOT NULL,
        [UpdatedAtUtc] datetime2 NULL,
        CONSTRAINT [PK_Shifts] PRIMARY KEY ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261002080114_InitialCreate'
)
BEGIN
    CREATE TABLE [GateEvents] (
        [Id] int NOT NULL IDENTITY,
        [ParkingLotId] int NOT NULL,
        [ParkingSessionId] int NULL,
        [ShiftId] int NULL,
        [EventType] nvarchar(40) NOT NULL,
        [PlateNumber] nvarchar(15) NULL,
        [ImagePath] nvarchar(512) NULL,
        [OcrRaw] nvarchar(100) NULL,
        [OcrConfidence] float NULL,
        [PerformedByUserId] int NULL,
        [Note] nvarchar(1000) NULL,
        [CreatedAtUtc] datetime2 NOT NULL,
        [UpdatedAtUtc] datetime2 NULL,
        CONSTRAINT [PK_GateEvents] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_GateEvents_ParkingSessions_ParkingSessionId] FOREIGN KEY ([ParkingSessionId]) REFERENCES [ParkingSessions] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_GateEvents_Shifts_ShiftId] FOREIGN KEY ([ShiftId]) REFERENCES [Shifts] ([Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261002080114_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_GateEvents_EventType_CreatedAtUtc] ON [GateEvents] ([EventType], [CreatedAtUtc]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261002080114_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_GateEvents_ParkingLotId_CreatedAtUtc] ON [GateEvents] ([ParkingLotId], [CreatedAtUtc]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261002080114_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_GateEvents_ParkingSessionId] ON [GateEvents] ([ParkingSessionId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261002080114_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_GateEvents_ShiftId] ON [GateEvents] ([ShiftId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261002080114_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_OutboxMessages_ProcessedAtUtc_OccurredAtUtc] ON [OutboxMessages] ([ProcessedAtUtc], [OccurredAtUtc]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261002080114_InitialCreate'
)
BEGIN
    EXEC(N'CREATE UNIQUE INDEX [IX_ParkingSessions_BookingId] ON [ParkingSessions] ([BookingId]) WHERE [BookingId] IS NOT NULL');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261002080114_InitialCreate'
)
BEGIN
    CREATE UNIQUE INDEX [IX_ParkingSessions_Code] ON [ParkingSessions] ([Code]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261002080114_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_ParkingSessions_OwnerProfileId_ParkingLotId_EntryAtUtc] ON [ParkingSessions] ([OwnerProfileId], [ParkingLotId], [EntryAtUtc]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261002080114_InitialCreate'
)
BEGIN
    EXEC(N'CREATE UNIQUE INDEX [IX_ParkingSessions_ParkingLotId_PlateNumber] ON [ParkingSessions] ([ParkingLotId], [PlateNumber]) WHERE [Status] = N''Active''');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261002080114_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_Shifts_ParkingLotId_StartedAtUtc] ON [Shifts] ([ParkingLotId], [StartedAtUtc]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261002080114_InitialCreate'
)
BEGIN
    EXEC(N'CREATE UNIQUE INDEX [IX_Shifts_StaffUserId] ON [Shifts] ([StaffUserId]) WHERE [Status] = N''Open''');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261002080114_InitialCreate'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20261002080114_InitialCreate', N'10.0.12');
END;

COMMIT;
GO

BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261002083901_AddDocumentCoverage'
)
BEGIN
    CREATE TABLE [GateDevices] (
        [Id] int NOT NULL IDENTITY,
        [ParkingLotId] int NOT NULL,
        [Code] nvarchar(40) NOT NULL,
        [Name] nvarchar(100) NOT NULL,
        [DeviceType] nvarchar(40) NOT NULL,
        [Position] nvarchar(40) NULL,
        [Status] nvarchar(40) NOT NULL,
        [LastSeenAtUtc] datetime2 NULL,
        [FirmwareVersion] nvarchar(40) NULL,
        [OfflinePublicKey] nvarchar(max) NULL,
        [IsActive] bit NOT NULL,
        [CreatedAtUtc] datetime2 NOT NULL,
        [UpdatedAtUtc] datetime2 NULL,
        CONSTRAINT [PK_GateDevices] PRIMARY KEY ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261002083901_AddDocumentCoverage'
)
BEGIN
    CREATE UNIQUE INDEX [IX_GateDevices_Code] ON [GateDevices] ([Code]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261002083901_AddDocumentCoverage'
)
BEGIN
    CREATE INDEX [IX_GateDevices_ParkingLotId_Status] ON [GateDevices] ([ParkingLotId], [Status]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261002083901_AddDocumentCoverage'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20261002083901_AddDocumentCoverage', N'10.0.12');
END;

COMMIT;
GO

