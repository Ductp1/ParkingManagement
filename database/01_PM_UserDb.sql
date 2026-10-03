-- Database cua UserService: PM_UserDb. Mo trong SSMS va bam F5 (chay lai nhieu lan duoc).
IF DB_ID(N'PM_UserDb') IS NULL CREATE DATABASE [PM_UserDb];
GO
USE [PM_UserDb];
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
    WHERE [MigrationId] = N'20261002080038_InitialCreate'
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
    WHERE [MigrationId] = N'20261002080038_InitialCreate'
)
BEGIN
    CREATE TABLE [Users] (
        [Id] int NOT NULL IDENTITY,
        [FullName] nvarchar(150) NOT NULL,
        [Email] nvarchar(256) NULL,
        [PhoneNumber] nvarchar(20) NULL,
        [EmailConfirmed] bit NOT NULL,
        [PhoneConfirmed] bit NOT NULL,
        [PasswordHash] nvarchar(200) NOT NULL,
        [Status] nvarchar(40) NOT NULL,
        [LockedUntilUtc] datetime2 NULL,
        [LockReason] nvarchar(500) NULL,
        [FailedLoginCount] int NOT NULL,
        [LastLoginAtUtc] datetime2 NULL,
        [AvatarUrl] nvarchar(512) NULL,
        [KycStatus] nvarchar(40) NOT NULL,
        [KycDocumentType] nvarchar(40) NULL,
        [KycNumberEncrypted] nvarchar(512) NULL,
        [KycFrontImageUrl] nvarchar(512) NULL,
        [KycBackImageUrl] nvarchar(512) NULL,
        [KycVerifiedAtUtc] datetime2 NULL,
        [IsDeleted] bit NOT NULL,
        [DeletedAtUtc] datetime2 NULL,
        [CreatedAtUtc] datetime2 NOT NULL,
        [UpdatedAtUtc] datetime2 NULL,
        CONSTRAINT [PK_Users] PRIMARY KEY ([Id]),
        CONSTRAINT [CK_Users_EmailOrPhone] CHECK ([Email] IS NOT NULL OR [PhoneNumber] IS NOT NULL)
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261002080038_InitialCreate'
)
BEGIN
    CREATE TABLE [OtpCodes] (
        [Id] int NOT NULL IDENTITY,
        [UserId] int NULL,
        [Destination] nvarchar(256) NOT NULL,
        [CodeHash] nvarchar(200) NOT NULL,
        [Purpose] nvarchar(40) NOT NULL,
        [ExpiresAtUtc] datetime2 NOT NULL,
        [AttemptCount] int NOT NULL,
        [ConsumedAtUtc] datetime2 NULL,
        [CreatedAtUtc] datetime2 NOT NULL,
        [UpdatedAtUtc] datetime2 NULL,
        CONSTRAINT [PK_OtpCodes] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_OtpCodes_Users_UserId] FOREIGN KEY ([UserId]) REFERENCES [Users] ([Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261002080038_InitialCreate'
)
BEGIN
    CREATE TABLE [OwnerProfiles] (
        [Id] int NOT NULL IDENTITY,
        [UserId] int NOT NULL,
        [BusinessName] nvarchar(200) NOT NULL,
        [TaxCode] nvarchar(20) NULL,
        [BusinessAddress] nvarchar(500) NULL,
        [BankBin] nvarchar(10) NOT NULL,
        [BankName] nvarchar(100) NOT NULL,
        [BankAccountNumber] nvarchar(30) NOT NULL,
        [BankAccountName] nvarchar(100) NOT NULL,
        [CommissionRateOverride] decimal(5,4) NULL,
        [IsLocked] bit NOT NULL,
        [CreatedAtUtc] datetime2 NOT NULL,
        [UpdatedAtUtc] datetime2 NULL,
        CONSTRAINT [PK_OwnerProfiles] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_OwnerProfiles_Users_UserId] FOREIGN KEY ([UserId]) REFERENCES [Users] ([Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261002080038_InitialCreate'
)
BEGIN
    CREATE TABLE [RefreshTokens] (
        [Id] int NOT NULL IDENTITY,
        [UserId] int NOT NULL,
        [TokenHash] nvarchar(200) NOT NULL,
        [ExpiresAtUtc] datetime2 NOT NULL,
        [RevokedAtUtc] datetime2 NULL,
        [ReplacedByTokenHash] nvarchar(200) NULL,
        [CreatedByIp] nvarchar(45) NULL,
        [CreatedAtUtc] datetime2 NOT NULL,
        [UpdatedAtUtc] datetime2 NULL,
        CONSTRAINT [PK_RefreshTokens] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_RefreshTokens_Users_UserId] FOREIGN KEY ([UserId]) REFERENCES [Users] ([Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261002080038_InitialCreate'
)
BEGIN
    CREATE TABLE [UserRoles] (
        [UserId] int NOT NULL,
        [Role] nvarchar(40) NOT NULL,
        [GrantedAtUtc] datetime2 NOT NULL,
        CONSTRAINT [PK_UserRoles] PRIMARY KEY ([UserId], [Role]),
        CONSTRAINT [FK_UserRoles_Users_UserId] FOREIGN KEY ([UserId]) REFERENCES [Users] ([Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261002080038_InitialCreate'
)
BEGIN
    CREATE TABLE [StaffAssignments] (
        [Id] int NOT NULL IDENTITY,
        [StaffUserId] int NOT NULL,
        [OwnerProfileId] int NOT NULL,
        [ParkingLotId] int NOT NULL,
        [IsActive] bit NOT NULL,
        [RevokedAtUtc] datetime2 NULL,
        [CreatedAtUtc] datetime2 NOT NULL,
        [UpdatedAtUtc] datetime2 NULL,
        CONSTRAINT [PK_StaffAssignments] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_StaffAssignments_OwnerProfiles_OwnerProfileId] FOREIGN KEY ([OwnerProfileId]) REFERENCES [OwnerProfiles] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_StaffAssignments_Users_StaffUserId] FOREIGN KEY ([StaffUserId]) REFERENCES [Users] ([Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261002080038_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_OtpCodes_Destination_Purpose_ExpiresAtUtc] ON [OtpCodes] ([Destination], [Purpose], [ExpiresAtUtc]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261002080038_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_OtpCodes_UserId] ON [OtpCodes] ([UserId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261002080038_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_OutboxMessages_ProcessedAtUtc_OccurredAtUtc] ON [OutboxMessages] ([ProcessedAtUtc], [OccurredAtUtc]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261002080038_InitialCreate'
)
BEGIN
    CREATE UNIQUE INDEX [IX_OwnerProfiles_UserId] ON [OwnerProfiles] ([UserId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261002080038_InitialCreate'
)
BEGIN
    CREATE UNIQUE INDEX [IX_RefreshTokens_TokenHash] ON [RefreshTokens] ([TokenHash]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261002080038_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_RefreshTokens_UserId] ON [RefreshTokens] ([UserId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261002080038_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_StaffAssignments_OwnerProfileId] ON [StaffAssignments] ([OwnerProfileId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261002080038_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_StaffAssignments_ParkingLotId] ON [StaffAssignments] ([ParkingLotId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261002080038_InitialCreate'
)
BEGIN
    EXEC(N'CREATE UNIQUE INDEX [IX_StaffAssignments_StaffUserId_ParkingLotId] ON [StaffAssignments] ([StaffUserId], [ParkingLotId]) WHERE [IsActive] = 1');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261002080038_InitialCreate'
)
BEGIN
    EXEC(N'CREATE UNIQUE INDEX [IX_Users_Email] ON [Users] ([Email]) WHERE [Email] IS NOT NULL');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261002080038_InitialCreate'
)
BEGIN
    EXEC(N'CREATE UNIQUE INDEX [IX_Users_PhoneNumber] ON [Users] ([PhoneNumber]) WHERE [PhoneNumber] IS NOT NULL');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261002080038_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_Users_Status] ON [Users] ([Status]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261002080038_InitialCreate'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20261002080038_InitialCreate', N'10.0.12');
END;

COMMIT;
GO

BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261002083836_AddDocumentCoverage'
)
BEGIN
    ALTER TABLE [OwnerProfiles] ADD [ExitEffectiveAtUtc] datetime2 NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261002083836_AddDocumentCoverage'
)
BEGIN
    ALTER TABLE [OwnerProfiles] ADD [ExitRequestedAtUtc] datetime2 NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261002083836_AddDocumentCoverage'
)
BEGIN
    ALTER TABLE [OwnerProfiles] ADD [Status] nvarchar(40) NOT NULL DEFAULT N'Active';
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261002083836_AddDocumentCoverage'
)
BEGIN
    CREATE TABLE [DataSubjectRequests] (
        [Id] int NOT NULL IDENTITY,
        [UserId] int NOT NULL,
        [RequestType] nvarchar(40) NOT NULL,
        [Status] nvarchar(40) NOT NULL,
        [Reason] nvarchar(1000) NULL,
        [DueAtUtc] datetime2 NOT NULL,
        [HandledByUserId] int NULL,
        [CompletedAtUtc] datetime2 NULL,
        [ResultNote] nvarchar(1000) NULL,
        [ExportFileUrl] nvarchar(512) NULL,
        [CreatedAtUtc] datetime2 NOT NULL,
        [UpdatedAtUtc] datetime2 NULL,
        CONSTRAINT [PK_DataSubjectRequests] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_DataSubjectRequests_Users_UserId] FOREIGN KEY ([UserId]) REFERENCES [Users] ([Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261002083836_AddDocumentCoverage'
)
BEGIN
    CREATE TABLE [SecurityEvents] (
        [Id] int NOT NULL IDENTITY,
        [UserId] int NULL,
        [EventType] nvarchar(40) NOT NULL,
        [Identifier] nvarchar(256) NULL,
        [IpAddress] nvarchar(45) NULL,
        [UserAgent] nvarchar(512) NULL,
        [Detail] nvarchar(1000) NULL,
        [CreatedAtUtc] datetime2 NOT NULL,
        [UpdatedAtUtc] datetime2 NULL,
        CONSTRAINT [PK_SecurityEvents] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_SecurityEvents_Users_UserId] FOREIGN KEY ([UserId]) REFERENCES [Users] ([Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261002083836_AddDocumentCoverage'
)
BEGIN
    CREATE INDEX [IX_DataSubjectRequests_Status_DueAtUtc] ON [DataSubjectRequests] ([Status], [DueAtUtc]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261002083836_AddDocumentCoverage'
)
BEGIN
    CREATE INDEX [IX_DataSubjectRequests_UserId] ON [DataSubjectRequests] ([UserId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261002083836_AddDocumentCoverage'
)
BEGIN
    CREATE INDEX [IX_SecurityEvents_Identifier_EventType_CreatedAtUtc] ON [SecurityEvents] ([Identifier], [EventType], [CreatedAtUtc]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261002083836_AddDocumentCoverage'
)
BEGIN
    CREATE INDEX [IX_SecurityEvents_UserId_CreatedAtUtc] ON [SecurityEvents] ([UserId], [CreatedAtUtc]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261002083836_AddDocumentCoverage'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20261002083836_AddDocumentCoverage', N'10.0.12');
END;

COMMIT;
GO

