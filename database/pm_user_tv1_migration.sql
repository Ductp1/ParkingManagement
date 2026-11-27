START TRANSACTION;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261007093748_CompleteTv1Identity') THEN
    ALTER TABLE "OutboxMessages" ADD "NextAttemptAtUtc" timestamp with time zone;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261007093748_CompleteTv1Identity') THEN
    CREATE TABLE "InboxReceipts" (
        "EventId" uuid NOT NULL,
        "ProcessedAtUtc" timestamp with time zone NOT NULL,
        CONSTRAINT "PK_InboxReceipts" PRIMARY KEY ("EventId")
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261007093748_CompleteTv1Identity') THEN
    INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
    VALUES ('20261007093748_CompleteTv1Identity', '10.0.12');
    END IF;
END $EF$;
COMMIT;

START TRANSACTION;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261007113445_StaffTenantClaims') THEN
    INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
    VALUES ('20261007113445_StaffTenantClaims', '10.0.12');
    END IF;
END $EF$;
COMMIT;

