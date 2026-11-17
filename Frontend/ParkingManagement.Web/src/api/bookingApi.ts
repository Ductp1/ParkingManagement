import { apiClient } from './client';
import type { BookingDetailDto, CreateBookingRequest, PriceQuoteDto } from '@/types/booking';

/**
 * API đặt chỗ — route qua Gateway :5000 → BookingService :5104
 * (Gateway route "bookings": /api/v1/bookings/{**rest}).
 */
export const bookingApi = {
  /**
   * US-022: tạo booking + giữ chỗ miễn phí 15 phút (POST /api/v1/bookings).
   * Backend tự chặn: bãi emergency stop (US-069), lạm dụng đặt/hủy (US-103),
   * start < now+15p, start > now+30 ngày. Trả BookingDetailDto có mã vé + QrToken.
   */
  createBooking: async (request: CreateBookingRequest): Promise<BookingDetailDto> => {
    const response = await apiClient.post<BookingDetailDto>('/api/v1/bookings', request);
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