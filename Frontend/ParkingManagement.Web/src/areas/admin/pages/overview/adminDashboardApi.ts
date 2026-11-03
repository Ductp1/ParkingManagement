import { adminRequest } from '@/api/adminClientApi';
import type { DashboardOverviewDto } from './dashboardTypes';

export const adminDashboardApi = {
  /**
   * Tổng quan Admin Dashboard (US-081): KPI, hàng đợi, governance, bảng – mỗi widget có trạng thái riêng.
   */
  overview: () => adminRequest<DashboardOverviewDto>({ method: 'GET', url: '/api/v1/admin/dashboard/overview' }),
};
