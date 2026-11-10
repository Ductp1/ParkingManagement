import type { PagedResult } from './adminCommon';

/**
 * US-079 – hoa hồng nền tảng. Khớp 1-1 DTO của:
 *  - AdminService  GET /api/v1/admin/commission-rates            (CommissionRateListDto)
 *  - PaymentService GET /api/v1/payments/admin/commission-summary (AdminCommissionSummaryDto)
 * Mọi tỷ lệ (rate / currentRate / effectiveRate) là PHÂN SỐ 0–1 (0.075 = 7,5%); tiền là VND không phần lẻ.
 */

export type CommissionRateSource = 'LotContract' | 'DefaultConfig';
export type CommissionRateReason = 'BeforeEffective' | 'AfterExpiry' | 'NoSignedContract';
export type CommissionStatus = 'Calculated' | 'Partial' | 'Pending';

// ===== AdminService: tỷ lệ hiện tại =====

export interface CommissionLotRateDto {
  parkingLotId: number;
  lotName: string;
  district: string | null;
  city: string | null;
  /** null khi ParkingService không trả về bãi này. */
  lotStatus: string | null;
  /** null khi bãi chưa có hợp đồng đã ký (lots-summary không có chủ bãi). */
  ownerProfileId: number | null;
  ownerName: string | null;
  currentRate: number;
  source: CommissionRateSource;
  reason: CommissionRateReason | null;
  contractId: number | null;
  contractNumber: string | null;
  contractVersion: number | null;
  /** YYYY-MM-DD (ngày Việt Nam). */
  effectiveFrom: string | null;
  effectiveTo: string | null;
}

export interface CommissionDefaultChangeDto {
  rate: number;
  effectiveFromUtc: string;
}

export interface CommissionRateListDto {
  asOfUtc: string;
  defaultRate: number;
  /** null = vẫn là giá trị gốc của tham số. */
  defaultEffectiveFromUtc: string | null;
  defaultNextChange: CommissionDefaultChangeDto | null;
  /** true = không đọc được ParkingService: chỉ còn các bãi có hợp đồng đã ký. */
  partial: boolean;
  partialReason: string | null;
  lots: PagedResult<CommissionLotRateDto>;
}

export interface CommissionRateListQuery {
  search?: string;
  source?: CommissionRateSource;
  page?: number;
  pageSize?: number;
}

// ===== PaymentService: tổng hợp hoa hồng =====

export interface AdminCommissionTotalsDto {
  grossAmount: number;
  refundedAmount: number;
  netAmount: number;
  commissionAmount: number;
  /** commission / net; null khi net = 0. */
  effectiveRate: number | null;
  transactionCount: number;
  /** > 0 = còn giao dịch chờ job tính: commissionAmount CHƯA đủ (không phải 0). */
  pendingCount: number;
  status: CommissionStatus;
}

export interface AdminCommissionByLotDto extends AdminCommissionTotalsDto {
  parkingLotId: number;
  ownerProfileId: number;
  rateSources: CommissionRateSource[];
}

export interface AdminCommissionByOwnerDto extends AdminCommissionTotalsDto {
  ownerProfileId: number;
  lotCount: number;
}

export interface AdminCommissionByRateSourceDto {
  source: CommissionRateSource;
  transactionCount: number;
  baseAmount: number;
  refundedAmount: number;
  commissionAmount: number;
  effectiveRate: number | null;
}

export interface AdminCommissionDayDto {
  /** YYYY-MM-DD (ngày Việt Nam). */
  date: string;
  grossAmount: number;
  netAmount: number;
  commissionAmount: number;
  transactionCount: number;
  pendingCount: number;
}

export interface AdminCommissionSummaryDto {
  asOfUtc: string;
  fromUtc: string;
  toUtc: string;
  currency: string;
  totals: AdminCommissionTotalsDto;
  byLot: AdminCommissionByLotDto[];
  byOwner: AdminCommissionByOwnerDto[];
  byRateSource: AdminCommissionByRateSourceDto[];
  series: AdminCommissionDayDto[];
}
