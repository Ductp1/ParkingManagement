-- Database cua PaymentService: PM_PaymentDb. Mo trong SSMS va bam F5 (chay lai nhieu lan duoc).
IF DB_ID(N'PM_PaymentDb') IS NULL CREATE DATABASE [PM_PaymentDb];
GO
USE [PM_PaymentDb];
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
    WHERE [MigrationId] = N'20261002080102_InitialCreate'
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
    WHERE [MigrationId] = N'20261002080102_InitialCreate'
)
BEGIN
    CREATE TABLE [Payments] (
        [Id] int NOT NULL IDENTITY,
        [Code] nvarchar(30) NOT NULL,
        [BookingId] int NULL,
        [BookingCode] nvarchar(30) NULL,
        [ParkingSessionId] int NULL,
        [UserId] int NULL,
        [ParkingLotId] int NOT NULL,
        [OwnerProfileId] int NOT NULL,
        [Purpose] nvarchar(40) NOT NULL,
        [Method] nvarchar(40) NOT NULL,
        [Status] nvarchar(40) NOT NULL,
        [Amount] decimal(18,2) NOT NULL,
        [RefundedAmount] decimal(18,2) NOT NULL,
        [Currency] nvarchar(3) NOT NULL,
        [IdempotencyKey] nvarchar(100) NOT NULL,
        [ProviderTransactionId] nvarchar(100) NULL,
        [ProviderResponseCode] nvarchar(20) NULL,
        [RawCallbackJson] nvarchar(max) NULL,
        [TransferContent] nvarchar(100) NULL,
        [PaidAtUtc] datetime2 NULL,
        [ExpiresAtUtc] datetime2 NULL,
        [ConfirmedByStaffId] int NULL,
        [RetryCount] int NOT NULL,
        [CreatedAtUtc] datetime2 NOT NULL,
        [UpdatedAtUtc] datetime2 NULL,
        CONSTRAINT [PK_Payments] PRIMARY KEY ([Id]),
        CONSTRAINT [CK_Payments_Amount] CHECK ([Amount] >= 0 AND [RefundedAmount] >= 0 AND [RefundedAmount] <= [Amount]),
        CONSTRAINT [CK_Payments_Target] CHECK ([BookingId] IS NOT NULL OR [ParkingSessionId] IS NOT NULL)
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261002080102_InitialCreate'
)
BEGIN
    CREATE TABLE [Promotions] (
        [Id] int NOT NULL IDENTITY,
        [Code] nvarchar(30) NOT NULL,
        [Name] nvarchar(150) NOT NULL,
        [ParkingLotId] int NULL,
        [OwnerProfileId] int NULL,
        [Sponsor] nvarchar(40) NOT NULL,
        [DiscountType] nvarchar(40) NOT NULL,
        [DiscountValue] decimal(18,2) NOT NULL,
        [MaxDiscountAmount] decimal(18,2) NULL,
        [MinOrderAmount] decimal(18,2) NULL,
        [StartsAtUtc] datetime2 NOT NULL,
        [EndsAtUtc] datetime2 NOT NULL,
        [UsageLimit] int NULL,
        [UsedCount] int NOT NULL,
        [IsActive] bit NOT NULL,
        [CreatedAtUtc] datetime2 NOT NULL,
        [UpdatedAtUtc] datetime2 NULL,
        CONSTRAINT [PK_Promotions] PRIMARY KEY ([Id]),
        CONSTRAINT [CK_Promotions_Period] CHECK ([EndsAtUtc] > [StartsAtUtc])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261002080102_InitialCreate'
)
BEGIN
    CREATE TABLE [RateCards] (
        [Id] int NOT NULL IDENTITY,
        [ParkingLotId] int NOT NULL,
        [OwnerProfileId] int NOT NULL,
        [Name] nvarchar(100) NOT NULL,
        [EffectiveFromUtc] datetime2 NOT NULL,
        [EffectiveToUtc] datetime2 NULL,
        [IsActive] bit NOT NULL,
        [OvernightSurcharge] decimal(18,2) NOT NULL,
        [WeekendMultiplier] decimal(4,2) NOT NULL,
        [HolidayMultiplier] decimal(4,2) NOT NULL,
        [OversizedMultiplier] decimal(4,2) NOT NULL,
        [OverstayMultiplier] decimal(4,2) NOT NULL,
        [MaxDailyAmount] decimal(18,2) NULL,
        [CreatedAtUtc] datetime2 NOT NULL,
        [UpdatedAtUtc] datetime2 NULL,
        CONSTRAINT [PK_RateCards] PRIMARY KEY ([Id]),
        CONSTRAINT [CK_RateCards_Multipliers] CHECK ([WeekendMultiplier] > 0 AND [HolidayMultiplier] > 0 AND [OversizedMultiplier] >= 1 AND [OverstayMultiplier] >= 1)
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261002080102_InitialCreate'
)
BEGIN
    CREATE TABLE [Settlements] (
        [Id] int NOT NULL IDENTITY,
        [OwnerProfileId] int NOT NULL,
        [PeriodStart] date NOT NULL,
        [PeriodEnd] date NOT NULL,
        [GrossAmount] decimal(18,2) NOT NULL,
        [CommissionRate] decimal(5,4) NOT NULL,
        [CommissionAmount] decimal(18,2) NOT NULL,
        [RefundAmount] decimal(18,2) NOT NULL,
        [HeldAmount] decimal(18,2) NOT NULL,
        [AdjustmentAmount] decimal(18,2) NOT NULL,
        [NetPayout] decimal(18,2) NOT NULL,
        [Status] nvarchar(40) NOT NULL,
        [ApprovedByUserId] int NULL,
        [ApprovedAtUtc] datetime2 NULL,
        [PaidAtUtc] datetime2 NULL,
        [PayoutReference] nvarchar(100) NULL,
        [CreatedAtUtc] datetime2 NOT NULL,
        [UpdatedAtUtc] datetime2 NULL,
        CONSTRAINT [PK_Settlements] PRIMARY KEY ([Id]),
        CONSTRAINT [CK_Settlements_Period] CHECK ([PeriodEnd] >= [PeriodStart])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261002080102_InitialCreate'
)
BEGIN
    CREATE TABLE [Invoices] (
        [Id] int NOT NULL IDENTITY,
        [PaymentId] int NOT NULL,
        [InvoiceNumber] nvarchar(30) NOT NULL,
        [BuyerName] nvarchar(200) NOT NULL,
        [BuyerTaxCode] nvarchar(20) NULL,
        [BuyerEmail] nvarchar(256) NULL,
        [SubTotal] decimal(18,2) NOT NULL,
        [VatAmount] decimal(18,2) NOT NULL,
        [Total] decimal(18,2) NOT NULL,
        [IssuedAtUtc] datetime2 NOT NULL,
        [PdfUrl] nvarchar(512) NULL,
        [CreatedAtUtc] datetime2 NOT NULL,
        [UpdatedAtUtc] datetime2 NULL,
        CONSTRAINT [PK_Invoices] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_Invoices_Payments_PaymentId] FOREIGN KEY ([PaymentId]) REFERENCES [Payments] ([Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261002080102_InitialCreate'
)
BEGIN
    CREATE TABLE [Refunds] (
        [Id] int NOT NULL IDENTITY,
        [PaymentId] int NOT NULL,
        [Amount] decimal(18,2) NOT NULL,
        [Reason] nvarchar(40) NOT NULL,
        [Note] nvarchar(1000) NULL,
        [Status] nvarchar(40) NOT NULL,
        [ComplaintId] int NULL,
        [RequestedByUserId] int NULL,
        [ApprovedByUserId] int NULL,
        [ProviderRefundId] nvarchar(100) NULL,
        [ProcessedAtUtc] datetime2 NULL,
        [CreatedAtUtc] datetime2 NOT NULL,
        [UpdatedAtUtc] datetime2 NULL,
        CONSTRAINT [PK_Refunds] PRIMARY KEY ([Id]),
        CONSTRAINT [CK_Refunds_Amount] CHECK ([Amount] > 0),
        CONSTRAINT [FK_Refunds_Payments_PaymentId] FOREIGN KEY ([PaymentId]) REFERENCES [Payments] ([Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261002080102_InitialCreate'
)
BEGIN
    CREATE TABLE [RateRules] (
        [Id] int NOT NULL IDENTITY,
        [RateCardId] int NOT NULL,
        [VehicleType] nvarchar(40) NOT NULL,
        [FromMinute] int NOT NULL,
        [ToMinute] int NULL,
        [BlockMinutes] int NOT NULL,
        [PricePerBlock] decimal(18,2) NOT NULL,
        [SortOrder] int NOT NULL,
        [CreatedAtUtc] datetime2 NOT NULL,
        [UpdatedAtUtc] datetime2 NULL,
        CONSTRAINT [PK_RateRules] PRIMARY KEY ([Id]),
        CONSTRAINT [CK_RateRules_Block] CHECK ([BlockMinutes] > 0 AND [PricePerBlock] >= 0),
        CONSTRAINT [CK_RateRules_Range] CHECK ([ToMinute] IS NULL OR [ToMinute] > [FromMinute]),
        CONSTRAINT [FK_RateRules_RateCards_RateCardId] FOREIGN KEY ([RateCardId]) REFERENCES [RateCards] ([Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261002080102_InitialCreate'
)
BEGIN
    CREATE TABLE [FinancialAdjustments] (
        [Id] int NOT NULL IDENTITY,
        [OwnerProfileId] int NOT NULL,
        [SettlementId] int NULL,
        [Amount] decimal(18,2) NOT NULL,
        [Reason] nvarchar(500) NOT NULL,
        [CreatedByUserId] int NOT NULL,
        [CreatedAtUtc] datetime2 NOT NULL,
        [UpdatedAtUtc] datetime2 NULL,
        CONSTRAINT [PK_FinancialAdjustments] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_FinancialAdjustments_Settlements_SettlementId] FOREIGN KEY ([SettlementId]) REFERENCES [Settlements] ([Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261002080102_InitialCreate'
)
BEGIN
    CREATE TABLE [SettlementLines] (
        [Id] int NOT NULL IDENTITY,
        [SettlementId] int NOT NULL,
        [LineType] nvarchar(40) NOT NULL,
        [PaymentId] int NULL,
        [RefundId] int NULL,
        [ParkingLotId] int NOT NULL,
        [ReferenceCode] nvarchar(30) NULL,
        [Amount] decimal(18,2) NOT NULL,
        [CommissionAmount] decimal(18,2) NOT NULL,
        [CreatedAtUtc] datetime2 NOT NULL,
        [UpdatedAtUtc] datetime2 NULL,
        CONSTRAINT [PK_SettlementLines] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_SettlementLines_Payments_PaymentId] FOREIGN KEY ([PaymentId]) REFERENCES [Payments] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_SettlementLines_Refunds_RefundId] FOREIGN KEY ([RefundId]) REFERENCES [Refunds] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_SettlementLines_Settlements_SettlementId] FOREIGN KEY ([SettlementId]) REFERENCES [Settlements] ([Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261002080102_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_FinancialAdjustments_OwnerProfileId] ON [FinancialAdjustments] ([OwnerProfileId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261002080102_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_FinancialAdjustments_SettlementId] ON [FinancialAdjustments] ([SettlementId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261002080102_InitialCreate'
)
BEGIN
    CREATE UNIQUE INDEX [IX_Invoices_InvoiceNumber] ON [Invoices] ([InvoiceNumber]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261002080102_InitialCreate'
)
BEGIN
    CREATE UNIQUE INDEX [IX_Invoices_PaymentId] ON [Invoices] ([PaymentId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261002080102_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_OutboxMessages_ProcessedAtUtc_OccurredAtUtc] ON [OutboxMessages] ([ProcessedAtUtc], [OccurredAtUtc]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261002080102_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_Payments_BookingId] ON [Payments] ([BookingId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261002080102_InitialCreate'
)
BEGIN
    CREATE UNIQUE INDEX [IX_Payments_Code] ON [Payments] ([Code]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261002080102_InitialCreate'
)
BEGIN
    CREATE UNIQUE INDEX [IX_Payments_IdempotencyKey] ON [Payments] ([IdempotencyKey]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261002080102_InitialCreate'
)
BEGIN
    EXEC(N'CREATE UNIQUE INDEX [IX_Payments_Method_ProviderTransactionId] ON [Payments] ([Method], [ProviderTransactionId]) WHERE [ProviderTransactionId] IS NOT NULL');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261002080102_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_Payments_OwnerProfileId_Status_PaidAtUtc] ON [Payments] ([OwnerProfileId], [Status], [PaidAtUtc]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261002080102_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_Payments_ParkingSessionId] ON [Payments] ([ParkingSessionId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261002080102_InitialCreate'
)
BEGIN
    CREATE UNIQUE INDEX [IX_Promotions_Code] ON [Promotions] ([Code]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261002080102_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_Promotions_ParkingLotId_IsActive] ON [Promotions] ([ParkingLotId], [IsActive]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261002080102_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_RateCards_OwnerProfileId] ON [RateCards] ([OwnerProfileId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261002080102_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_RateCards_ParkingLotId_IsActive_EffectiveFromUtc] ON [RateCards] ([ParkingLotId], [IsActive], [EffectiveFromUtc]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261002080102_InitialCreate'
)
BEGIN
    CREATE UNIQUE INDEX [IX_RateRules_RateCardId_VehicleType_FromMinute] ON [RateRules] ([RateCardId], [VehicleType], [FromMinute]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261002080102_InitialCreate'
)
BEGIN
    EXEC(N'CREATE INDEX [IX_Refunds_ComplaintId] ON [Refunds] ([ComplaintId]) WHERE [ComplaintId] IS NOT NULL');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261002080102_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_Refunds_PaymentId_Status] ON [Refunds] ([PaymentId], [Status]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261002080102_InitialCreate'
)
BEGIN
    EXEC(N'CREATE INDEX [IX_SettlementLines_PaymentId] ON [SettlementLines] ([PaymentId]) WHERE [PaymentId] IS NOT NULL');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261002080102_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_SettlementLines_RefundId] ON [SettlementLines] ([RefundId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261002080102_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_SettlementLines_SettlementId] ON [SettlementLines] ([SettlementId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261002080102_InitialCreate'
)
BEGIN
    CREATE UNIQUE INDEX [IX_Settlements_OwnerProfileId_PeriodStart] ON [Settlements] ([OwnerProfileId], [PeriodStart]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261002080102_InitialCreate'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20261002080102_InitialCreate', N'10.0.12');
END;

COMMIT;
GO

BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261002083852_AddDocumentCoverage'
)
BEGIN
    DROP INDEX [IX_Invoices_PaymentId] ON [Invoices];
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261002083852_AddDocumentCoverage'
)
BEGIN
    DECLARE @var nvarchar(max);
    SELECT @var = QUOTENAME([d].[name])
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[Invoices]') AND [c].[name] = N'PaymentId');
    IF @var IS NOT NULL EXEC(N'ALTER TABLE [Invoices] DROP CONSTRAINT ' + @var + ';');
    ALTER TABLE [Invoices] ALTER COLUMN [PaymentId] int NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261002083852_AddDocumentCoverage'
)
BEGIN
    ALTER TABLE [Invoices] ADD [SettlementId] int NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261002083852_AddDocumentCoverage'
)
BEGIN
    ALTER TABLE [Invoices] ADD [Type] nvarchar(40) NOT NULL DEFAULT N'ParkingFee';
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261002083852_AddDocumentCoverage'
)
BEGIN
    CREATE TABLE [CompensationVouchers] (
        [Id] int NOT NULL IDENTITY,
        [Code] nvarchar(30) NOT NULL,
        [UserId] int NOT NULL,
        [Amount] decimal(18,2) NOT NULL,
        [Reason] nvarchar(500) NOT NULL,
        [BookingId] int NULL,
        [ComplaintId] int NULL,
        [ChargedToOwnerProfileId] int NULL,
        [ExpiresAtUtc] datetime2 NOT NULL,
        [Status] nvarchar(40) NOT NULL,
        [RedeemedAtUtc] datetime2 NULL,
        [RedeemedPaymentId] int NULL,
        [IssuedByUserId] int NOT NULL,
        [CreatedAtUtc] datetime2 NOT NULL,
        [UpdatedAtUtc] datetime2 NULL,
        CONSTRAINT [PK_CompensationVouchers] PRIMARY KEY ([Id]),
        CONSTRAINT [CK_CompensationVouchers_Amount] CHECK ([Amount] > 0),
        CONSTRAINT [FK_CompensationVouchers_Payments_RedeemedPaymentId] FOREIGN KEY ([RedeemedPaymentId]) REFERENCES [Payments] ([Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261002083852_AddDocumentCoverage'
)
BEGIN
    CREATE TABLE [Holidays] (
        [Id] int NOT NULL IDENTITY,
        [Date] date NOT NULL,
        [Name] nvarchar(100) NOT NULL,
        [City] nvarchar(100) NULL,
        [CreatedAtUtc] datetime2 NOT NULL,
        [UpdatedAtUtc] datetime2 NULL,
        CONSTRAINT [PK_Holidays] PRIMARY KEY ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261002083852_AddDocumentCoverage'
)
BEGIN
    CREATE TABLE [PaymentCallbackLogs] (
        [Id] int NOT NULL IDENTITY,
        [PaymentId] int NULL,
        [Provider] nvarchar(40) NOT NULL,
        [ProviderTransactionId] nvarchar(100) NULL,
        [RawPayload] nvarchar(max) NOT NULL,
        [SignatureValid] bit NOT NULL,
        [IsDuplicate] bit NOT NULL,
        [ResultCode] nvarchar(20) NULL,
        [ProcessingError] nvarchar(1000) NULL,
        [SourceIp] nvarchar(45) NULL,
        [CreatedAtUtc] datetime2 NOT NULL,
        [UpdatedAtUtc] datetime2 NULL,
        CONSTRAINT [PK_PaymentCallbackLogs] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_PaymentCallbackLogs_Payments_PaymentId] FOREIGN KEY ([PaymentId]) REFERENCES [Payments] ([Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261002083852_AddDocumentCoverage'
)
BEGIN
    CREATE TABLE [PromotionRedemptions] (
        [Id] int NOT NULL IDENTITY,
        [PromotionId] int NOT NULL,
        [UserId] int NOT NULL,
        [BookingId] int NOT NULL,
        [PaymentId] int NULL,
        [DiscountAmount] decimal(18,2) NOT NULL,
        [IsReverted] bit NOT NULL,
        [CreatedAtUtc] datetime2 NOT NULL,
        [UpdatedAtUtc] datetime2 NULL,
        CONSTRAINT [PK_PromotionRedemptions] PRIMARY KEY ([Id]),
        CONSTRAINT [CK_PromotionRedemptions_Amount] CHECK ([DiscountAmount] >= 0),
        CONSTRAINT [FK_PromotionRedemptions_Payments_PaymentId] FOREIGN KEY ([PaymentId]) REFERENCES [Payments] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_PromotionRedemptions_Promotions_PromotionId] FOREIGN KEY ([PromotionId]) REFERENCES [Promotions] ([Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261002083852_AddDocumentCoverage'
)
BEGIN
    IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'Id', N'City', N'CreatedAtUtc', N'Date', N'Name', N'UpdatedAtUtc') AND [object_id] = OBJECT_ID(N'[Holidays]'))
        SET IDENTITY_INSERT [Holidays] ON;
    EXEC(N'INSERT INTO [Holidays] ([Id], [City], [CreatedAtUtc], [Date], [Name], [UpdatedAtUtc])
    VALUES (1, NULL, ''2026-10-01T00:00:00.0000000Z'', ''2027-01-01'', N''Tết Dương lịch'', NULL),
    (2, NULL, ''2026-10-01T00:00:00.0000000Z'', ''2027-02-05'', N''Tết Nguyên đán (29 Tết)'', NULL),
    (3, NULL, ''2026-10-01T00:00:00.0000000Z'', ''2027-02-06'', N''Tết Nguyên đán (Mùng 1)'', NULL),
    (4, NULL, ''2026-10-01T00:00:00.0000000Z'', ''2027-02-07'', N''Tết Nguyên đán (Mùng 2)'', NULL),
    (5, NULL, ''2026-10-01T00:00:00.0000000Z'', ''2027-02-08'', N''Tết Nguyên đán (Mùng 3)'', NULL),
    (6, NULL, ''2026-10-01T00:00:00.0000000Z'', ''2027-02-09'', N''Tết Nguyên đán (Mùng 4)'', NULL),
    (7, NULL, ''2026-10-01T00:00:00.0000000Z'', ''2027-04-16'', N''Giỗ Tổ Hùng Vương'', NULL),
    (8, NULL, ''2026-10-01T00:00:00.0000000Z'', ''2027-04-30'', N''Ngày Giải phóng miền Nam'', NULL),
    (9, NULL, ''2026-10-01T00:00:00.0000000Z'', ''2027-05-01'', N''Quốc tế Lao động'', NULL),
    (10, NULL, ''2026-10-01T00:00:00.0000000Z'', ''2027-09-02'', N''Quốc khánh'', NULL),
    (11, NULL, ''2026-10-01T00:00:00.0000000Z'', ''2027-09-03'', N''Quốc khánh (nghỉ liền kề)'', NULL)');
    IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'Id', N'City', N'CreatedAtUtc', N'Date', N'Name', N'UpdatedAtUtc') AND [object_id] = OBJECT_ID(N'[Holidays]'))
        SET IDENTITY_INSERT [Holidays] OFF;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261002083852_AddDocumentCoverage'
)
BEGIN
    EXEC(N'CREATE UNIQUE INDEX [IX_Invoices_PaymentId] ON [Invoices] ([PaymentId]) WHERE [PaymentId] IS NOT NULL');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261002083852_AddDocumentCoverage'
)
BEGIN
    EXEC(N'CREATE UNIQUE INDEX [IX_Invoices_SettlementId] ON [Invoices] ([SettlementId]) WHERE [SettlementId] IS NOT NULL');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261002083852_AddDocumentCoverage'
)
BEGIN
    EXEC(N'ALTER TABLE [Invoices] ADD CONSTRAINT [CK_Invoices_Source] CHECK (([Type] = N''ParkingFee'' AND [PaymentId] IS NOT NULL) OR ([Type] = N''PlatformCommission'' AND [SettlementId] IS NOT NULL))');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261002083852_AddDocumentCoverage'
)
BEGIN
    EXEC(N'CREATE INDEX [IX_CompensationVouchers_ChargedToOwnerProfileId] ON [CompensationVouchers] ([ChargedToOwnerProfileId]) WHERE [ChargedToOwnerProfileId] IS NOT NULL');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261002083852_AddDocumentCoverage'
)
BEGIN
    CREATE UNIQUE INDEX [IX_CompensationVouchers_Code] ON [CompensationVouchers] ([Code]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261002083852_AddDocumentCoverage'
)
BEGIN
    CREATE INDEX [IX_CompensationVouchers_RedeemedPaymentId] ON [CompensationVouchers] ([RedeemedPaymentId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261002083852_AddDocumentCoverage'
)
BEGIN
    CREATE INDEX [IX_CompensationVouchers_UserId_Status_ExpiresAtUtc] ON [CompensationVouchers] ([UserId], [Status], [ExpiresAtUtc]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261002083852_AddDocumentCoverage'
)
BEGIN
    EXEC(N'CREATE UNIQUE INDEX [IX_Holidays_Date_City] ON [Holidays] ([Date], [City]) WHERE [City] IS NOT NULL');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261002083852_AddDocumentCoverage'
)
BEGIN
    CREATE INDEX [IX_PaymentCallbackLogs_PaymentId_CreatedAtUtc] ON [PaymentCallbackLogs] ([PaymentId], [CreatedAtUtc]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261002083852_AddDocumentCoverage'
)
BEGIN
    CREATE INDEX [IX_PaymentCallbackLogs_Provider_ProviderTransactionId] ON [PaymentCallbackLogs] ([Provider], [ProviderTransactionId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261002083852_AddDocumentCoverage'
)
BEGIN
    EXEC(N'CREATE UNIQUE INDEX [IX_PromotionRedemptions_BookingId] ON [PromotionRedemptions] ([BookingId]) WHERE [IsReverted] = 0');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261002083852_AddDocumentCoverage'
)
BEGIN
    CREATE INDEX [IX_PromotionRedemptions_PaymentId] ON [PromotionRedemptions] ([PaymentId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261002083852_AddDocumentCoverage'
)
BEGIN
    CREATE INDEX [IX_PromotionRedemptions_PromotionId_UserId] ON [PromotionRedemptions] ([PromotionId], [UserId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261002083852_AddDocumentCoverage'
)
BEGIN
    ALTER TABLE [Invoices] ADD CONSTRAINT [FK_Invoices_Settlements_SettlementId] FOREIGN KEY ([SettlementId]) REFERENCES [Settlements] ([Id]) ON DELETE NO ACTION;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261002083852_AddDocumentCoverage'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20261002083852_AddDocumentCoverage', N'10.0.12');
END;

COMMIT;
GO

