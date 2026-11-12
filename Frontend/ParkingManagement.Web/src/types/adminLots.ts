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

// ===== Lot detail sub-tabs (P0-4) – đọc dữ liệu owner đã cấu hình; admin chỉ xem =====

/** ParkingService GET /api/v1/parking-lots/{lotId}/operating-hours – lịch tuần hiện tại (TimeOnly "HH:mm:ss"). */
export interface LotOperatingHourItemDto {
  dayOfWeek: string;
  dayOfWeekNumber: number;
  dayName: string;
  openTime: string;
  closeTime: string;
  isClosed: boolean;
  isOvernight: boolean;
  is24Hours: boolean;
  isDefault: boolean;
  isOverride: boolean;
  reason: string | null;
}

export interface LotOperatingHoursWeeklyDto {
  parkingLotId: number;
  parkingLotName: string;
  defaultOpenTime: string;
  defaultCloseTime: string;
  schedule: LotOperatingHourItemDto[];
  year: number | null;
  weekOfYear: number | null;
  isCustomWeek: boolean;
  weekStartDate: string | null;
  weekEndDate: string | null;
}

/** ParkingService GET /api/v1/parking-lots/{lotId}/closures – status: Active | Upcoming | Past | Cancelled. */
export interface LotClosureDto {
  id: number;
  parkingLotId: number;
  targetType: string;
  targetId: number;
  targetName: string;
  startsAtUtc: string;
  endsAtUtc: string | null;
  reason: string;
  isEmergency: boolean;
  affectedBookingsNotified: boolean;
  createdByUserId: number;
  cancelledAtUtc: string | null;
  status: string;
  affectedSlotsCount: number;
  createdAtUtc: string;
}

/** ParkingService GET /api/v1/parking-lots/{lotId}/attributes – tiện ích + ảnh trong một lần gọi. */
export interface LotAmenityDto {
  id: number;
  parkingLotId: number;
  code: string;
  name: string;
  note: string | null;
}

export interface LotPhotoDto {
  id: number;
  parkingLotId: number;
  url: string;
  caption: string | null;
  sortOrder: number;
  isCover: boolean;
}

export interface LotAttributesDto {
  parkingLotId: number;
  name: string;
  description: string | null;
  hotlinePhone: string | null;
  maxHeightCm: number;
  coverImageUrl: string | null;
  amenities: LotAmenityDto[];
  photos: LotPhotoDto[];
}

/** PaymentService GET /api/v1/rate-cards?parkingLotId= – bảng giá của bãi (RateCardDetailDto). */
export interface RateRuleDto {
  id: number;
  vehicleType: string;
  fromMinute: number;
  toMinute: number | null;
  blockMinutes: number;
  pricePerBlock: number;
  sortOrder: number;
}

export interface RateCardDto {
  id: number;
  parkingLotId: number;
  ownerProfileId: number;
  name: string;
  effectiveFromUtc: string;
  effectiveToUtc: string | null;
  isActive: boolean;
  overnightSurcharge: number;
  weekendMultiplier: number;
  holidayMultiplier: number;
  oversizedMultiplier: number;
  overstayMultiplier: number;
  maxDailyAmount: number | null;
  rules: RateRuleDto[];
}
