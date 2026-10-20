import type { ContractDraftStatus, PartnerApplicationStatus } from './adminEnums';

/**
 * Partner Onboarding (US-116) – AdminService, khớp
 * Services/AdminService/AdminService.Application/Features/PartnerOnboarding/*.cs (A1–A12).
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
  contractNumber: string | null;
  createdAtUtc: string;
  updatedAtUtc: string | null;
}

/** Một phiên bản hợp đồng tự sinh (T5 / T5b). commissionRate theo %; ngày dạng "yyyy-MM-dd". */
export interface ContractDraftDto {
  id: number;
  version: number;
  contractNumber: string;
  templateVersion: number;
  commissionRate: number;
  effectiveFrom: string;
  effectiveTo: string;
  termMonths: number;
  bankBin: string;
  bankName: string;
  bankAccountNumber: string;
  bankAccountName: string;
  fileContentType: string;
  fileSha256: string;
  status: ContractDraftStatus | string;
  revisionReason: string | null;
  sentAtUtc: string;
  sentToEmail: string;
  createdByAdminId: number;
}

/**
 * Chi tiết hồ sơ (A2; mọi thao tác A3–A12 trả về cùng DTO này).
 * nextStatuses = trạng thái có thể chuyển tới theo bảng T2–T10; contractDrafts = mới nhất trước.
 */
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
  contractNumber: string | null;
  createdUserId: number | null;
  createdOwnerProfileId: number | null;
  createdAtUtc: string;
  updatedAtUtc: string | null;
  nextStatuses: Array<PartnerApplicationStatus | string>;
  contractDrafts: ContractDraftDto[];
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

/**
 * A5 contract-preview / A6 send-contract. commissionRate theo % (> 0, ≤ 100, 2 chữ số thập phân);
 * effectiveFrom "yyyy-MM-dd" ≥ hôm nay; termMonths 12 / 24 / 36.
 */
export interface PartnerContractTermsRequest {
  commissionRate: number;
  effectiveFrom: string;
  termMonths: number;
  bank: PartnerBankAccountRequest;
}

/** A7 revise-contract: điều khoản mới + lý do (≤ 1000 ký tự). */
export interface PartnerReviseContractRequest extends PartnerContractTermsRequest {
  reason: string;
}

/** A9 issue-account (multipart): bản scan PDF / JPG / PNG ≤ 10 MB + phiên bản Admin xác nhận đã ký. */
export interface IssueOwnerAccountRequest {
  confirmedDraftVersion: number;
  signedFile: File;
}

/** File trả về từ A5 / A8 (HTML hợp đồng). */
export interface AdminFile {
  blob: Blob;
  fileName: string | null;
}
