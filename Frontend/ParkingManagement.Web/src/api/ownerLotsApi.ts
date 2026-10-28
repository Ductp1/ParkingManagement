import { apiClient } from './client';
import type {
  OwnerParkingLotItemDto,
  CreateParkingLotRequest,
  OwnerLotDetailDto,
} from '@/types/ownerKyb';

/**
 * Service API quản lý bãi đỗ xe thuộc phân hệ Chủ bãi (Owner Portal - US-053).
 * Backend: ParkingService / ParkingLotsController (Route: /api/v1/parking-lots)
 */
export const ownerLotsApi = {
  /**
   * Lấy danh sách tất cả các bãi đỗ thuộc quyền sở hữu của chủ bãi.
   */
  getMyLots: async (ownerProfileId: number): Promise<OwnerParkingLotItemDto[]> => {
    const response = await apiClient.get<OwnerParkingLotItemDto[]>(
      `/api/v1/parking-lots/owner/${ownerProfileId}`
    );
    return response.data;
  },

  /**
   * Đăng ký bãi đỗ xe mới trên hệ thống (US-053).
   * Kiểm tra trùng tên bãi trong cùng tài khoản chủ bãi.
   */
  createParkingLot: async (
    data: CreateParkingLotRequest
  ): Promise<{ id: number }> => {
    const response = await apiClient.post<{ id: number }>(
      '/api/v1/parking-lots',
      data
    );
    return response.data;
  },

  /**
   * Xem chi tiết thông tin bãi đỗ xe theo ID.
   */
  getLotById: async (id: number): Promise<OwnerLotDetailDto> => {
    const response = await apiClient.get<OwnerLotDetailDto>(
      `/api/v1/parking-lots/${id}`
    );
    return response.data;
  },
};
