import { adminRequest, cleanParams } from './adminClientApi';
import type { PagedResult } from '@/types/adminCommon';
import type {
  PartnerApplicationDetailDto,
  PartnerApplicationListQuery,
  PartnerApplicationSummaryDto,
  PartnerReasonRequest,
  PartnerRequestInfoRequest,
} from '@/types/adminPartnerOnboarding';

/** Partner Onboarding – endpoint DỰ KIẾN A1–A12 (AdminService, chưa triển khai). */
const BASE = '/api/v1/admin/partner-applications';

export const adminPartnerOnboardingApi = {
  /** A1 – danh sách hồ sơ đối tác. */
  list: (query: PartnerApplicationListQuery) =>
    adminRequest<PagedResult<PartnerApplicationSummaryDto>>({
      method: 'GET',
      url: BASE,
      params: cleanParams({ status: query.status, search: query.search, page: query.page, pageSize: query.pageSize }),
    }),

  /** A2 – chi tiết hồ sơ. */
  getById: (id: number) => adminRequest<PartnerApplicationDetailDto>({ method: 'GET', url: `${BASE}/${id}` }),

  /** A3 – Submitted → UnderReview. */
  startReview: (id: number) => adminRequest<PartnerApplicationDetailDto>({ method: 'POST', url: `${BASE}/${id}/start-review` }),

  /** A4 – yêu cầu bổ sung thông tin. */
  requestInfo: (id: number, data: PartnerRequestInfoRequest) =>
    adminRequest<PartnerApplicationDetailDto>({ method: 'POST', url: `${BASE}/${id}/request-info`, data }),

  /** A10 – gửi lại link kích hoạt. */
  resendActivation: (id: number) =>
    adminRequest<PartnerApplicationDetailDto>({ method: 'POST', url: `${BASE}/${id}/resend-activation` }),

  /** A11 – từ chối hồ sơ. */
  reject: (id: number, data: PartnerReasonRequest) =>
    adminRequest<PartnerApplicationDetailDto>({ method: 'POST', url: `${BASE}/${id}/reject`, data }),

  /** A12 – hủy hồ sơ. */
  cancel: (id: number, data: PartnerReasonRequest) =>
    adminRequest<PartnerApplicationDetailDto>({ method: 'POST', url: `${BASE}/${id}/cancel`, data }),
};
