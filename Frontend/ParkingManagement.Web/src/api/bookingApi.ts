import { apiClient } from './client';
import type {
  BookingDetailDto,
  BookingSummaryDto,
  CancelBookingRequest,
  CancellationPreviewDto,
  CancellationResultDto,
  CreateBookingRequest,
  ExtendBookingRequest,
  ModifyBookingRequest,
  PriceQuoteDto,
} from '@/types/booking';

/**
 * API đặt chỗ — route qua Gateway :5000 → BookingService :5104
 * (Gateway route "bookings": /api/v1/bookings/{**rest}).
 */
export const bookingApi = {
  /**
   * UC-13: Danh sách booking của tài xế (GET /api/v1/bookings?userId=...&status=...).
   */
  listMyBookings: async (userId: number, status?: string): Promise<BookingSummaryDto[]> => {
    const response = await apiClient.get<BookingSummaryDto[]>('/api/v1/bookings', {
      params: status ? { userId, status } : { userId },
    });
    return response.data;
  },

  /**
   * Chi tiết booking kèm QR token và lịch sử trạng thái (GET /api/v1/bookings/{code}).
   */
  getByCode: async (code: string): Promise<BookingDetailDto> => {
    const response = await apiClient.get<BookingDetailDto>(
      `/api/v1/bookings/${encodeURIComponent(code)}`
    );
    return response.data;
  },

  /**
   * UC-16: Xem trước chính sách hủy & số tiền hoàn tại thời điểm hiện tại
   * (GET /api/v1/bookings/{code}/cancellation-preview).
   */
  previewCancellation: async (code: string): Promise<CancellationPreviewDto> => {
    const response = await apiClient.get<CancellationPreviewDto>(
      `/api/v1/bookings/${encodeURIComponent(code)}/cancellation-preview`
    );
    return response.data;
  },

  /**
   * US-022: tạo booking + giữ chỗ miễn phí 15 phút (POST /api/v1/bookings).
   */
  createBooking: async (request: CreateBookingRequest): Promise<BookingDetailDto> => {
    const response = await apiClient.post<BookingDetailDto>('/api/v1/bookings', request);
    return response.data;
  },

  /**
   * US-038: Hủy booking theo Cancellation Window 60 phút (POST /api/v1/bookings/{code}/cancel).
   */
  cancelBooking: async (
    code: string,
    request: CancelBookingRequest
  ): Promise<CancellationResultDto> => {
    const response = await apiClient.post<CancellationResultDto>(
      `/api/v1/bookings/${encodeURIComponent(code)}/cancel`,
      request
    );
    return response.data;
  },

  /**
   * US-029: Sửa khung giờ hoặc xe trước khi check-in (PUT /api/v1/bookings/{code}).
   */
  modifyBooking: async (
    code: string,
    request: ModifyBookingRequest
  ): Promise<BookingDetailDto> => {
    const response = await apiClient.put<BookingDetailDto>(
      `/api/v1/bookings/${encodeURIComponent(code)}`,
      request
    );
    return response.data;
  },

  /**
   * US-030: Gia hạn thời gian đỗ xe (POST /api/v1/bookings/{code}/extend).
   */
  extendBooking: async (
    code: string,
    request: ExtendBookingRequest
  ): Promise<BookingDetailDto> => {
    const response = await apiClient.post<BookingDetailDto>(
      `/api/v1/bookings/${encodeURIComponent(code)}/extend`,
      request
    );
    return response.data;
  },

  /**
   * US-070/071: báo giá thật từ bảng giá của bãi
   * (GET /api/v1/pricing/quote qua Gateway → PaymentService).
   */
  getQuote: async (
    parkingLotId: number,
    vehicleType: string,
    startAtUtc: string,
    endAtUtc: string
  ): Promise<PriceQuoteDto> => {
    const response = await apiClient.get<PriceQuoteDto>('/api/v1/pricing/quote', {
      params: { parkingLotId, vehicleType, startAtUtc, endAtUtc },
    });
    return response.data;
  },
};
