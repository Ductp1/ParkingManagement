import { apiClient } from './client';
import type { ParkingLotDto, ParkingLotSearchItemDto } from '@/types/parking';

export const parkingApi = {
  /**
   * Xem chi tiết bãi đỗ (US-018)
   */
  getById: async (id: number): Promise<ParkingLotDto> => {
    const response = await apiClient.get<ParkingLotDto>(`/api/v1/parking-lots/${id}`);
    return response.data;
  },

  /**
   * Tìm kiếm bãi đỗ gần nhất
   */
  search: async (params: {
    lat: number;
    lng: number;
    radiusKm?: number;
    vehicleHeightCm?: number;
  }): Promise<ParkingLotSearchItemDto[]> => {
    const response = await apiClient.get<ParkingLotSearchItemDto[]>('/api/v1/parking-lots/search', {
      params,
    });
    return response.data;
  },
};
