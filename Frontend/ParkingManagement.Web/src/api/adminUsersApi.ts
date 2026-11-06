import { adminRequest, cleanParams } from './adminClientApi';
import type { PagedResult } from '@/types/adminCommon';
import type {
  AdminCurrentUserDto,
  AdminUserDto,
  AdminUserListQuery,
  AdminUserSummaryDto,
  LockOwnerRequest,
  SanctionDto,
  UnlockOwnerRequest,
} from '@/types/adminUsers';

export const adminUsersApi = {
  /**
   * Danh sách người dùng có lọc theo vai trò (US-096) – AdminService gọi UserService.
   */
  list: (query: AdminUserListQuery) =>
    adminRequest<PagedResult<AdminUserSummaryDto>>({
      method: 'GET',
      url: '/api/v1/admin/users',
      params: cleanParams({ role: query.role, page: query.page, pageSize: query.pageSize }),
    }),

  /**
   * Chi tiết một người dùng (US-096).
   */
  getById: (userId: number) =>
    adminRequest<AdminUserDto>({ method: 'GET', url: `/api/v1/admin/users/${userId}` }),

  /**
   * Lịch sử chế tài của chủ bãi (US-096).
   */
  listOwnerSanctions: (ownerProfileId: number, page = 1, pageSize = 20) =>
    adminRequest<PagedResult<SanctionDto>>({
      method: 'GET',
      url: `/api/v1/admin/owners/${ownerProfileId}/sanctions`,
      params: { page, pageSize },
    }),

  /**
   * Khóa chủ bãi (US-096): lưu Sanction + AuditLog rồi yêu cầu UserService khóa.
   */
  lockOwner: (ownerProfileId: number, data: LockOwnerRequest) =>
    adminRequest<SanctionDto>({ method: 'POST', url: `/api/v1/admin/owners/${ownerProfileId}/lock`, data }),

  /**
   * Mở khóa chủ bãi (US-096).
   */
  unlockOwner: (ownerProfileId: number, data: UnlockOwnerRequest) =>
    adminRequest<SanctionDto>({ method: 'POST', url: `/api/v1/admin/owners/${ownerProfileId}/unlock`, data }),

  /**
   * Người dùng đang đăng nhập (UserService) – hiển thị trên sidebar / profile menu.
   */
  getCurrentUser: () => adminRequest<AdminCurrentUserDto>({ method: 'GET', url: '/api/v1/users/me' }),
};
