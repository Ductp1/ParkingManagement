import { adminRequest } from './adminClientApi';
import type { AdminGateOccupancyDto, AdminLotsSummaryDto } from '@/types/adminLots';

/**
 * US-081: nguồn dữ liệu bãi cho admin portal (Parking Lots, bộ chọn bãi) – qua Gateway, chỉ role Admin.
 * Tách khỏi adminOperationsApi để không chạm file đang được nhánh US-069 sửa.
 */
export const adminLotsApi = {
  /** Mọi bãi chưa xóa (ParkingService), sắp theo tên. */
  lotsSummary: () => adminRequest<AdminLotsSummaryDto>({ method: 'GET', url: '/api/v1/parking-lots/admin/lots-summary' }),

  /** Xe đang trong bãi theo từng bãi (GateService). */
  occupancy: () => adminRequest<AdminGateOccupancyDto>({ method: 'GET', url: '/api/v1/gate/admin/occupancy' }),
};
