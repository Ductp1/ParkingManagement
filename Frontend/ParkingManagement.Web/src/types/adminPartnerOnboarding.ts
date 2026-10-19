import type { PartnerApplicationStatus } from './adminEnums';

/**
 * Partner Onboarding – hợp đồng DỰ KIẾN (chưa có endpoint nào), theo
 * "api design/AdminService/Partner Onboarding - State Machine & Flow.md" mục 6.1 (A1–A12) và mục 7 (data model).
 */
export interface PartnerApplicationSummaryDto {
  id: number;
  code: string;
  businessName: string;
  taxCode: string;
  fullName: string;
  phone: string;
  email: string;
  lotCount: number;
  status: PartnerApplicationStatus | string;
  reviewerAdminId: number | null;
  updatedAtUtc: string | null;
  createdAtUtc: string;
}

export interface PartnerApplicationDetailDto extends PartnerApplicationSummaryDto {
  position: string | null;
  businessType: string;
  businessAddress: string;
  businessLicenseUrl: string | null;
  primaryLotAddress: string;
  estimatedCarSlots: number | null;
  estimatedMotorbikeSlots: number | null;
  operatingHours: string | null;
  infoRequestNote: string | null;
  rejectReason: string | null;
  cancelReason: string | null;
  contractNumber: string | null;
}

/** A1: GET /api/v1/admin/partner-applications?status=&search=&page=&pageSize= */
export interface PartnerApplicationListQuery {
  status?: PartnerApplicationStatus;
  search?: string;
  page?: number;
  pageSize?: number;
}

/** A4 request-info */
export interface PartnerRequestInfoRequest {
  note: string;
}

/** A11 reject / A12 cancel */
export interface PartnerReasonRequest {
  reason: string;
}
