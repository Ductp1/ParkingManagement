import { adminRequest, cleanParams } from './adminClientApi';
import type { PagedResult } from '@/types/adminCommon';
import type {
  CancelSystemConfigChangeRequest,
  PlatformSettingsDto,
  SystemConfigChangeDto,
  SystemConfigDetailDto,
  UpdateSystemConfigRequest,
} from '@/types/adminSettings';

export const adminSettingsApi = {
  /**
   * Tham số hệ thống + feature flag đang hiệu lực (US-098 / US-099).
   */
  getAll: (at?: string) =>
    adminRequest<PlatformSettingsDto>({ method: 'GET', url: '/api/v1/admin/settings', params: cleanParams({ at }) }),

  /**
   * Chi tiết một tham số kèm giới hạn giá trị (US-098).
   */
  getByKey: (key: string, at?: string) =>
    adminRequest<SystemConfigDetailDto>({
      method: 'GET',
      url: `/api/v1/admin/settings/${encodeURIComponent(key)}`,
      params: cleanParams({ at }),
    }),

  /**
   * Lịch sử thay đổi của một tham số (US-098).
   */
  getHistory: (key: string, page = 1, pageSize = 20) =>
    adminRequest<PagedResult<SystemConfigChangeDto>>({
      method: 'GET',
      url: `/api/v1/admin/settings/${encodeURIComponent(key)}/history`,
      params: { page, pageSize },
    }),

  /**
   * Đổi giá trị tham số, có thể hẹn ngày hiệu lực (US-098).
   */
  update: (key: string, data: UpdateSystemConfigRequest) =>
    adminRequest<SystemConfigChangeDto>({ method: 'PUT', url: `/api/v1/admin/settings/${encodeURIComponent(key)}`, data }),

  /**
   * Hủy thay đổi chưa tới ngày hiệu lực (US-098).
   */
  cancelChange: (key: string, changeId: number, data: CancelSystemConfigChangeRequest) =>
    adminRequest<SystemConfigChangeDto>({
      method: 'POST',
      url: `/api/v1/admin/settings/${encodeURIComponent(key)}/changes/${changeId}/cancel`,
      data,
    }),
};
