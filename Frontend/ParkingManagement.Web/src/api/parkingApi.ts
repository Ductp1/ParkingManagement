import { apiClient } from './client';
import type { ParkingLotDto, ParkingLotSearchItemDto, FloorLayoutDto, ParkingLotHierarchyDto, LotOperationalStatusDto } from '@/types/parking';

export const parkingApi = {
  /**
   * Xem chi tiết bãi đỗ (US-018)
   */
  getById: async (id: number): Promise<ParkingLotDto> => {
    const response = await apiClient.get<ParkingLotDto>(`/api/v1/parking-lots/${id}`);
    return response.data;
  },

  /**
   * US-068: độ tươi dữ liệu của bãi (heartbeat & stale) – để hiển thị cảnh báo
   * "số chỗ trống có thể không chính xác" khi bãi quá hạn heartbeat.
   */
  getOperationalStatus: async (lotId: number): Promise<LotOperationalStatusDto> => {
    const response = await apiClient.get<LotOperationalStatusDto>(
      `/api/v1/parking-lots/${lotId}/operations/status`
    );
    return response.data;
  },

  /**
   * Tìm kiếm bãi đỗ gần nhất (US-014, US-015, US-016)
   */
  search: async (params: {
    lat: number;
    lng: number;
    radiusKm?: number;
    vehicleHeightCm?: number;
    limit?: number;
    amenities?: string;
    openNowOnly?: boolean;
    hasAvailableSlotsOnly?: boolean;
    sortBy?: string;
  }): Promise<ParkingLotSearchItemDto[]> => {
    const response = await apiClient.get<ParkingLotSearchItemDto[]>('/api/v1/parking-lots/search', {
      params,
    });
    return response.data;
  },

  /**
   * Lấy cây cấu trúc toàn bộ bãi đỗ: Lot -> Zone -> Floor -> Slot (Hợp đồng API số 4)
   */
  getLotHierarchy: async (lotId: number): Promise<ParkingLotHierarchyDto> => {
    const response = await apiClient.get<ParkingLotHierarchyDto>(`/api/v1/parking-lots/${lotId}/hierarchy`);
    return response.data;
  },

  /**
   * Lấy sơ đồ ma trận 2D của tầng (T-502, US-020, US-059)
   */
  getFloorLayout: async (floorId: number): Promise<FloorLayoutDto> => {
    const response = await apiClient.get<FloorLayoutDto>(`/api/v1/parking-lots/floors/${floorId}/layout`);
    return response.data;
  },

  /**
   * Lấy danh sách khu vực trong bãi (US-058)
   */
  getLotZones: async (lotId: number): Promise<Array<{ id: number; code: string; name: string; isOutdoor: boolean }>> => {
    const response = await apiClient.get(`/api/v1/parking-lots/${lotId}/zones`);
    return response.data;
  },

  /**
   * Lấy danh sách tầng trong khu vực (US-058)
   */
  getZoneFloors: async (zoneId: number): Promise<Array<{ id: number; name: string; level: number; maxHeightCm: number; gridColumns: number; gridRows: number }>> => {
    const response = await apiClient.get(`/api/v1/parking-lots/zones/${zoneId}/floors`);
    return response.data;
  },
};
