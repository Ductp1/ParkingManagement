import { adminFileRequest, adminRequest, cleanParams } from './adminClientApi';
import type { AdminDataRequestDto, AdminDataRequestQuery, AdminKycItemDto, KycImageSide } from '@/types/adminIdentity';

/** US-005 – route dưới "users" để Gateway chuyển sang UserService. */
const KYC = '/api/v1/users/kyc';
/** US-007 – yêu cầu xem/xuất/sửa/xóa dữ liệu cá nhân. */
const DATA_REQUESTS = '/api/v1/users/data-requests';

export const adminKycApi = {
  /** Hồ sơ đang chờ duyệt, gửi sớm nhất lên trước. */
  listPending: (page: number) =>
    adminRequest<AdminKycItemDto[]>({ method: 'GET', url: KYC, params: cleanParams({ page }) }),

  /** Ảnh giấy tờ chỉ đọc được qua phiên đăng nhập (không có URL công khai) → tải Blob rồi tạo object URL. */
  image: async (userId: number, side: KycImageSide) => (await adminFileRequest({ method: 'GET', url: `${KYC}/${userId}/images/${side}` })).blob,

  /** Pending → Verified. */
  approve: (userId: number) => adminRequest<void>({ method: 'POST', url: `${KYC}/${userId}/approve` }),

  /** Pending → Rejected; lý do bắt buộc (≤ 500 ký tự), số giấy tờ và ảnh bị xóa. */
  reject: (userId: number, reason: string) =>
    adminRequest<void>({ method: 'POST', url: `${KYC}/${userId}/reject`, data: { reason } }),
};

export const adminDataRequestsApi = {
  list: (query: AdminDataRequestQuery) =>
    adminRequest<AdminDataRequestDto[]>({
      method: 'GET',
      url: DATA_REQUESTS,
      params: cleanParams({ status: query.status, type: query.type, page: query.page }),
    }),

  /** Chỉ cho yêu cầu Deletion: ẩn danh hóa tài khoản. */
  approveDeletion: (id: number, note?: string) =>
    adminRequest<AdminDataRequestDto>({ method: 'POST', url: `${DATA_REQUESTS}/${id}/approve`, data: { note: note || null } }),

  /** Chỉ cho yêu cầu Correction: ghi chú nội dung đã sửa là bắt buộc. */
  completeCorrection: (id: number, note: string) =>
    adminRequest<AdminDataRequestDto>({ method: 'POST', url: `${DATA_REQUESTS}/${id}/complete`, data: { note } }),

  /** Từ chối, lý do bắt buộc (≤ 1000 ký tự). */
  reject: (id: number, note: string) =>
    adminRequest<AdminDataRequestDto>({ method: 'POST', url: `${DATA_REQUESTS}/${id}/reject`, data: { note } }),
};
