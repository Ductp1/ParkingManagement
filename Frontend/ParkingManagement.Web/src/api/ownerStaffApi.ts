import { apiClient } from './client';

export interface StaffItemDto {
  id: number;
  userId: number;
  fullName: string;
  email?: string | null;
  parkingLotId: number;
  status: string;
  /** null khi ParkingService không trả lời. */
  lotStatus?: string | null;
  activationStatus: 'Invited' | 'Active';
}

export interface OwnerLotOptionDto {
  id: number;
  name?: string | null;
  status?: string | null;
}

export interface CreateStaffRequest {
  fullName: string;
  contact: string;
  parkingLotId: number;
}

export const STAFF_PAGE_SIZE = 20;

/**
 * Service API quản lý tài khoản nhân viên trực cổng (US-075 / UI-28).
 * Backend: UserService / StaffController (/api/v1/users/me/staff)
 */
export const ownerStaffApi = {
  getStaffList: async (page = 1, lotId?: number, signal?: AbortSignal): Promise<StaffItemDto[]> => {
    const response = await apiClient.get<StaffItemDto[]>('/api/v1/users/me/staff', {
      params: { page, lotId },
      signal,
    });
    return response.data;
  },

  /** Mọi phân công của một bãi (để chọn người xếp ca và hiện tên) – gộp các trang 20 dòng. */
  getAllStaff: async (lotId: number): Promise<StaffItemDto[]> => {
    const all: StaffItemDto[] = [];
    for (let page = 1; page <= 10; page++) {
      const items = await ownerStaffApi.getStaffList(page, lotId);
      all.push(...items);
      if (items.length < STAFF_PAGE_SIZE) break;
    }
    return all;
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

  resendInvite: async (assignmentId: number): Promise<void> => {
    await apiClient.post(`/api/v1/users/me/staff/${assignmentId}/resend-invite`);
  },

  revokeStaff: async (assignmentId: number): Promise<void> => {
    await apiClient.delete(`/api/v1/users/me/staff/${assignmentId}`);
  },
};
