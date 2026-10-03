-- Database cua BookingService: PM_BookingDb. Mo trong SSMS va bam F5 (chay lai nhieu lan duoc).
IF DB_ID(N'PM_BookingDb') IS NULL CREATE DATABASE [PM_BookingDb];
GO
USE [PM_BookingDb];
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
    WHERE [MigrationId] = N'20261002080056_InitialCreate'
)
BEGIN
    CREATE TABLE [Bookings] (
        [Id] int NOT NULL IDENTITY,
        [Code] nvarchar(30) NOT NULL,
        [UserId] int NOT NULL,
        [VehicleId] int NOT NULL,
        [PlateNumber] nvarchar(15) NOT NULL,
        [VehicleType] nvarchar(40) NOT NULL,
        [ParkingLotId] int NOT NULL,
        [OwnerProfileId] int NOT NULL,
        [ParkingLotName] nvarchar(200) NOT NULL,
        [ZoneId] int NULL,
        [SlotId] int NULL,
        [SlotCode] nvarchar(20) NULL,
        [AllocationMode] nvarchar(40) NOT NULL,
        [StartAtUtc] datetime2 NOT NULL,
        [EndAtUtc] datetime2 NOT NULL,
        [HoldExpiresAtUtc] datetime2 NULL,
        [ConfirmedAtUtc] datetime2 NULL,
        [CheckedInAtUtc] datetime2 NULL,
        [CheckedOutAtUtc] datetime2 NULL,
        [CompletedAtUtc] datetime2 NULL,
        [CancelledAtUtc] datetime2 NULL,
        [CancelReason] nvarchar(500) NULL,
        [CancelledByUserId] int NULL,
        [Status] nvarchar(40) NOT NULL,
        [RequiresOwnerApproval] bit NOT NULL,
        [TotalAmount] decimal(18,2) NOT NULL,
        [DiscountAmount] decimal(18,2) NOT NULL,
        [PaidAmount] decimal(18,2) NOT NULL,
        [PromotionId] int NULL,
        [PromotionCode] nvarchar(30) NULL,
        [QrToken] nvarchar(1000) NULL,
        [RowVersion] rowversion NOT NULL,
        [CreatedAtUtc] datetime2 NOT NULL,
        [UpdatedAtUtc] datetime2 NULL,
        CONSTRAINT [PK_Bookings] PRIMARY KEY ([Id]),
        CONSTRAINT [CK_Bookings_Amount] CHECK ([TotalAmount] >= 0 AND [DiscountAmount] >= 0 AND [PaidAmount] >= 0),
        CONSTRAINT [CK_Bookings_Time] CHECK ([EndAtUtc] > [StartAtUtc])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261002080056_InitialCreate'
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
    WHERE [MigrationId] = N'20261002080056_InitialCreate'
)
BEGIN
    CREATE TABLE [BookingStatusLogs] (
        [Id] int NOT NULL IDENTITY,
        [BookingId] int NOT NULL,
        [FromStatus] nvarchar(40) NULL,
        [ToStatus] nvarchar(40) NOT NULL,
        [ChangedByUserId] int NULL,
        [Reason] nvarchar(500) NULL,
        [CreatedAtUtc] datetime2 NOT NULL,
        [UpdatedAtUtc] datetime2 NULL,
        CONSTRAINT [PK_BookingStatusLogs] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_BookingStatusLogs_Bookings_BookingId] FOREIGN KEY ([BookingId]) REFERENCES [Bookings] ([Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261002080056_InitialCreate'
)
BEGIN
    CREATE TABLE [PriceSnapshots] (
        [Id] int NOT NULL IDENTITY,
        [BookingId] int NOT NULL,
        [RateCardId] int NOT NULL,
        [RateCardJson] nvarchar(max) NOT NULL,
        [BaseAmount] decimal(18,2) NOT NULL,
        [SurchargeAmount] decimal(18,2) NOT NULL,
        [DiscountAmount] decimal(18,2) NOT NULL,
        [FinalAmount] decimal(18,2) NOT NULL,
        [BillingGracePeriodMinutes] int NOT NULL,
        [OverstayMultiplier] decimal(4,2) NOT NULL,
        [CreatedAtUtc] datetime2 NOT NULL,
        [UpdatedAtUtc] datetime2 NULL,
        CONSTRAINT [PK_PriceSnapshots] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_PriceSnapshots_Bookings_BookingId] FOREIGN KEY ([BookingId]) REFERENCES [Bookings] ([Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261002080056_InitialCreate'
)
BEGIN
    CREATE UNIQUE INDEX [IX_Bookings_Code] ON [Bookings] ([Code]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261002080056_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_Bookings_OwnerProfileId_ParkingLotId_StartAtUtc] ON [Bookings] ([OwnerProfileId], [ParkingLotId], [StartAtUtc]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261002080056_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_Bookings_PlateNumber] ON [Bookings] ([PlateNumber]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261002080056_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_Bookings_SlotId_StartAtUtc_EndAtUtc] ON [Bookings] ([SlotId], [StartAtUtc], [EndAtUtc]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261002080056_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_Bookings_Status_HoldExpiresAtUtc] ON [Bookings] ([Status], [HoldExpiresAtUtc]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261002080056_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_Bookings_UserId_Status] ON [Bookings] ([UserId], [Status]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261002080056_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_BookingStatusLogs_BookingId_CreatedAtUtc] ON [BookingStatusLogs] ([BookingId], [CreatedAtUtc]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261002080056_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_OutboxMessages_ProcessedAtUtc_OccurredAtUtc] ON [OutboxMessages] ([ProcessedAtUtc], [OccurredAtUtc]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261002080056_InitialCreate'
)
BEGIN
    CREATE UNIQUE INDEX [IX_PriceSnapshots_BookingId] ON [PriceSnapshots] ([BookingId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261002080056_InitialCreate'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20261002080056_InitialCreate', N'10.0.12');
END;

COMMIT;
GO

BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261002083848_AddDocumentCoverage'
)
BEGIN
    CREATE TABLE [BookingModifications] (
        [Id] int NOT NULL IDENTITY,
        [BookingId] int NOT NULL,
        [ModificationType] nvarchar(40) NOT NULL,
        [OldStartAtUtc] datetime2 NULL,
        [OldEndAtUtc] datetime2 NULL,
        [NewStartAtUtc] datetime2 NULL,
        [NewEndAtUtc] datetime2 NULL,
        [OldVehicleId] int NULL,
        [NewVehicleId] int NULL,
        [OldSlotId] int NULL,
        [NewSlotId] int NULL,
        [PriceDifference] decimal(18,2) NOT NULL,
        [PaymentId] int NULL,
        [RequestedByUserId] int NOT NULL,
        [CreatedAtUtc] datetime2 NOT NULL,
        [UpdatedAtUtc] datetime2 NULL,
        CONSTRAINT [PK_BookingModifications] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_BookingModifications_Bookings_BookingId] FOREIGN KEY ([BookingId]) REFERENCES [Bookings] ([Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261002083848_AddDocumentCoverage'
)
BEGIN
    CREATE TABLE [MonthlyPasses] (
        [Id] int NOT NULL IDENTITY,
        [Code] nvarchar(30) NOT NULL,
        [UserId] int NOT NULL,
        [VehicleId] int NOT NULL,
        [PlateNumber] nvarchar(15) NOT NULL,
        [ParkingLotId] int NOT NULL,
        [OwnerProfileId] int NOT NULL,
        [SlotId] int NULL,
        [SlotCode] nvarchar(20) NULL,
        [ValidFrom] date NOT NULL,
        [ValidTo] date NOT NULL,
        [Price] decimal(18,2) NOT NULL,
        [AutoRenew] bit NOT NULL,
        [Status] nvarchar(40) NOT NULL,
        [PaymentId] int NULL,
        [CancelledAtUtc] datetime2 NULL,
        [CreatedAtUtc] datetime2 NOT NULL,
        [UpdatedAtUtc] datetime2 NULL,
        CONSTRAINT [PK_MonthlyPasses] PRIMARY KEY ([Id]),
        CONSTRAINT [CK_MonthlyPasses_Period] CHECK ([ValidTo] >= [ValidFrom])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261002083848_AddDocumentCoverage'
)
BEGIN
    CREATE INDEX [IX_BookingModifications_BookingId_CreatedAtUtc] ON [BookingModifications] ([BookingId], [CreatedAtUtc]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261002083848_AddDocumentCoverage'
)
BEGIN
    CREATE UNIQUE INDEX [IX_MonthlyPasses_Code] ON [MonthlyPasses] ([Code]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261002083848_AddDocumentCoverage'
)
BEGIN
    CREATE INDEX [IX_MonthlyPasses_ParkingLotId_PlateNumber_Status] ON [MonthlyPasses] ([ParkingLotId], [PlateNumber], [Status]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261002083848_AddDocumentCoverage'
)
BEGIN
    EXEC(N'CREATE UNIQUE INDEX [IX_MonthlyPasses_SlotId] ON [MonthlyPasses] ([SlotId]) WHERE [SlotId] IS NOT NULL AND [Status] = N''Active''');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261002083848_AddDocumentCoverage'
)
BEGIN
    CREATE INDEX [IX_MonthlyPasses_UserId] ON [MonthlyPasses] ([UserId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261002083848_AddDocumentCoverage'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20261002083848_AddDocumentCoverage', N'10.0.12');
END;

COMMIT;
GO

