/**
 * Mẫu hợp đồng bãi = loại hợp đồng có tên (US-116 v11, T0–T6) – AdminService, khớp
 * Services/AdminService/AdminService.Application/Features/ContractTemplates/*.cs.
 * Kind: Structured (soạn bằng form), Markup (mẫu cũ gõ cú pháp), Document (file DOCX gốc).
 * Draft (sửa được) → Active (chọn được khi lập hợp đồng; nhiều mẫu cùng lúc) → Retired.
 */
export type ContractTemplateStatus = 'Draft' | 'Active' | 'Retired';
export type ContractTemplateKind = 'Structured' | 'Markup' | 'Document';
export type TemplateAppendixKind = 'LotInfo' | 'PayoutAccount' | 'Text';

/** Bên A (nền tảng) – đi theo từng mẫu, in qua placeholder {{Platform…}}. */
export interface PlatformPartyDto {
  name: string;
  address: string;
  taxCode: string;
  phone: string;
  email: string;
  representativeName: string;
  representativePosition: string;
}

export interface TemplateHeaderDto {
  placeOfSigning: string;
  title: string;
  subtitle: string | null;
}

/** Một điều: tiêu đề + các khoản (số "n.m." do hệ thống đánh). isSpecialTerms = điều in thỏa thuận riêng của từng bãi. */
export interface TemplateArticleDto {
  title: string;
  clauses: string[];
  isSpecialTerms: boolean;
}

/** LotInfo / PayoutAccount: bảng do hệ thống dựng; Text: chỉ ghi chú. */
export interface TemplateAppendixDto {
  kind: TemplateAppendixKind;
  title: string;
  subtitle: string | null;
  notes: string[];
}

/** Nội dung mẫu dạng form. Chữ có thể chứa trường {{Key}} trong danh mục placeholder. */
export interface TemplateContentDto {
  header: TemplateHeaderDto;
  legalBases: string[];
  introduction: string | null;
  articles: TemplateArticleDto[];
  appendices: TemplateAppendixDto[];
  signNote: string | null;
  footnote: string | null;
}

export interface ContractTemplateSummaryDto {
  id: number;
  version: number;
  name: string;
  kind: ContractTemplateKind | string;
  status: ContractTemplateStatus | string;
  changeNote: string | null;
  createdAtUtc: string;
  updatedAtUtc: string | null;
  activatedAtUtc: string | null;
  createdByAdminId: number | null;
  /** Số phiên bản hợp đồng bãi đã sinh từ mẫu này. */
  usedByVersions: number;
}

export interface ContractTemplateDetailDto extends ContractTemplateSummaryDto {
  /** Có với mẫu Structured (và Document đã từng soạn form). */
  content: TemplateContentDto | null;
  /** Markup: sinh từ form (Structured) hoặc gõ tay (Markup). */
  body: string;
  platform: PlatformPartyDto;
  /** Tên file DOCX gốc (mẫu nhập từ file). */
  sourceFileName: string | null;
  /** Cảnh báo của AI / bộ tách khi nhập file (mỗi dòng một cảnh báo). */
  importNotes: string | null;
  sourceTemplateId: number | null;
  sourceTemplateName: string | null;
  /** Base64 – gửi lại khi sửa / đổi tên / áp dụng / thôi áp dụng. */
  rowVersion: string;
}

/** Placeholder dùng được trong mẫu. block = phải đứng riêng một dòng. */
export interface ContractPlaceholderDto {
  key: string;
  group: string;
  description: string;
  sample: string;
  required: boolean;
  block: boolean;
}

/** T2: Blank = khung form mặc định; Duplicate = chép mẫu sourceTemplateId. */
export interface CreateContractTemplateRequest {
  name: string;
  mode: 'Blank' | 'Duplicate';
  sourceTemplateId?: number;
}

/** T2i (multipart): file .docx ≤ 10 MB; useAi = gửi chữ của file cho AI (Anthropic) phân tích. */
export interface ImportContractTemplateRequest {
  file: File;
  name?: string;
  useAi: boolean;
}

/** T3: Structured gửi content; Markup gửi body; Document dùng file gốc (content giữ lại nếu có). */
export interface UpdateContractTemplateDraftRequest {
  name: string;
  changeNote: string;
  kind: ContractTemplateKind;
  content?: TemplateContentDto | null;
  body?: string;
  platform: PlatformPartyDto;
  rowVersion: string;
}

/** T4: xem trước nội dung đang soạn (chưa cần lưu) với dữ liệu hợp đồng giả định. */
export interface PreviewContractTemplateRequest {
  kind: ContractTemplateKind;
  content?: TemplateContentDto | null;
  body?: string;
  platform: PlatformPartyDto;
}
