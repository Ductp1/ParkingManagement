/**
 * US-071: ngày lễ của PaymentService (HolidaysController) – áp hệ số HolidayMultiplier của RateCard khi báo giá.
 * Khớp HolidayDto / CreateHolidayCommand (JSON camelCase, DateOnly = "YYYY-MM-DD").
 */
export interface HolidayDto {
  id: number;
  /** Ngày lễ "YYYY-MM-DD" (DateOnly, không có giờ / múi giờ). */
  date: string;
  name: string;
  /** null = toàn quốc. */
  city: string | null;
}

/**
 * Body POST /api/v1/holidays. Admin portal chỉ tạo ngày lễ toàn quốc (không gửi city):
 * pricing hiện bỏ qua City (audit §7.1) nên ô City chưa được mở.
 */
export interface CreateHolidayRequest {
  date: string;
  name: string;
}
