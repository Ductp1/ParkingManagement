import { apiClient } from './client';

/**
 * US-066 – API cấu hình sức chứa & vùng đệm vãng lai.
 * Route qua Gateway :5000 → GateService :5107 (route Gateway "lots").
 */
export interface LotCapacityConfigDto {
  configured: boolean;
  capacity: number;
  transientBuffer: number;
  subscriberQuota: number;
  totalParked: number;
  transientParked: number;
  subscriberParked: number;
  activeSubscribers: number;
  availableForTransient: number;
  availableForSubscriber: number;
  version: number;
  updatedAtUtc: string | null;
}

export interface UpdateLotCapacityResult {
  saved: boolean;
  warnings: string[];
  version: number;
  snapshot: LotCapacityConfigDto;
}

export const capacityApi = {
  get: async (lotId: number): Promise<LotCapacityConfigDto> => {
    const response = await apiClient.get<LotCapacityConfigDto>(`/api/v1/lots/${lotId}/capacity-config`);
    return response.data;
  },

  put: async (
    lotId: number,
    body: { capacity: number; transientBuffer: number; subscriberQuota: number; version: number },
  ): Promise<UpdateLotCapacityResult> => {
    const response = await apiClient.put<UpdateLotCapacityResult>(`/api/v1/lots/${lotId}/capacity-config`, body);
    return response.data;
  },
};