import { apiClient } from './client';
import type {
  ZoneDto,
  FloorDto,
  SlotDto,
  CreateZoneRequest,
  UpdateZoneRequest,
  CreateFloorRequest,
  UpdateFloorRequest,
  CreateSlotRequest,
  UpdateSlotRequest,
  BatchCreateSlotsRequest,
  ParkingLotHierarchyDto,
} from '@/types/ownerStructure';

/**
 * Service API quản lý Cấu trúc phân tầng bãi xe: Zone -> Floor -> Slot (US-058 & US-063).
 * Backend: ParkingService / ParkingLotStructureController & ParkingLotsController.
 */
export const ownerStructureApi = {
  // ==================== ZONES ====================

  /**
   * Lấy danh sách các Khu vực (Zones) trong bãi đỗ.
   */
  getZones: async (lotId: number): Promise<ZoneDto[]> => {
    const response = await apiClient.get<ZoneDto[]>(
      `/api/v1/parking-lots/${lotId}/zones`
    );
    return response.data;
  },

  /**
   * Tạo mới Khu vực (Zone).
   */
  createZone: async (
    lotId: number,
    data: CreateZoneRequest
  ): Promise<ZoneDto> => {
    const response = await apiClient.post<ZoneDto>(
      `/api/v1/parking-lots/${lotId}/zones`,
      data
    );
    return response.data;
  },

  /**
   * Cập nhật thông tin Khu vực (Zone), bao gồm trạng thái tạm đóng (isClosed) và lý do (closedReason).
   */
  updateZone: async (
    id: number,
    data: UpdateZoneRequest
  ): Promise<ZoneDto> => {
    const response = await apiClient.put<ZoneDto>(
      `/api/v1/parking-lots/zones/${id}`,
      data
    );
    return response.data;
  },

  /**
   * Xóa Khu vực (Zone). Chặn xóa nếu còn tầng con (409 Conflict).
   */
  deleteZone: async (id: number): Promise<void> => {
    await apiClient.delete(`/api/v1/parking-lots/zones/${id}`);
  },

  // ==================== FLOORS ====================

  /**
   * Lấy danh sách Tầng (Floors) thuộc Khu vực.
   */
  getFloors: async (zoneId: number): Promise<FloorDto[]> => {
    const response = await apiClient.get<FloorDto[]>(
      `/api/v1/parking-lots/zones/${zoneId}/floors`
    );
    return response.data;
  },

  /**
   * Tạo mới Tầng (Floor) trong Khu vực.
   */
  createFloor: async (
    zoneId: number,
    data: CreateFloorRequest
  ): Promise<FloorDto> => {
    const response = await apiClient.post<FloorDto>(
      `/api/v1/parking-lots/zones/${zoneId}/floors`,
      data
    );
    return response.data;
  },

  /**
   * Cập nhật thông số Tầng (Floor), bao gồm trạng thái tạm đóng tầng (isClosed).
   */
  updateFloor: async (
    id: number,
    data: UpdateFloorRequest
  ): Promise<FloorDto> => {
    const response = await apiClient.put<FloorDto>(
      `/api/v1/parking-lots/floors/${id}`,
      data
    );
    return response.data;
  },

  /**
   * Xóa Tầng (Floor). Chặn xóa nếu còn ô đỗ (409 Conflict).
   */
  deleteFloor: async (id: number): Promise<void> => {
    await apiClient.delete(`/api/v1/parking-lots/floors/${id}`);
  },

  // ==================== SLOTS & HIERARCHY ====================

  /**
   * Lấy toàn bộ cây phân cấp đầy đủ: Lot -> Zones -> Floors -> Slots (Contract #4).
   */
  getHierarchy: async (lotId: number): Promise<ParkingLotHierarchyDto> => {
    const response = await apiClient.get<ParkingLotHierarchyDto>(
      `/api/v1/parking-lots/${lotId}/hierarchy`
    );
    return response.data;
  },

  /**
   * Lấy danh sách ô đỗ của tầng.
   */
  getSlots: async (floorId: number): Promise<SlotDto[]> => {
    const response = await apiClient.get<SlotDto[]>(
      `/api/v1/parking-lots/floors/${floorId}/slots`
    );
    return response.data;
  },

  /**
   * Tạo ô đỗ đơn lẻ trên tầng.
   */
  createSlot: async (
    floorId: number,
    data: CreateSlotRequest
  ): Promise<SlotDto> => {
    const response = await apiClient.post<SlotDto>(
      `/api/v1/parking-lots/floors/${floorId}/slots`,
      data
    );
    return response.data;
  },

  /**
   * Tạo hàng loạt ô đỗ tự động trên tầng.
   */
  batchCreateSlots: async (
    floorId: number,
    data: BatchCreateSlotsRequest
  ): Promise<number> => {
    const response = await apiClient.post<number>(
      `/api/v1/parking-lots/floors/${floorId}/slots/batch`,
      data
    );
    return response.data;
  },

  /**
   * Cập nhật cấu hình ô đỗ, bao gồm bật/tắt kích hoạt hoạt động (isActive - US-063).
   */
  updateSlot: async (
    id: number,
    data: UpdateSlotRequest
  ): Promise<SlotDto> => {
    const response = await apiClient.put<SlotDto>(
      `/api/v1/parking-lots/slots/${id}`,
      data
    );
    return response.data;
  },

  /**
   * Xóa ô đỗ khỏi CSDL. Chặn xóa nếu ô đỗ đang có xe hoặc có booking.
   */
  deleteSlot: async (id: number): Promise<void> => {
    await apiClient.delete(`/api/v1/parking-lots/slots/${id}`);
  },
};
