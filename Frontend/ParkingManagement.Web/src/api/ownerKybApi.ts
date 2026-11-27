import { apiClient } from './client';
import type {
  KybApplicationDto,
  SaveKybDraftRequest,
  SubmitKybApplicationRequest,
} from '@/types/ownerKyb';

/**
 * Service API quản lý hồ sơ thẩm định KYB 4 bước của bãi đỗ xe (US-054).
 * Backend: ParkingService / KybApplicationsController (Route: /api/v1/parking-lots/{lotId}/kyb)
 */
export const ownerKybApi = {
  /**
   * Tra cứu hồ sơ thẩm định KYB của bãi đỗ (Read-only, an toàn, không sinh bản ghi ảo).
   */
  getKybApplication: async (
    lotId: number,
    ownerProfileId: number
  ): Promise<KybApplicationDto> => {
    const response = await apiClient.get<KybApplicationDto>(
      `/api/v1/parking-lots/${lotId}/kyb`,
      {
        params: { ownerProfileId },
      }
    );
    return response.data;
  },

  /**
   * Lưu nháp tiến độ hồ sơ KYB (Upsert: Cập nhật nếu đã có, tạo mới nếu chưa; không sinh duplicate bản ghi).
   */
  saveDraft: async (
    lotId: number,
    data: SaveKybDraftRequest
  ): Promise<KybApplicationDto> => {
    const response = await apiClient.put<KybApplicationDto>(
      `/api/v1/parking-lots/${lotId}/kyb/draft`,
      data
    );
    return response.data;
  },

  /**
   * Nộp hồ sơ KYB chính thức (Upsert: Chuyển trạng thái sang Submitted; kiểm tra nghiêm ngặt 4 bước).
   */
  submit: async (
    lotId: number,
    data: SubmitKybApplicationRequest
  ): Promise<KybApplicationDto> => {
    const response = await apiClient.post<KybApplicationDto>(
      `/api/v1/parking-lots/${lotId}/kyb/submit`,
      data
    );
    return response.data;
  },
};
