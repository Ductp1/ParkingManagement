import { adminFileRequest, adminRequest, cleanParams } from './adminClientApi';
import type { PagedResult } from '@/types/adminCommon';
import type {
  AdminFile,
  IssueOwnerAccountRequest,
  PartnerApplicationDetailDto,
  PartnerApplicationListQuery,
  PartnerApplicationSummaryDto,
  PartnerContractTermsRequest,
  PartnerReasonRequest,
  PartnerRequestInfoRequest,
  PartnerReviseContractRequest,
} from '@/types/adminPartnerOnboarding';

/** Partner Onboarding (US-116) – AdminService A1–A12, chỉ role Admin. */
const BASE = '/api/v1/admin/partner-applications';

export const adminPartnerOnboardingApi = {
  /** A1 – danh sách hồ sơ đối tác. */
  list: (query: PartnerApplicationListQuery) =>
    adminRequest<PagedResult<PartnerApplicationSummaryDto>>({
      method: 'GET',
      url: BASE,
      params: cleanParams({ status: query.status, search: query.search, page: query.page, pageSize: query.pageSize }),
    }),

  /** A2 – chi tiết hồ sơ kèm các phiên bản hợp đồng. */
  getById: (id: number) => adminRequest<PartnerApplicationDetailDto>({ method: 'GET', url: `${BASE}/${id}` }),

  /** A3 – Submitted → UnderReview. */
  startReview: (id: number) => adminRequest<PartnerApplicationDetailDto>({ method: 'POST', url: `${BASE}/${id}/start-review` }),

  /** A4 – UnderReview → NeedsMoreInfo. */
  requestInfo: (id: number, data: PartnerRequestInfoRequest) =>
    adminRequest<PartnerApplicationDetailDto>({ method: 'POST', url: `${BASE}/${id}/request-info`, data }),

  /** A5 – sinh thử hợp đồng (HTML), không lưu, không đổi trạng thái. */
  previewContract: (id: number, data: PartnerContractTermsRequest): Promise<AdminFile> =>
    adminFileRequest({ method: 'POST', url: `${BASE}/${id}/contract-preview`, data }),

  /** A6 – UnderReview → ContractPending, sinh hợp đồng v1. */
  sendContract: (id: number, data: PartnerContractTermsRequest) =>
    adminRequest<PartnerApplicationDetailDto>({ method: 'POST', url: `${BASE}/${id}/send-contract`, data }),

  /** A7 – sinh phiên bản v+1 cùng số hợp đồng, bản cũ Superseded. */
  reviseContract: (id: number, data: PartnerReviseContractRequest) =>
    adminRequest<PartnerApplicationDetailDto>({ method: 'POST', url: `${BASE}/${id}/revise-contract`, data }),

  /** A8 – tải file hợp đồng một phiên bản (SHA-256 được kiểm tra lại ở server). */
  contractFile: (id: number, version: number): Promise<AdminFile> =>
    adminFileRequest({ method: 'GET', url: `${BASE}/${id}/contracts/${version}/file` }),

  /** A9 – ContractPending → AccountIssued qua UserService (multipart: bản scan đã ký + phiên bản xác nhận). */
  issueAccount: (id: number, data: IssueOwnerAccountRequest) => {
    const form = new FormData();
    form.append('confirmedDraftVersion', String(data.confirmedDraftVersion));
    form.append('signedFile', data.signedFile);
    // Ghi đè JSON mặc định của apiClient; trình duyệt tự thêm boundary.
    return adminRequest<PartnerApplicationDetailDto>({
      method: 'POST',
      url: `${BASE}/${id}/issue-account`,
      data: form,
      headers: { 'Content-Type': 'multipart/form-data' },
    });
  },

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
