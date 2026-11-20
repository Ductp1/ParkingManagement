import { adminRequest } from './adminClientApi';
import type { LotCapacityConfigDto } from './capacityApi';
import type {
  AdminGateOccupancyDto,
  AdminLotsSummaryDto,
  LotAttributesDto,
  LotClosureDto,
  LotOperatingHoursWeeklyDto,
  RateCardDto,
} from '@/types/adminLots';

/**
 * US-081: nguồn dữ liệu bãi cho admin portal (Parking Lots, bộ chọn bãi) – qua Gateway, chỉ role Admin.
 * Tách khỏi adminOperationsApi để không chạm file đang được nhánh US-069 sửa.
 */
export const adminLotsApi = {
  /** Mọi bãi chưa xóa (ParkingService), sắp theo tên. */
  lotsSummary: () => adminRequest<AdminLotsSummaryDto>({ method: 'GET', url: '/api/v1/parking-lots/admin/lots-summary' }),

  /** Xe đang trong bãi theo từng bãi (GateService). */
  occupancy: () => adminRequest<AdminGateOccupancyDto>({ method: 'GET', url: '/api/v1/gate/admin/occupancy' }),

  // ===== Lot detail sub-tabs (P0-4) – chỉ đọc =====

  /**
   * Sức chứa vận hành (GateService, chỉ đọc, có số xe đang đỗ). Không dùng ParkingService /parking-lots/{id}/capacity-config
   * vì GET đó tự tạo cấu hình và ghi DB (audit §7.5).
   */
  capacity: (lotId: number) => adminRequest<LotCapacityConfigDto>({ method: 'GET', url: `/api/v1/lots/${lotId}/capacity-config` }),

  /** Giờ mở cửa tuần hiện tại (ParkingService). */
  operatingHours: (lotId: number) =>
    adminRequest<LotOperatingHoursWeeklyDto>({ method: 'GET', url: `/api/v1/parking-lots/${lotId}/operating-hours` }),

  /** Mọi lịch đóng cửa của bãi (đang hiệu lực, sắp tới, đã qua, đã hủy). */
  closures: (lotId: number) => adminRequest<LotClosureDto[]>({ method: 'GET', url: `/api/v1/parking-lots/${lotId}/closures` }),

  /** Tiện ích + ảnh + thông tin mô tả (ParkingService). */
  attributes: (lotId: number) => adminRequest<LotAttributesDto>({ method: 'GET', url: `/api/v1/parking-lots/${lotId}/attributes` }),

  /** Bảng giá của bãi (PaymentService). */
  rateCards: (lotId: number) => adminRequest<RateCardDto[]>({ method: 'GET', url: '/api/v1/rate-cards', params: { parkingLotId: lotId } }),
};
