/** Mode phân bổ chỗ đỗ — khớp AllocationMode của backend. */
export type AllocationMode = 'Dynamic' | 'Fixed';

/** Trạng thái booking từ BookingService (BookingStatus enum). */
export type BackendBookingStatus =
  | 'PendingPayment'
  | 'PendingOwnerApproval'
  | 'Confirmed'
  | 'CheckedIn'
  | 'Completed'
  | 'Cancelled'
  | 'CancelledNoRefund'
  | 'Expired'
  | 'NoShow'
  | 'Rejected';

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
  payAtGate?: boolean;
}

/** Khớp BookingSummaryDto từ GET /api/v1/bookings?userId=... */
export interface BookingSummaryDto {
  id: number;
  code: string;
  status: BackendBookingStatus | string;
  parkingLotName: string;
  slotCode?: string | null;
  plateNumber: string;
  startAtUtc: string;
  endAtUtc: string;
  totalAmount: number;
}

export interface StatusLogDto {
  fromStatus?: string | null;
  toStatus: string;
  changedByUserId?: number | null;
  reason?: string | null;
  atUtc: string;
}

/**
 * BookingDetailDto — khớp GET /api/v1/bookings/{code} và POST /api/v1/bookings.
 */
export interface BookingDetailDto {
  id: number;
  code: string;
  status: BackendBookingStatus | string;
  userId?: number;
  vehicleId?: number;
  plateNumber?: string;
  vehicleType?: string;
  parkingLotId: number;
  parkingLotName: string;
  zoneId?: number | null;
  slotId?: number | null;
  slotCode?: string | null;
  startAtUtc: string;
  endAtUtc: string;
  holdExpiresAtUtc?: string | null;
  totalAmount: number;
  discountAmount?: number;
  paidAmount?: number;
  promotionCode?: string | null;
  history?: StatusLogDto[];
  qrToken?: string | null;
}

/** Khớp CancellationPreviewDto từ GET /api/v1/bookings/{code}/cancellation-preview */
export interface CancellationPreviewDto {
  code: string;
  canCancel: boolean;
  resultStatus: string;
  refundPercent: number;
  refundAmount: number;
  reason: string;
}

/** Khớp CancellationResultDto từ POST /api/v1/bookings/{code}/cancel */
export interface CancellationResultDto {
  code: string;
  status: string;
  refundPercent: number;
  refundAmount: number;
  reason: string;
}

export interface CancelBookingRequest {
  userId: number;
  reason?: string | null;
}

export interface ModifyBookingRequest {
  userId: number;
  newVehicleId?: number | null;
  newPlateNumber?: string | null;
  newStartAtUtc?: string | null;
  newEndAtUtc?: string | null;
}

export interface ExtendBookingRequest {
  userId: number;
  newEndAtUtc: string;
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
