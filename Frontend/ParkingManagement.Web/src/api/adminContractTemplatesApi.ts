import { adminFileRequest, adminRequest } from './adminClientApi';
import type {
  ContractPlaceholderDto,
  ContractTemplateDetailDto,
  ContractTemplateSummaryDto,
  CreateContractTemplateRequest,
  ImportContractTemplateRequest,
  PreviewContractTemplateRequest,
  UpdateContractTemplateDraftRequest,
} from '@/types/adminContractTemplates';
import type { AdminFile } from '@/types/adminPartnerOnboarding';

/** Mẫu hợp đồng bãi (US-116 v11, T0–T6) – AdminService, chỉ role Admin. */
const BASE = '/api/v1/admin/contract-templates';

export const adminContractTemplatesApi = {
  /** T0 – danh sách, mới nhất trước. */
  list: () => adminRequest<ContractTemplateSummaryDto[]>({ method: 'GET', url: BASE }),

  /** T1 – chi tiết (nội dung form / markup + Bên A + rowVersion). */
  getById: (id: number) => adminRequest<ContractTemplateDetailDto>({ method: 'GET', url: `${BASE}/${id}` }),

  /** T1p – danh mục trường (placeholder) cho menu "Insert field". */
  placeholders: () => adminRequest<ContractPlaceholderDto[]>({ method: 'GET', url: `${BASE}/placeholders` }),

  /** T1f – file DOCX gốc. */
  sourceFile: (id: number): Promise<AdminFile> => adminFileRequest({ method: 'GET', url: `${BASE}/${id}/source-file` }),

  /** T2 – mẫu mới (khung form trống hoặc bản sao). */
  create: (data: CreateContractTemplateRequest) => adminRequest<ContractTemplateDetailDto>({ method: 'POST', url: BASE, data }),

  /** T2i – nhập từ file .docx (multipart), AI tùy chọn. */
  importDocx: (data: ImportContractTemplateRequest) => {
    const form = new FormData();
    form.append('file', data.file);
    if (data.name) form.append('name', data.name);
    form.append('useAi', String(data.useAi));
    // Ghi đè JSON mặc định của apiClient; trình duyệt tự thêm boundary.
    return adminRequest<ContractTemplateDetailDto>({
      method: 'POST',
      url: `${BASE}/import`,
      data: form,
      headers: { 'Content-Type': 'multipart/form-data' },
      timeout: 300_000,
    });
  },

  /** T3 – sửa bản nháp. */
  updateDraft: (id: number, data: UpdateContractTemplateDraftRequest) =>
    adminRequest<ContractTemplateDetailDto>({ method: 'PUT', url: `${BASE}/${id}`, data }),

  /** T3n – đổi tên (mọi trạng thái). */
  rename: (id: number, name: string, rowVersion: string) =>
    adminRequest<ContractTemplateDetailDto>({ method: 'PATCH', url: `${BASE}/${id}/name`, data: { name, rowVersion } }),

  /** T4 – xem trước (HTML có dấu "bản xem trước", dữ liệu giả định, không lưu). */
  preview: (data: PreviewContractTemplateRequest): Promise<AdminFile> =>
    adminFileRequest({ method: 'POST', url: `${BASE}/preview`, data }),

  /** T5 – áp dụng bản nháp; retireSource = thôi áp dụng mẫu nguồn. */
  activate: (id: number, rowVersion: string, retireSource: boolean) =>
    adminRequest<ContractTemplateDetailDto>({ method: 'POST', url: `${BASE}/${id}/activate`, data: { rowVersion, retireSource } }),

  /** T5r – thôi áp dụng. */
  retire: (id: number, rowVersion: string) =>
    adminRequest<ContractTemplateDetailDto>({ method: 'POST', url: `${BASE}/${id}/retire`, data: { rowVersion } }),

  /** T6c – mẫu markup cũ → bản nháp dạng form. */
  convert: (id: number) => adminRequest<ContractTemplateDetailDto>({ method: 'POST', url: `${BASE}/${id}/convert` }),

  /** T6 – hủy bản nháp. */
  discard: (id: number) => adminRequest<void>({ method: 'DELETE', url: `${BASE}/${id}` }),
};
