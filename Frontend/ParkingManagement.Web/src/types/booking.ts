
/** Mode phân bổ chỗ đỗ — khớp AllocationMode của backend. */
export type AllocationMode = 'Dynamic' | 'Fixed';

/**
 * US-022/US-069: request tạo booking — khớp CreateBookingCommand của BookingsController
 * (POST /api/v1/bookings qua Gateway :5000 → BookingService :5104).
 */
export interface CreateBookingRequest {
  userId: number;
  vehicleId: number;
  plateNumber: string;
  vehicleType: string;
  parkingLotId: number;
  ownerProfileId: number;
  parkingLotName: string;
  zoneId?: number | null;
  slotId?: number | null;
  slotCode?: string | null;
  allocationMode: string;
  startAtUtc: string; // ISO UTC
  endAtUtc: string; // ISO UTC
  totalAmount: number;
  promotionCode?: string | null;
  requiresOwnerApproval?: boolean;
}

/**
 * BookingDetailDto rút gọn các field FE dùng sau khi tạo booking:
 * mã vé (code), hạn giữ chỗ 15 phút, QR token và trạng thái.
 */
export interface BookingDetailDto {
  id: number;
  code: string;
  status: string;
  parkingLotId: number;
  parkingLotName: string;
  slotCode?: string | null;
  startAtUtc: string;
  endAtUtc: string;
  holdExpiresAtUtc?: string | null;
  totalAmount: number;
  qrToken?: string | null;
}

/** US-070/071: kết quả báo giá từ PricingController GET /api/v1/pricing/quote. */
export interface PriceQuoteDto {
  parkingLotId: number;
  rateCardId: number;
  vehicleType: string;
  startAtUtc: string;
  endAtUtc: string;
  durationMinutes: number;
  baseAmount: number;
  multiplier: number;
  finalAmount: number;
  isWithinGracePeriod: boolean;
}
