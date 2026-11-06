import { adminFileRequest, adminRequest, cleanParams } from './adminClientApi';
import type { PagedResult } from '@/types/adminCommon';
import type {
  CreateLotContractRequest,
  LotContractDetailDto,
  LotContractListQuery,
  LotContractReasonRequest,
  LotContractSummaryDto,
  LotContractTermsRequest,
  ReviseLotContractRequest,
  SignLotContractRequest,
} from '@/types/adminLotContracts';
import type { AdminFile } from '@/types/adminPartnerOnboarding';

/** Hợp đồng theo từng bãi (US-116, L0–L4) – AdminService, chỉ role Admin. */
const BASE = '/api/v1/admin/lot-contracts';

export const adminLotContractsApi = {
  /** L0 – danh sách (lọc trạng thái / bãi / chủ bãi, tìm số hợp đồng / tên bãi / doanh nghiệp / MST). */
  list: (query: LotContractListQuery) =>
    adminRequest<PagedResult<LotContractSummaryDto>>({
      method: 'GET',
      url: BASE,
      params: cleanParams({
        status: query.status,
        parkingLotId: query.parkingLotId,
        ownerProfileId: query.ownerProfileId,
        search: query.search,
        page: query.page,
        pageSize: query.pageSize,
      }),
    }),

  /** L0b – chi tiết kèm các phiên bản. */
  getById: (id: number) => adminRequest<LotContractDetailDto>({ method: 'GET', url: `${BASE}/${id}` }),

  /** L1p – xem trước hợp đồng v1 cho hồ sơ KYB đã duyệt (HTML, có dấu "bản xem trước", không lưu). */
  previewNew: (data: CreateLotContractRequest): Promise<AdminFile> => adminFileRequest({ method: 'POST', url: `${BASE}/preview`, data }),

  /** L1 – lập hợp đồng v1. */
  create: (data: CreateLotContractRequest) => adminRequest<LotContractDetailDto>({ method: 'POST', url: BASE, data }),

  /** L2p – xem trước phiên bản v+1. */
  previewRevision: (id: number, data: LotContractTermsRequest): Promise<AdminFile> =>
    adminFileRequest({ method: 'POST', url: `${BASE}/${id}/preview`, data }),

  /** L2 – sửa: phiên bản v+1 cùng số hợp đồng. */
  revise: (id: number, data: ReviseLotContractRequest) =>
    adminRequest<LotContractDetailDto>({ method: 'POST', url: `${BASE}/${id}/revise`, data }),

  /** L2f – tải file một phiên bản (SHA-256 kiểm tra lại ở server). */
  versionFile: (id: number, version: number): Promise<AdminFile> =>
    adminFileRequest({ method: 'GET', url: `${BASE}/${id}/versions/${version}/file` }),

  /** L3 – ký = duyệt KYB (multipart: bản scan + phiên bản xác nhận + ghi chú); ParkingService duyệt KYB và kích hoạt bãi. */
  sign: (id: number, data: SignLotContractRequest) => {
    const form = new FormData();
    form.append('confirmedVersion', String(data.confirmedVersion));
    form.append('signedFile', data.signedFile);
    if (data.note) form.append('note', data.note);
    // Ghi đè JSON mặc định của apiClient; trình duyệt tự thêm boundary.
    return adminRequest<LotContractDetailDto>({
      method: 'POST',
      url: `${BASE}/${id}/sign`,
      data: form,
      headers: { 'Content-Type': 'multipart/form-data' },
    });
  },

  /** L4 – hủy trước khi ký (bãi vẫn chờ duyệt). */
  cancel: (id: number, data: LotContractReasonRequest) =>
    adminRequest<LotContractDetailDto>({ method: 'POST', url: `${BASE}/${id}/cancel`, data }),
};
