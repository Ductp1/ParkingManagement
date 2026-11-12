import { adminRequest } from '@/api/adminClientApi';
import type { DashboardOverviewDto } from './dashboardTypes';

export const adminDashboardApi = {
  /**
   * Tổng quan Admin Dashboard (US-081): KPI, hàng đợi, governance, bảng – mỗi widget có trạng thái riêng.
   * signal: hủy request khi rời trang (AbortController của useDashboardOverview).
   */
  overview: (signal?: AbortSignal) =>
    adminRequest<DashboardOverviewDto>({ method: 'GET', url: '/api/v1/admin/dashboard/overview', signal }),
};
