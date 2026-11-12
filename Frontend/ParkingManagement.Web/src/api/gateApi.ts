import { apiClient } from './client';

// ===== DTO khớp GateService / BookingService / PaymentService API =====

export interface ParkingSessionDto {
  id: number;
  code: string;
  parkingLotId: number;
  plateNumber: string;
  plateDisplay: string;
  isWalkIn: boolean;
  bookingCode: string | null;
  slotCode: string | null;
  status: string;
  entryAtUtc: string;
  exitAtUtc: string | null;
  minutesParked: number;
  checkInMethod: string;
  fee: number | null;
  vehicleType: string;
}

/** UC-20 (US-076): 1 dòng booking trong ca trực – không có SĐT/CCCD của khách. */
export interface ShiftBookingItemDto {
  code: string;
  bookingCode: string | null;
  plateDisplay: string;
  slotCode: string | null;
  startAtUtc: string;
  endAtUtc: string | null;
  status: string;
  isOverdue: boolean;
  source: 'Booking' | 'WalkIn';
  totalAmount: number | null;
}

export interface FeeLineDto {
  description: string;
  minutes: number;
  amount: number;
}

/** UC-22 (US-045→047): báo giá chốt phí khi xe ra cổng. */
export interface ExitFeeQuoteDto {
  sessionCode: string;
  plateDisplay: string;
  bookingCode: string | null;
  bookingStatus: string | null;
  entryAtUtc: string;
  minutesParked: number;
  sessionFee: number;
  prepaidAmount: number;
  additionalDue: number;
  isWithinBillingGrace: boolean;
  overstayMinutes: number;
  overstayAmount: number;
  lines: FeeLineDto[];
}

export interface CheckOutResultDto {
  session: ParkingSessionDto;
  fee: ExitFeeQuoteDto | null;
  warning: string | null;
}

export interface CheckInCommandDto {
  parkingLotId: number;
  method: 'Qr' | 'BookingCode' | 'Manual';
  plate: string;
  bookingCode?: string;
  qrToken?: string;
  staffUserId?: number;
}

export interface CheckOutCommandDto {
  parkingLotId: number;
  plate: string;
  method?: 'Qr' | 'Manual';
  staffUserId?: number;
  confirmFee?: boolean;
}

export const gateApi = {
  /** UC-21: check-in tại cổng vào (QR / mã booking / nhập tay). */
  checkIn: async (command: CheckInCommandDto): Promise<ParkingSessionDto> => {
    const response = await apiClient.post<ParkingSessionDto>('/api/v1/gate/check-in', command);
    return response.data;
  },

  /** UC-22 bước 1–3: tính tổng tiền + phụ thu quá giờ để staff xác nhận trước khi mở barie. */
  exitQuote: async (parkingLotId: number, plate: string): Promise<ExitFeeQuoteDto> => {
    const response = await apiClient.get<ExitFeeQuoteDto>('/api/v1/gate/exit-quote', {
      params: { parkingLotId, plate },
    });
    return response.data;
  },

  /** UC-22: chốt phí + mở barie (confirmFee=true chạy đủ luồng thu phí). */
  checkOut: async (command: CheckOutCommandDto): Promise<CheckOutResultDto> => {
    const response = await apiClient.post<CheckOutResultDto>('/api/v1/gate/check-out', command);
    return response.data;
  },

  /** UC-20 (US-076): booking trong ca trực + khách vãng lai đang đỗ, tìm theo mã booking/biển số. */
  shiftBookings: async (params: {
    parkingLotId?: number;
    staffUserId?: number;
    fromUtc?: string;
    toUtc?: string;
    search?: string;
  }): Promise<ShiftBookingItemDto[]> => {
    const response = await apiClient.get<ShiftBookingItemDto[]>('/api/v1/gate/console/bookings', { params });
    return response.data;
  },

  /** Xe đang trong bãi (lượt Active) – nguồn dữ liệu feed cổng. */
  activeSessions: async (parkingLotId: number): Promise<ParkingSessionDto[]> => {
    const response = await apiClient.get<ParkingSessionDto[]>('/api/v1/parking-sessions', {
      params: { parkingLotId, status: 'Active' },
    });
    return response.data;
  },
};
