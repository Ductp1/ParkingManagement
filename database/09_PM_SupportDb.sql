-- Database cua SupportService: PM_SupportDb. Mo trong SSMS va bam F5 (chay lai nhieu lan duoc).
IF DB_ID(N'PM_SupportDb') IS NULL CREATE DATABASE [PM_SupportDb];
GO
USE [PM_SupportDb];
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
    WHERE [MigrationId] = N'20261002080126_InitialCreate'
)
BEGIN
    CREATE TABLE [Complaints] (
        [Id] int NOT NULL IDENTITY,
        [Code] nvarchar(30) NOT NULL,
        [UserId] int NOT NULL,
        [BookingId] int NULL,
        [BookingCode] nvarchar(30) NULL,
        [ParkingLotId] int NOT NULL,
        [OwnerProfileId] int NOT NULL,
        [Category] nvarchar(40) NOT NULL,
        [Description] nvarchar(4000) NOT NULL,
        [EvidenceUrlsJson] nvarchar(max) NULL,
        [RequestedAmount] decimal(18,2) NULL,
        [Status] nvarchar(40) NOT NULL,
        [OwnerResponse] nvarchar(4000) NULL,
        [OwnerResponseDueAtUtc] datetime2 NULL,
        [OwnerRespondedAtUtc] datetime2 NULL,
        [EscalatedAtUtc] datetime2 NULL,
        [Resolution] nvarchar(4000) NULL,
        [ResolvedRefundAmount] decimal(18,2) NULL,
        [ResolvedByUserId] int NULL,
        [ResolvedAtUtc] datetime2 NULL,
        [PayoutHeld] bit NOT NULL,
        [CreatedAtUtc] datetime2 NOT NULL,
        [UpdatedAtUtc] datetime2 NULL,
        CONSTRAINT [PK_Complaints] PRIMARY KEY ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261002080126_InitialCreate'
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
    WHERE [MigrationId] = N'20261002080126_InitialCreate'
)
BEGIN
    CREATE TABLE [Reviews] (
        [Id] int NOT NULL IDENTITY,
        [BookingId] int NOT NULL,
        [UserId] int NOT NULL,
        [ReviewerName] nvarchar(150) NOT NULL,
        [ParkingLotId] int NOT NULL,
        [OwnerProfileId] int NOT NULL,
        [Rating] tinyint NOT NULL,
        [Comment] nvarchar(2000) NULL,
        [OwnerReply] nvarchar(2000) NULL,
        [OwnerRepliedAtUtc] datetime2 NULL,
        [IsFlagged] bit NOT NULL,
        [FlagReason] nvarchar(500) NULL,
        [IsHidden] bit NOT NULL,
        [IsDeleted] bit NOT NULL,
        [DeletedAtUtc] datetime2 NULL,
        [CreatedAtUtc] datetime2 NOT NULL,
        [UpdatedAtUtc] datetime2 NULL,
        CONSTRAINT [PK_Reviews] PRIMARY KEY ([Id]),
        CONSTRAINT [CK_Reviews_Rating] CHECK ([Rating] BETWEEN 1 AND 5)
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261002080126_InitialCreate'
)
BEGIN
    EXEC(N'CREATE INDEX [IX_Complaints_BookingId] ON [Complaints] ([BookingId]) WHERE [BookingId] IS NOT NULL');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261002080126_InitialCreate'
)
BEGIN
    CREATE UNIQUE INDEX [IX_Complaints_Code] ON [Complaints] ([Code]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261002080126_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_Complaints_OwnerProfileId_Status] ON [Complaints] ([OwnerProfileId], [Status]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261002080126_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_Complaints_Status_OwnerResponseDueAtUtc] ON [Complaints] ([Status], [OwnerResponseDueAtUtc]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261002080126_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_Complaints_UserId] ON [Complaints] ([UserId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261002080126_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_OutboxMessages_ProcessedAtUtc_OccurredAtUtc] ON [OutboxMessages] ([ProcessedAtUtc], [OccurredAtUtc]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261002080126_InitialCreate'
)
BEGIN
    CREATE UNIQUE INDEX [IX_Reviews_BookingId] ON [Reviews] ([BookingId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261002080126_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_Reviews_ParkingLotId_IsHidden_CreatedAtUtc] ON [Reviews] ([ParkingLotId], [IsHidden], [CreatedAtUtc]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261002080126_InitialCreate'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20261002080126_InitialCreate', N'10.0.12');
END;

COMMIT;
GO

BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261002083909_AddDocumentCoverage'
)
BEGIN
    CREATE TABLE [ComplaintMessages] (
        [Id] int NOT NULL IDENTITY,
        [ComplaintId] int NOT NULL,
        [SenderParty] nvarchar(40) NOT NULL,
        [SenderUserId] int NULL,
        [Message] nvarchar(4000) NOT NULL,
        [AttachmentUrlsJson] nvarchar(max) NULL,
        [IsInternal] bit NOT NULL,
        [CreatedAtUtc] datetime2 NOT NULL,
        [UpdatedAtUtc] datetime2 NULL,
        CONSTRAINT [PK_ComplaintMessages] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_ComplaintMessages_Complaints_ComplaintId] FOREIGN KEY ([ComplaintId]) REFERENCES [Complaints] ([Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261002083909_AddDocumentCoverage'
)
BEGIN
    CREATE TABLE [FaqArticles] (
        [Id] int NOT NULL IDENTITY,
        [Category] nvarchar(50) NOT NULL,
        [Question] nvarchar(500) NOT NULL,
        [Answer] nvarchar(4000) NOT NULL,
        [Audience] nvarchar(20) NOT NULL,
        [SortOrder] int NOT NULL,
        [IsPublished] bit NOT NULL,
        [ViewCount] int NOT NULL,
        [CreatedAtUtc] datetime2 NOT NULL,
        [UpdatedAtUtc] datetime2 NULL,
        CONSTRAINT [PK_FaqArticles] PRIMARY KEY ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261002083909_AddDocumentCoverage'
)
BEGIN
    CREATE INDEX [IX_ComplaintMessages_ComplaintId_CreatedAtUtc] ON [ComplaintMessages] ([ComplaintId], [CreatedAtUtc]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261002083909_AddDocumentCoverage'
)
BEGIN
    CREATE INDEX [IX_FaqArticles_IsPublished_Category_SortOrder] ON [FaqArticles] ([IsPublished], [Category], [SortOrder]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261002083909_AddDocumentCoverage'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20261002083909_AddDocumentCoverage', N'10.0.12');
END;

COMMIT;
GO

