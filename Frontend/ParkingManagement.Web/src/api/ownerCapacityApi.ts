import { apiClient } from './client';
import type {
  LotCapacityConfigDto,
  UpdateLotCapacityConfigRequest,
} from '@/types/ownerCapacity';

/**
 * Service API quản lý Cấu hình sức chứa & vùng đệm khách vãng lai (UI-26, EP10).
 * Backend: ParkingService / LotCapacityController (Route: /api/v1/parking-lots/{lotId}/capacity-config)
 */
export const ownerCapacityApi = {
  /**
   * Lấy cấu hình sức chứa, hạn ngạch online và vùng đệm vãng lai của bãi xe.
   * Backend tự động auto-provisioning cấu hình mặc định an toàn nếu bãi mới chưa có.
   */
  getCapacityConfig: async (
    lotId: number,
    ownerProfileId?: number
  ): Promise<LotCapacityConfigDto> => {
    const response = await apiClient.get<LotCapacityConfigDto>(
      `/api/v1/parking-lots/${lotId}/capacity-config`,
      {
        params: ownerProfileId ? { ownerProfileId } : undefined,
      }
    );
    return response.data;
  },

  /**
   * Cập nhật cấu hình sức chứa & tỷ lệ vùng đệm an toàn cho khách vãng lai.
   */
  updateCapacityConfig: async (
    lotId: number,
    request: UpdateLotCapacityConfigRequest
  ): Promise<LotCapacityConfigDto> => {
    const response = await apiClient.put<LotCapacityConfigDto>(
      `/api/v1/parking-lots/${lotId}/capacity-config`,
      request
    );
    return response.data;
  },
};
