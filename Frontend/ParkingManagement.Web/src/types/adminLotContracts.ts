import type { LotContractStatus, LotContractVersionStatus } from './adminEnums';
import type { PartnerBankAccountDto } from './adminPartnerOnboarding';

/**
 * Hợp đồng theo từng bãi đỗ (US-116, mục 14) – AdminService, khớp
 * Services/AdminService/AdminService.Application/Features/LotContracts/*.cs (L0–L4).
 * Lập sau khi KYB của bãi được duyệt; ký xong thì bãi chuyển Active. commissionRate theo %; ngày dạng "yyyy-MM-dd".
 */
export interface LotContractSummaryDto {
  id: number;
  contractNumber: string;
  status: LotContractStatus | string;
  parkingLotId: number;
  kybApplicationId: number;
  lotName: string;
  lotCity: string;
  ownerProfileId: number;
  businessName: string;
  latestVersion: number | null;
  commissionRate: number | null;
  effectiveFrom: string | null;
  effectiveTo: string | null;
  createdAtUtc: string;
  updatedAtUtc: string | null;
}

export interface LotContractVersionDto {
  id: number;
  version: number;
  templateVersion: number;
  commissionRate: number;
  effectiveFrom: string;
  effectiveTo: string;
  termMonths: number;
  fileContentType: string;
  fileSha256: string;
  status: LotContractVersionStatus | string;
  revisionReason: string | null;
  createdAtUtc: string;
  createdByAdminId: number;
}

/** Chi tiết: ảnh chụp bãi + Bên B lúc lập hợp đồng, trạng thái ký, các phiên bản (mới nhất trước). */
export interface LotContractDetailDto {
  id: number;
  contractNumber: string;
  status: LotContractStatus | string;
  parkingLotId: number;
  kybApplicationId: number;
  lotName: string;
  lotAddress: string;
  lotCity: string;
  lotDistrict: string | null;
  lotTotalSlots: number;
  lotOpeningHours: string;
  ownerProfileId: number;
  partnerApplicationId: number;
  applicationCode: string;
  businessName: string;
  taxCode: string;
  businessAddress: string;
  legalRepresentativeName: string;
  legalRepresentativePosition: string | null;
  email: string;
  phone: string;
  bank: PartnerBankAccountDto;
  signedFileContentType: string | null;
  signedFileSha256: string | null;
  signedAtUtc: string | null;
  cancelReason: string | null;
  createdByAdminId: number;
  createdAtUtc: string;
  updatedAtUtc: string | null;
  versions: LotContractVersionDto[];
}

/** L0: GET /api/v1/admin/lot-contracts?status=&parkingLotId=&ownerProfileId=&search=&page=&pageSize= */
export interface LotContractListQuery {
  status?: LotContractStatus;
  parkingLotId?: number;
  ownerProfileId?: number;
  search?: string;
  page?: number;
  pageSize?: number;
}

/** Điều khoản: commissionRate > 0, ≤ 100, 2 chữ số thập phân; effectiveFrom ≥ hôm nay; termMonths 12 / 24 / 36. */
export interface LotContractTermsRequest {
  commissionRate: number;
  effectiveFrom: string;
  termMonths: number;
}

/** L1 / L1p: lập hợp đồng cho hồ sơ KYB đã duyệt. */
export interface CreateLotContractRequest extends LotContractTermsRequest {
  kybApplicationId: number;
}

/** L2: điều khoản mới + lý do (≤ 1000 ký tự). */
export interface ReviseLotContractRequest extends LotContractTermsRequest {
  reason: string;
}

/** L3 (multipart): bản scan PDF / JPG / PNG ≤ 10 MB + phiên bản Admin xác nhận đã ký. */
export interface SignLotContractRequest {
  confirmedVersion: number;
  signedFile: File;
}

/** L4. */
export interface LotContractReasonRequest {
  reason: string;
}
