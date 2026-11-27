import { apiClient } from './client';

export interface GateBookingListItemDto {
  code: string;
  plateNumber: string;
  vehicleType: string;
  slotCode?: string | null;
  startAtUtc: string;
  endAtUtc: string;
  status: string;
  isOverdue: boolean;
  totalAmount: number;
}

export interface StatusLogDto {
  fromStatus?: string | null;
  toStatus: string;
  changedByUserId?: number | null;
  reason?: string | null;
  atUtc: string;
}

export interface PriceSnapshotDto {
  rateCardId: number;
  baseAmount: number;
  surchargeAmount: number;
  discountAmount: number;
  finalAmount: number;
  billingGracePeriodMinutes: number;
  rateCardJson: string;
}

export interface BookingDetailDto {
  id: number;
  code: string;
  status: string;
  userId: number;
  vehicleId: number;
  plateNumber: string;
  vehicleType: string;
  parkingLotId: number;
  parkingLotName: string;
  zoneId?: number | null;
  slotId?: number | null;
  slotCode?: string | null;
  startAtUtc: string;
  endAtUtc: string;
  holdExpiresAtUtc?: string | null;
  totalAmount: number;
  discountAmount: number;
  paidAmount: number;
  promotionCode?: string | null;
  priceSnapshot?: PriceSnapshotDto | null;
  history: StatusLogDto[];
  qrToken?: string | null;
}

export interface ApprovalLogDto {
  id: number;
  bookingId: number;
  bookingCode: string;
  plateNumber: string;
  parkingLotId: number;
  parkingLotName: string;
  result: 'Approved' | 'Rejected' | string;
  reviewedByUserId?: number | null;
  reason?: string | null;
  createdAtUtc: string;
}

export interface ReviewBookingCommand {
  ownerProfileId: number;
  reason?: string | null;
}

export interface LotCancelBookingCommand {
  ownerProfileId: number;
  reason: string;
}

/**
 * Service API quản lý đặt chỗ và duyệt booking cho Chủ bãi (UI-23 / US-031, US-042).
 * Backend: BookingService / BookingsController (/api/v1/bookings)
 */
export const ownerBookingsApi = {
  /**
   * Lấy danh sách booking đang hoạt động / trong ca trực tại bãi đỗ xe (UC-20).
   */
  getGateBookings: async (params: {
    parkingLotId: number;
    fromUtc: string;
    toUtc: string;
    search?: string;
    gracePeriodMinutes?: number;
  }): Promise<GateBookingListItemDto[]> => {
    const response = await apiClient.get<GateBookingListItemDto[]>('/api/v1/bookings/gate', {
      params: {
        parkingLotId: params.parkingLotId,
        fromUtc: params.fromUtc,
        toUtc: params.toUtc,
        search: params.search || undefined,
        gracePeriodMinutes: params.gracePeriodMinutes ?? 15,
      },
    });
    return response.data;
  },

  /**
   * Tra cứu chi tiết booking theo mã (BK-xxxx) bao gồm giá đã khóa và lịch sử trạng thái.
   */
  getBookingByCode: async (code: string): Promise<BookingDetailDto> => {
    const response = await apiClient.get<BookingDetailDto>(`/api/v1/bookings/${encodeURIComponent(code)}`);
    return response.data;
  },

  /**
   * Lấy lịch sử duyệt / từ chối đơn đặt chỗ bãi Mức 0 (T-303).
   */
  getApprovalLogs: async (params: {
    parkingLotId?: number;
    fromUtc?: string;
    toUtc?: string;
    result?: string;
  }): Promise<ApprovalLogDto[]> => {
    const response = await apiClient.get<ApprovalLogDto[]>('/api/v1/bookings/approval-logs', {
      params: {
        parkingLotId: params.parkingLotId || undefined,
        fromUtc: params.fromUtc || undefined,
        toUtc: params.toUtc || undefined,
        result: params.result || undefined,
      },
    });
    return response.data;
  },

  /**
   * Chủ bãi phê duyệt booking tại bãi Mức 0 (US-031).
   */
  approveBooking: async (code: string, command: ReviewBookingCommand): Promise<BookingDetailDto> => {
    const response = await apiClient.post<BookingDetailDto>(
      `/api/v1/bookings/${encodeURIComponent(code)}/approve`,
      command
    );
    return response.data;
  },

  /**
   * Chủ bãi từ chối booking tại bãi Mức 0 (US-031).
   */
  rejectBooking: async (code: string, command: ReviewBookingCommand): Promise<BookingDetailDto> => {
    const response = await apiClient.post<BookingDetailDto>(
      `/api/v1/bookings/${encodeURIComponent(code)}/reject`,
      command
    );
    return response.data;
  },

  /**
   * Chủ bãi chủ động hủy booking do sự cố tại bãi đỗ xe (US-042 - Hoàn 100%).
   */
  lotCancelBooking: async (code: string, command: LotCancelBookingCommand): Promise<BookingDetailDto> => {
    const response = await apiClient.post<BookingDetailDto>(
      `/api/v1/bookings/${encodeURIComponent(code)}/lot-cancel`,
      command
    );
    return response.data;
  },
};
