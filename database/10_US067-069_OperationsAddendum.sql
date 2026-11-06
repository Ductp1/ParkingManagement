-- =====================================================================
-- US-068 (phần heartbeat/stale của addendum Operations) — pm_parking
-- Nguồn chân lý của schema là EF model. Script dành cho database vận hành
-- đã tạo từ baseline SQL (v1) — chạy 1 lần, idempotent (IF NOT EXISTS).
-- PostgreSQL dialect khớp runtime (Npgsql).
-- US-069 sẽ bổ sung tiếp các cột/bảng emergency stop vào file này.
-- =====================================================================

-- ===================== pm_parking (ParkingService) =====================

-- US-068: heartbeat & stale data trên ParkingLots
ALTER TABLE "ParkingLots" ADD COLUMN IF NOT EXISTS "LastHeartbeatAtUtc" timestamptz NULL;
ALTER TABLE "ParkingLots" ADD COLUMN IF NOT EXISTS "IsDataStale" boolean NOT NULL DEFAULT false;

-- =====================================================================
-- ROLLBACK (Down) — MẤY DỮ LIỆU CỘT PHÁI SINH (an toàn: heartbeat/stale có
-- thể tái tạo từ thiết bị), KHÔNG đụng dữ liệu nghiệp vụ gốc của ParkingLots.
-- =====================================================================
-- ALTER TABLE "ParkingLots" DROP COLUMN IF EXISTS "IsDataStale";
-- ALTER TABLE "ParkingLots" DROP COLUMN IF EXISTS "LastHeartbeatAtUtc";
