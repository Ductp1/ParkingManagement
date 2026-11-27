import type { PartnerApplicationStatus } from './adminEnums';

/**
 * Partner Onboarding (US-116) – AdminService, khớp
 * Services/AdminService/AdminService.Application/Features/PartnerOnboarding/*.cs.
 * Hồ sơ đối tác chỉ còn thẩm định + cấp tài khoản; hợp đồng ký theo từng bãi (types/adminLotContracts.ts).
 */
export interface PartnerApplicationSummaryDto {
  id: number;
  code: string;
  businessName: string;
  taxCode: string;
  fullName: string;
  email: string;
  phone: string;
  lotCount: number;
  status: PartnerApplicationStatus | string;
  reviewerAdminId: number | null;
  createdOwnerProfileId: number | null;
  createdAtUtc: string;
  updatedAtUtc: string | null;
}

/** Tài khoản nhận thanh toán (in vào Phụ lục 02 của hợp đồng từng bãi). */
export interface PartnerBankAccountDto {
  bin: string;
  name: string;
  accountNumber: string;
  accountName: string;
}

/** Chi tiết hồ sơ (A2; mọi thao tác A3–A12 trả về cùng DTO này). nextStatuses = trạng thái có thể chuyển tới theo bảng T2–T10. */
export interface PartnerApplicationDetailDto {
  id: number;
  code: string;
  status: PartnerApplicationStatus | string;
  fullName: string;
  email: string;
  phone: string;
  position: string | null;
  businessType: string;
  businessName: string;
  taxCode: string;
  businessAddress: string;
  legalRepresentativeName: string;
  legalRepresentativePosition: string | null;
  businessLicenseUrl: string | null;
  lotCount: number;
  primaryLotAddress: string;
  estimatedCarSlots: number | null;
  estimatedMotorbikeSlots: number | null;
  operatingHours: string | null;
  consentAcceptedAtUtc: string;
  reviewerAdminId: number | null;
  infoRequestNote: string | null;
  rejectReason: string | null;
  cancelReason: string | null;
  /** null khi chưa cấp tài khoản. */
  payoutBank: PartnerBankAccountDto | null;
  createdUserId: number | null;
  createdOwnerProfileId: number | null;
  createdAtUtc: string;
  updatedAtUtc: string | null;
  nextStatuses: Array<PartnerApplicationStatus | string>;
  /** Gửi lại khi hủy / yêu cầu bổ sung: người khác vừa sửa hồ sơ → 409. */
  rowVersion?: string | null;
  cancelledAtUtc?: string | null;
  /** Gỡ tài khoản chủ bãi chưa kích hoạt sau khi hủy hồ sơ: NotRequired | Pending | Synced | Failed. */
  cleanupSyncStatus?: PartnerSyncStatus | string | null;
  cleanupError?: string | null;
  /** Khóa các mục Admin yêu cầu bổ sung (xem PARTNER_INFO_ITEMS); rỗng = không chỉ định. */
  infoRequestItems?: string[] | null;
  infoRequestedAtUtc?: string | null;
  infoRequestDueAtUtc?: string | null;
  infoProvidedAtUtc?: string | null;
  /** Email yêu cầu bổ sung: NotRequired | Pending | Synced (đã xếp hàng gửi) | Failed. */
  infoRequestEmailStatus?: PartnerSyncStatus | string | null;
  infoRequestEmailError?: string | null;
}

export type PartnerSyncStatus = 'NotRequired' | 'Pending' | 'Synced' | 'Failed';

