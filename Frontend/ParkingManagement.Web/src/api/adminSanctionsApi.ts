import { adminRequest, cleanParams } from './adminClientApi';
import type { PagedResult } from '@/types/adminCommon';
import type {
  IssueSanctionRequest,
  IssueSanctionResultDto,
  LotSanctionStateDto,
  RevokeSanctionRequest,
  SanctionDetailDto,
  SanctionListQuery,
  SanctionStatsDto,
  SanctionSuggestionDto,
  SanctionSummaryDto,
} from '@/types/adminSanctions';

const BASE = '/api/v1/admin/sanctions';

/** signal: hủy request cũ khi người dùng đổi lựa chọn (AbortController của màn hình). */
export const adminSanctionsApi = {
  /**
   * Danh sách chế tài, mới áp trước (US-097).
   */
  list: (query: SanctionListQuery, page: number, pageSize: number, signal?: AbortSignal) =>
    adminRequest<PagedResult<SanctionSummaryDto>>({
      method: 'GET',
      url: BASE,
      signal,
      params: {
        ...cleanParams({
          status: query.status,
          level: query.level,
          ownerProfileId: query.ownerProfileId,
          parkingLotId: query.parkingLotId,
          search: query.search,
          from: query.from,
          to: query.to,
          syncFailed: query.syncFailed ? true : undefined,
        }),
        page,
        pageSize,
      },
    }),

  /**
   * KPI: đang hiệu lực theo cấp, đồng bộ lỗi / chờ, sắp hết hạn 7 ngày (US-097).
   */
  stats: (signal?: AbortSignal) => adminRequest<SanctionStatsDto>({ method: 'GET', url: `${BASE}/stats`, signal }),

  /**
   * Chi tiết + timeline audit + trạng thái đồng bộ từng service (US-097).
   */
  getById: (id: number, signal?: AbortSignal) => adminRequest<SanctionDetailDto>({ method: 'GET', url: `${BASE}/${id}`, signal }),

  /**
   * Áp chế tài: cấp 2/3 nhiều bãi trong 1 request; bãi nào trùng chế tài còn hiệu lực → 409 cả request (US-097).
   */
  issue: (data: IssueSanctionRequest) => adminRequest<IssueSanctionResultDto>({ method: 'POST', url: BASE, data }),

  /**
   * Gỡ chế tài còn hiệu lực, bắt buộc lý do (US-097).
   */
  revoke: (id: number, data: RevokeSanctionRequest) =>
    adminRequest<SanctionDetailDto>({ method: 'POST', url: `${BASE}/${id}/revoke`, data }),

  /**
   * Gửi lại các lệnh đồng bộ Pending / Failed (UserService, ParkingService, thông báo) bằng JWT Admin (US-097).
   */
  retrySync: (id: number) => adminRequest<SanctionDetailDto>({ method: 'POST', url: `${BASE}/${id}/retry-sync` }),

  /**
   * Gợi ý cấp kế tiếp theo chế tài trong 90 ngày gần nhất (US-097).
   */
  suggestion: (ownerProfileId: number, signal?: AbortSignal) =>
    adminRequest<SanctionSuggestionDto>({ method: 'GET', url: `/api/v1/admin/owners/${ownerProfileId}/sanction-suggestion`, signal }),

  /**
   * ParkingService (Admin): hiệu lực chế tài đang áp lên bãi + chủ bãi hiện tại của bãi (US-097).
   */
  lotState: (parkingLotId: number, signal?: AbortSignal) =>
    adminRequest<LotSanctionStateDto>({ method: 'GET', url: `/api/v1/parking-lots/${parkingLotId}/sanction-state`, signal }),
};
