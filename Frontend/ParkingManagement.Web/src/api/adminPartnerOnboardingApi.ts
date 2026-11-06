import { adminRequest, cleanParams } from './adminClientApi';
import type { PagedResult } from '@/types/adminCommon';
import type {
  IssueOwnerAccountRequest,
  PartnerApplicationDetailDto,
  PartnerApplicationListQuery,
  PartnerApplicationSummaryDto,
  PartnerReasonRequest,
  PartnerRequestInfoRequest,
} from '@/types/adminPartnerOnboarding';

/** Partner Onboarding (US-116) – AdminService, chỉ role Admin. Hợp đồng ký theo từng bãi: adminLotContractsApi. */
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

  /** A4 – UnderReview → NeedsMoreInfo. */
  requestInfo: (id: number, data: PartnerRequestInfoRequest) =>
    adminRequest<PartnerApplicationDetailDto>({ method: 'POST', url: `${BASE}/${id}/request-info`, data }),

  /** A9 – UnderReview → AccountIssued: UserService tạo tài khoản chủ bãi; lưu tài khoản nhận thanh toán. */
  issueAccount: (id: number, data: IssueOwnerAccountRequest) =>
    adminRequest<PartnerApplicationDetailDto>({ method: 'POST', url: `${BASE}/${id}/issue-account`, data }),

  /** A10 – gửi lại link kích hoạt (UserService). */
  resendActivation: (id: number) =>
    adminRequest<PartnerApplicationDetailDto>({ method: 'POST', url: `${BASE}/${id}/resend-activation` }),

  /** A11 – từ chối hồ sơ. */
  reject: (id: number, data: PartnerReasonRequest) =>
    adminRequest<PartnerApplicationDetailDto>({ method: 'POST', url: `${BASE}/${id}/reject`, data }),

  /** A12 – hủy hồ sơ. */
  cancel: (id: number, data: PartnerReasonRequest) =>
    adminRequest<PartnerApplicationDetailDto>({ method: 'POST', url: `${BASE}/${id}/cancel`, data }),
};
