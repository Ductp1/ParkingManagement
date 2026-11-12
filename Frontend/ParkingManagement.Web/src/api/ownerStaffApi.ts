import { apiClient } from './client';

export interface StaffItemDto {
  id: number;
  userId: number;
  fullName: string;
  parkingLotId: number;
  status: 'PendingVerification' | 'Active' | 'Locked' | 'Banned' | string;
}

export interface OwnerLotOptionDto {
  id: number;
  name?: string | null;
}

export interface CreateStaffRequest {
  fullName: string;
  contact: string;
  parkingLotId: number;
}

/**
 * Service API quản lý tài khoản nhân viên trực cổng (US-075 / UI-28).
 * Backend: UserService / StaffController (/api/v1/users/me/staff)
 */
export const ownerStaffApi = {
  getStaffList: async (page = 1, signal?: AbortSignal): Promise<StaffItemDto[]> => {
    const response = await apiClient.get<StaffItemDto[]>('/api/v1/users/me/staff', {
      params: { page },
      signal,
    });
    return response.data;
  },

  getOwnerLots: async (signal?: AbortSignal): Promise<OwnerLotOptionDto[]> => {
    const response = await apiClient.get<OwnerLotOptionDto[]>('/api/v1/users/me/staff/lots', {
      signal,
    });
    return response.data;
  },

  createStaff: async (data: CreateStaffRequest): Promise<StaffItemDto> => {
    const response = await apiClient.post<StaffItemDto>('/api/v1/users/me/staff', data);
    return response.data;
  },

  revokeStaff: async (assignmentId: number): Promise<void> => {
    await apiClient.delete(`/api/v1/users/me/staff/${assignmentId}`);
  },
};
