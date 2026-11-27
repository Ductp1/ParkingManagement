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

/** Biển số đọc từ ảnh (Plate Recognizer). isValidFormat = false → staff cần kiểm tra lại. */
export interface PlateScanResultDto {
  plate: string;
  plateDisplay: string;
  confidence: number;
  isValidFormat: boolean;
}

// ===== UI-32 (US-039 / UC-24): ứng viên + hủy No-show =====

/** US-039 (UC-24): booking Confirmed chưa check-in đã quá mốc ân hạn 30' – Staff xem rồi hủy. */
export interface GateNoShowCandidateDto {
  code: string;
  userId: number;
  plateNumber: string;
  parkingLotName: string;
  slotCode: string | null;
  startAtUtc: string;
  endAtUtc: string | null;
  minutesOverdue: number;
}

export interface CancelNoShowCommandDto {
  parkingLotId: number;
  staffUserId: number;
  reason?: string;
}

/** US-039 (UC-24): kết quả hủy No-show – nhả slot, ghi GateEvents + bộ đếm ca trực. */
export interface NoShowCancelResultDto {
  bookingCode: string;
  bookingStatus: string;
  cancelledAtUtc: string;
  minutesOverdue: number;
  slotCode: string | null;
  slotReleased: boolean;
  shiftFlagged: boolean;
  abnormalAlertRaised: boolean;
  auditNote: string;
}

// ===== UI-33 (US-048 / UC-23): xử lý ngoại lệ thủ công tại cổng =====

export type GateExceptionActionType =
  | 'RecordOnly'
  | 'ManualCheckIn'
  | 'ManualCheckOut'
  | 'ConfirmCashPayment'
  | 'EmergencyBarrierOpen';

export type GateExceptionTypeEnum =
  | 'OcrFailed'
  | 'ManualPlateCorrection'
  | 'PaymentFailed'
  | 'EmergencyBarrierOpen'
  | 'VehicleArrived';

export interface GateExceptionCommandDto {
  parkingLotId: number;
  action: GateExceptionActionType;
  exceptionType: GateExceptionTypeEnum;
  reason: string;
  staffUserId: number;
  plate?: string;
  bookingCode?: string;
  parkingSessionId?: number;
  imagePath?: string;
}

export interface GateExceptionResultDto {
  eventId: number;
  parkingLotId: number;
  action: string;
  exceptionType: string;
  bookingCode: string | null;
  plateDisplay: string | null;
  sessionCode: string | null;
  status: string;
  auditNote: string;
  atUtc: string;
}

/** US-048: dòng audit GateEvents (dùng cho nhật ký ngoại lệ tại cổng). */
export interface GateEventDto {
  id: number;
  parkingLotId: number;
  eventType: string;
  parkingSessionId: number | null;
  plateNumber: string | null;
  plateDisplay: string | null;
  performedByUserId: number | null;
  note: string | null;
  imagePath: string | null;
  shiftId: number | null;
  createdAtUtc: string;
}

// ===== UI-33 (US-049 / UC-25): Staff ghi đè trạng thái slot =====

export interface OverrideSlotStatusRequestDto {
  parkingLotId: number;
  newState: 'Available' | 'Occupied' | 'Maintenance';
  staffUserId: number;
  reason: string;
}

export interface OverrideSlotStatusResultDto {
  slotId: number;
  slotCode: string;
  fromState: string;
  toState: string;
  source: string;
  staffUserId: number;
  changedAtUtc: string;
  auditNote: string;
}

export const gateApi = {
  /** Gửi ảnh camera/ảnh tải lên cho GateService đọc biển số. */
  scanPlate: async (image: Blob, fileName = 'plate.jpg'): Promise<PlateScanResultDto> => {
    const form = new FormData();
    form.append('image', image, fileName);
    // Ghi đè Content-Type mặc định (application/json) – nếu không axios sẽ đổi FormData thành JSON.
    const response = await apiClient.post<PlateScanResultDto>('/api/v1/gate/plate-scan', form, {
      timeout: 25000,
      headers: { 'Content-Type': 'multipart/form-data' },
    });
    return response.data;
  },

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

  /** UI-32 · US-039 (UC-24) bước 1: booking Confirmed chưa check-in quá mốc ân hạn (mặc định 30') để Staff xem và hủy. */
  noShowCandidates: async (parkingLotId: number, graceMinutes?: number): Promise<GateNoShowCandidateDto[]> => {
    const response = await apiClient.get<GateNoShowCandidateDto[]>('/api/v1/gate/no-show/candidates', {
      params: { parkingLotId, graceMinutes: graceMinutes ?? undefined },
    });
    return response.data;
  },

  /** UI-32 · US-039 (UC-24) bước 3–5: hủy booking No-show (trước mốc 30' → 400; đã check-in/sai trạng thái → 409). */
  cancelNoShow: async (bookingCode: string, command: CancelNoShowCommandDto): Promise<NoShowCancelResultDto> => {
    const response = await apiClient.post<NoShowCancelResultDto>(
      `/api/v1/gate/no-show/${encodeURIComponent(bookingCode)}/cancel`,
      command,
    );
    return response.data;
  },

  /** UI-33 · US-048 (UC-23): staff ghi nhận + xử lý ngoại lệ (QR/biển số lỗi, thanh toán lỗi, hỏng barie...). Reason bắt buộc. */
  recordGateException: async (command: GateExceptionCommandDto): Promise<GateExceptionResultDto> => {
    const response = await apiClient.post<GateExceptionResultDto>('/api/v1/gate/exceptions', command);
    return response.data;
  },

  /** UI-33 · US-048: nhật ký audit các thao tác thủ công tại cổng (mới nhất trước). */
  listGateExceptions: async (params: {
    parkingLotId: number;
    exceptionType?: GateExceptionTypeEnum;
    fromUtc?: string;
    toUtc?: string;
    limit?: number;
  }): Promise<GateEventDto[]> => {
    const response = await apiClient.get<GateEventDto[]>('/api/v1/gate/exceptions', { params });
    return response.data;
  },

  /** UI-33 · US-049 (UC-25): staff ghi đè trạng thái slot khi cảm biến hỏng (không cho phép đặt Reserved). */
  overrideSlot: async (slotId: number, request: OverrideSlotStatusRequestDto): Promise<OverrideSlotStatusResultDto> => {
    const response = await apiClient.post<OverrideSlotStatusResultDto>(`/api/v1/gate/slots/${slotId}/override`, request);
    return response.data;
  },
};