/** Mục Admin có thể yêu cầu bổ sung – khớp PartnerInfoItems.Labels (AdminService). */
export const PARTNER_INFO_ITEMS: ReadonlyArray<{ key: string; label: string; group: 'field' | 'document' }> = [
  { key: 'fullName', label: 'Full name', group: 'field' },
  { key: 'position', label: 'Position', group: 'field' },
  { key: 'phone', label: 'Phone', group: 'field' },
  { key: 'businessName', label: 'Business name', group: 'field' },
  { key: 'taxCode', label: 'Tax code', group: 'field' },
  { key: 'businessAddress', label: 'Business address', group: 'field' },
  { key: 'legalRepresentativeName', label: 'Legal representative', group: 'field' },
  { key: 'legalRepresentativePosition', label: 'Representative position', group: 'field' },
  { key: 'lotCount', label: 'Lot count', group: 'field' },
  { key: 'primaryLotAddress', label: 'Primary lot address', group: 'field' },
  { key: 'operatingHours', label: 'Operating hours', group: 'field' },
  { key: 'estimatedSlots', label: 'Estimated capacity', group: 'field' },
  { key: 'doc:businessLicense', label: 'Business registration', group: 'document' },
  { key: 'doc:lotLegalProof', label: 'Lot legal proof', group: 'document' },
  { key: 'doc:idCardFront', label: 'ID card – front', group: 'document' },
  { key: 'doc:idCardBack', label: 'ID card – back', group: 'document' },
  { key: 'doc:other', label: 'Other documents', group: 'document' },
];

/** POST …/{id}/edit-link (T3): link bổ sung hồ sơ một lần cho đối tác – token chỉ trả về lần này. */
export interface PartnerEditLinkDto {
  code: string;
  token: string;
  expiresAtUtc: string;
}

/** AdminService PartnerBusinessType. */
export type PartnerBusinessType = 'Company' | 'HouseholdBusiness';

/**
 * POST /api/v1/admin/partner-applications – Admin nhập hồ sơ thay đối tác (US-116 T1, nhận qua điện thoại).
 * Cùng trường với form public; consentAccepted = Admin xác nhận đối tác đã đồng ý xử lý dữ liệu; intakeNote ghi vào audit log.
 * Server: 201 + PartnerApplicationDetailDto; 400 dữ liệu sai; 409 trùng email / SĐT / MST với hồ sơ đang xử lý (kèm mã hồ sơ đó).
 */
export interface AdminCreatePartnerApplicationRequest {
  fullName: string;
  email: string;
  phone: string;
  position: string | null;
  businessType: PartnerBusinessType;
  businessName: string;
  taxCode: string;
  businessAddress: string;
  legalRepresentativeName: string;
  legalRepresentativePosition: string | null;
  lotCount: number;
  primaryLotAddress: string;
  estimatedCarSlots: number | null;
  estimatedMotorbikeSlots: number | null;
  operatingHours: string | null;
  consentAccepted: boolean;
  intakeNote: string | null;
}

/** A1: GET /api/v1/admin/partner-applications?status=&search=&page=&pageSize= (search: mã, tên DN, MST, email, SĐT). */
export interface PartnerApplicationListQuery {
  status?: PartnerApplicationStatus;
  search?: string;
  page?: number;
  pageSize?: number;
}

/** A4 request-info – ghi chú ≤ 1000 ký tự; items = khóa mục cần bổ sung; dueAtUtc mặc định +7 ngày (23:59 ICT). */
export interface PartnerRequestInfoRequest {
  note: string;
  items?: string[];
  dueAtUtc?: string | null;
  rowVersion?: string | null;
}

/** A11 reject / A12 cancel – ≤ 1000 ký tự. confirmCode = mã hồ sơ gõ lại, bắt buộc khi hủy hồ sơ đã cấp tài khoản. */
export interface PartnerReasonRequest {
  reason: string;
  rowVersion?: string | null;
  confirmCode?: string | null;
}

export interface PartnerBankAccountRequest {
  /** 6 chữ số. */
  bin: string;
  name: string;
  /** 6–30 chữ số. */
  accountNumber: string;
  accountName: string;
}

/** A9 issue-account: tài khoản nhận thanh toán mặc định của đối tác. */
export interface IssueOwnerAccountRequest {
  bank: PartnerBankAccountRequest;
}

/** US-053: giấy tờ pháp lý đối tác nộp kèm hồ sơ (GET …/partner-applications/{id}/documents, chỉ Admin). */
export interface PartnerDocumentDto {
  id: number;
  documentType: 'BusinessLicense' | 'RepresentativeId' | 'LotLegalProof' | 'Other' | string;
  originalFileName: string;
  contentType: string;
  sizeBytes: number;
  uploadedAtUtc: string;
}

/** File trả về từ API (HTML hợp đồng). */
export interface AdminFile {
  blob: Blob;
  fileName: string | null;
}
