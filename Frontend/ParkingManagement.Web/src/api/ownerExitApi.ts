import { apiClient } from './client';

export interface OwnerLotSummaryItemDto {
  lotId: number;
  name: string;
  address: string;
  status: string;
  totalSlots: number;
  availableSlots: number;
}

export interface OwnerExitImpactSummaryDto {
  ownerProfileId: number;
  totalLots: number;
  activeLots: number;
  suspendedLots: number;
  totalSlots: number;
  lots: OwnerLotSummaryItemDto[];
}

export interface OwnerExitStatusDto {
  ownerProfileId: number;
  userId: number;
  businessName: string;
  status: 'Active' | 'Leaving' | 'Terminated' | string;
  exitRequestedAtUtc?: string | null;
  exitEffectiveAtUtc?: string | null;
  remainingDays?: number | null;
  canCancel: boolean;
  exitReason?: string | null;
}

export interface RequestOwnerExitRequest {
  reason: string;
  noticeDays?: number;
}

export interface CancelOwnerExitRequest {
  reason?: string;
}

/**
 * Service API quy trình Chủ bãi xin rời nền tảng (UI-21 / US-057).
 * Backend: UserService / UsersController (/api/v1/users/owner-profiles) & ParkingService / ParkingLotsController (/api/v1/parking-lots/owner-exit)
 */
export const ownerExitApi = {
  /**
   * Lấy trạng thái rời nền tảng hiện tại của hồ sơ chủ bãi.
   */
  getExitStatus: async (ownerProfileId: number): Promise<OwnerExitStatusDto> => {
    const response = await apiClient.get<OwnerExitStatusDto>(
      `/api/v1/users/owner-profiles/${ownerProfileId}/exit-status`
    );
    return response.data;
  },

  /**
   * Đánh giá tác động tới các bãi đỗ xe trước khi gửi yêu cầu rời nền tảng.
   */
  getExitImpact: async (ownerProfileId: number): Promise<OwnerExitImpactSummaryDto> => {
    const response = await apiClient.get<OwnerExitImpactSummaryDto>(
      '/api/v1/parking-lots/owner-exit/impact',
      {
        params: { ownerProfileId },
      }
    );
    return response.data;
  },

  /**
   * Gửi yêu cầu xin rời nền tảng (thông báo trước 30 ngày theo hợp đồng hợp tác).
   */
  requestExit: async (
    ownerProfileId: number,
    data: RequestOwnerExitRequest
  ): Promise<OwnerExitStatusDto> => {
    const response = await apiClient.post<OwnerExitStatusDto>(
      `/api/v1/users/owner-profiles/${ownerProfileId}/request-exit`,
      data
    );
    return response.data;
  },

  /**
   * Rút lại yêu cầu xin rời nền tảng khi còn trong thời hạn 30 ngày thông báo.
   */
  cancelExit: async (
    ownerProfileId: number,
    data?: CancelOwnerExitRequest
  ): Promise<OwnerExitStatusDto> => {
    const response = await apiClient.post<OwnerExitStatusDto>(
      `/api/v1/users/owner-profiles/${ownerProfileId}/cancel-exit`,
      data || {}
    );
    return response.data;
  },
};
