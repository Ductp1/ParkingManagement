-- Database cua VehicleService: PM_VehicleDb. Mo trong SSMS va bam F5 (chay lai nhieu lan duoc).
IF DB_ID(N'PM_VehicleDb') IS NULL CREATE DATABASE [PM_VehicleDb];
GO
USE [PM_VehicleDb];
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
    WHERE [MigrationId] = N'20261002080044_InitialCreate'
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
    WHERE [MigrationId] = N'20261002080044_InitialCreate'
)
BEGIN
    CREATE TABLE [Vehicles] (
        [Id] int NOT NULL IDENTITY,
        [UserId] int NOT NULL,
        [PlateNumber] nvarchar(15) NOT NULL,
        [PlateDisplay] nvarchar(20) NOT NULL,
        [VehicleType] nvarchar(40) NOT NULL,
        [FuelType] nvarchar(40) NOT NULL,
        [Brand] nvarchar(50) NULL,
        [Model] nvarchar(50) NULL,
        [Color] nvarchar(30) NULL,
        [LengthCm] int NULL,
        [WidthCm] int NULL,
        [HeightCm] int NOT NULL,
        [IsEmergencyVehicle] bit NOT NULL,
        [IsDefault] bit NOT NULL,
        [IsDeleted] bit NOT NULL,
        [DeletedAtUtc] datetime2 NULL,
        [CreatedAtUtc] datetime2 NOT NULL,
        [UpdatedAtUtc] datetime2 NULL,
        CONSTRAINT [PK_Vehicles] PRIMARY KEY ([Id]),
        CONSTRAINT [CK_Vehicles_Height] CHECK ([HeightCm] > 0)
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261002080044_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_OutboxMessages_ProcessedAtUtc_OccurredAtUtc] ON [OutboxMessages] ([ProcessedAtUtc], [OccurredAtUtc]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261002080044_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_Vehicles_PlateNumber] ON [Vehicles] ([PlateNumber]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261002080044_InitialCreate'
)
BEGIN
    EXEC(N'CREATE UNIQUE INDEX [IX_Vehicles_UserId_PlateNumber] ON [Vehicles] ([UserId], [PlateNumber]) WHERE [IsDeleted] = 0');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261002080044_InitialCreate'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20261002080044_InitialCreate', N'10.0.12');
END;

COMMIT;
GO

BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261002083840_AddDocumentCoverage'
)
BEGIN
    CREATE TABLE [VehicleShares] (
        [Id] int NOT NULL IDENTITY,
        [VehicleId] int NOT NULL,
        [OwnerUserId] int NOT NULL,
        [SharedWithUserId] int NOT NULL,
        [Status] nvarchar(40) NOT NULL,
        [ExpiresAtUtc] datetime2 NULL,
        [RevokedAtUtc] datetime2 NULL,
        [CreatedAtUtc] datetime2 NOT NULL,
        [UpdatedAtUtc] datetime2 NULL,
        CONSTRAINT [PK_VehicleShares] PRIMARY KEY ([Id]),
        CONSTRAINT [CK_VehicleShares_NotSelf] CHECK ([OwnerUserId] <> [SharedWithUserId]),
        CONSTRAINT [FK_VehicleShares_Vehicles_VehicleId] FOREIGN KEY ([VehicleId]) REFERENCES [Vehicles] ([Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261002083840_AddDocumentCoverage'
)
BEGIN
    CREATE INDEX [IX_VehicleShares_SharedWithUserId] ON [VehicleShares] ([SharedWithUserId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261002083840_AddDocumentCoverage'
)
BEGIN
    EXEC(N'CREATE UNIQUE INDEX [IX_VehicleShares_VehicleId_SharedWithUserId] ON [VehicleShares] ([VehicleId], [SharedWithUserId]) WHERE [Status] <> N''Revoked''');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261002083840_AddDocumentCoverage'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20261002083840_AddDocumentCoverage', N'10.0.12');
END;

COMMIT;
GO

