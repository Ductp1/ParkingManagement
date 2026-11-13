import { apiClient } from './client';

export interface RateRuleDto {
  id?: number;
  vehicleType: string;
  fromMinute: number;
  toMinute?: number | null;
  blockMinutes: number;
  pricePerBlock: number;
  sortOrder: number;
}

export interface CreateRateRuleRequest {
  vehicleType: number | string; // Enum 1: Car, 2: Motorbike, etc.
  fromMinute: number;
  toMinute?: number | null;
  blockMinutes: number;
  pricePerBlock: number;
  sortOrder: number;
}

export interface RateCardDetailDto {
  id: number;
  parkingLotId: number;
  ownerProfileId: number;
  name: string;
  effectiveFromUtc: string;
  effectiveToUtc?: string | null;
  isActive: boolean;
  overnightSurcharge: number;
  weekendMultiplier: number;
  holidayMultiplier: number;
  oversizedMultiplier: number;
  overstayMultiplier: number;
  maxDailyAmount?: number | null;
  rules: RateRuleDto[];
}

export interface CreateRateCardCommand {
  parkingLotId: number;
  ownerProfileId: number;
  name: string;
  effectiveFromUtc: string;
  effectiveToUtc?: string | null;
  overnightSurcharge: number;
  weekendMultiplier: number;
  holidayMultiplier: number;
  oversizedMultiplier: number;
  overstayMultiplier: number;
  maxDailyAmount?: number | null;
  rules: CreateRateRuleRequest[];
}

export interface UpdateRateCardCommand {
  id: number;
  name: string;
  overnightSurcharge: number;
  weekendMultiplier: number;
  holidayMultiplier: number;
  oversizedMultiplier: number;
  overstayMultiplier: number;
  maxDailyAmount?: number | null;
  isActive: boolean;
}

/**
 * Service API quản lý biểu phí, bảng giá lũy tiến và phụ thu (UI-22 / US-070, US-071, US-072).
 * Backend: PaymentService / RateCardsController (/api/v1/rate-cards)
 */
export const ownerPricingApi = {
  /**
   * Lấy toàn bộ danh sách bảng giá và lịch sử đổi giá của một bãi xe (US-070).
   */
  getRateCardsByLot: async (parkingLotId: number): Promise<RateCardDetailDto[]> => {
    const response = await apiClient.get<RateCardDetailDto[]>('/api/v1/rate-cards', {
      params: { parkingLotId },
    });
    return response.data;
  },

  /**
   * Lấy chi tiết một bảng giá theo ID.
   */
  getRateCardById: async (id: number): Promise<RateCardDetailDto> => {
    const response = await apiClient.get<RateCardDetailDto>(`/api/v1/rate-cards/${id}`);
    return response.data;
  },

  /**
   * Tạo bảng giá lũy tiến mới kèm các mức phụ thu (US-070, US-071).
   */
  createRateCard: async (command: CreateRateCardCommand): Promise<RateCardDetailDto> => {
    const response = await apiClient.post<RateCardDetailDto>('/api/v1/rate-cards', command);
    return response.data;
  },

  /**
   * Cập nhật thông số phụ thu hoặc bật/tắt bảng giá (US-071, US-072).
   */
  updateRateCard: async (
    id: number,
    command: UpdateRateCardCommand
  ): Promise<RateCardDetailDto> => {
    const response = await apiClient.put<RateCardDetailDto>(`/api/v1/rate-cards/${id}`, command);
    return response.data;
  },
};
