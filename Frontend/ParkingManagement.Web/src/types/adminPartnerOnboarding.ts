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
}

/** A1: GET /api/v1/admin/partner-applications?status=&search=&page=&pageSize= (search: mã, tên DN, MST, email, SĐT). */
export interface PartnerApplicationListQuery {
  status?: PartnerApplicationStatus;
  search?: string;
  page?: number;
  pageSize?: number;
}

/** A4 request-info – ≤ 1000 ký tự. */
export interface PartnerRequestInfoRequest {
  note: string;
}

/** A11 reject / A12 cancel – ≤ 1000 ký tự. */
export interface PartnerReasonRequest {
  reason: string;
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

/** File trả về từ API (HTML hợp đồng). */
export interface AdminFile {
  blob: Blob;
  fileName: string | null;
}
