import type { IntegrationTier, ParkingLotStatus } from './adminEnums';

/**
 * US-081: một bãi (chưa xóa) trong ParkingService GET /api/v1/parking-lots/admin/lots-summary (RequireAdmin).
 * Không có chủ bãi, địa chỉ, KYB hay heartbeat – màn danh sách không hiển thị các cột đó.
 */
export interface AdminLotSummaryRow {
  parkingLotId: number;
  name: string;
  city: string;
  district: string | null;
  status: ParkingLotStatus | string;
  integrationTier: IntegrationTier | string;
  totalSlots: number;
  isEmergencyStopped: boolean;
  instantBookingPaused: boolean;
}

export interface AdminLotsSummaryDto {
  asOfUtc: string;
  /** Sắp theo tên. */
  lots: AdminLotSummaryRow[];
}

/**
 * US-081: xe đang trong một bãi – GateService GET /api/v1/gate/admin/occupancy (RequireAdmin).
 * Chỉ có bãi đang có xe hoặc đã cấu hình sức chứa; bãi không có trong danh sách = 0 xe.
 * capacity = null khi bãi chưa cấu hình sức chứa ở GateService.
 */
export interface AdminLotOccupancyRow {
  parkingLotId: number;
  activeSessions: number;
  walkIn: number;
  booked: number;
  capacity: number | null;
}

export interface AdminGateOccupancyDto {
  asOfUtc: string;
  total: number;
  walkIn: number;
  booked: number;
  abnormal: number;
  lots: AdminLotOccupancyRow[];
}
