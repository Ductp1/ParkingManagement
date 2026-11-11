/**
 * US-080: quyết toán định kỳ cho chủ bãi – khớp DTO của PaymentService (Features/Settlements) và AdminService
 * (OwnerPayoutAccountFeatures). JSON camelCase; DateOnly = "YYYY-MM-DD"; DateTime = ISO UTC; số tiền VND (number).
 */
import type { PagedResult } from './adminCommon';
import type { SettlementStatus } from './adminEnums';

export type { SettlementStatus };

export type SettlementLineType = 'Booking' | 'WalkIn' | 'Refund' | 'Adjustment' | 'DisputeHold';
export type FinancialAdjustmentStatus = 'PendingApproval' | 'Approved' | 'Rejected';
export type FinancialAdjustmentSource = 'Manual' | 'CompensationVoucher' | 'Sanction';
export type SettlementAction = 'Regenerate' | 'Submit' | 'Approve' | 'Reject' | 'MarkPaid';
export type SettlementBlockReason =
  | 'CommissionPending'
  | 'BookingCodeMissing'
  | 'BookingNotFound'
  | 'BookingUnverifiable'
  | 'HoldsNotLoaded';

export interface SettlementSummaryDto {
  id: number;
  code: string;
  ownerProfileId: number;
  periodStart: string;
  periodEnd: string;
  cycle: string | null;
  grossAmount: number;
  refundAmount: number;
  ownerCollectedAmount: number;
  commissionAmount: number;
  commissionRate: number;
  heldAmount: number;
  adjustmentAmount: number;
  carryOverAmount: number;
  netPayout: number;
  status: SettlementStatus;
  isBlocked: boolean;
  blockedReasons: string[];
  generatedAtUtc: string;
  submittedAtUtc: string | null;
  approvedAtUtc: string | null;
  paidAtUtc: string | null;
  payoutReference: string | null;
  rowVersion: string;
}

export interface SettlementFormulaDto {
  gross: number;
  refund: number;
  ownerCollected: number;
  commission: number;
  held: number;
  adjustment: number;
  carryOver: number;
  netPayout: number;
  expression: string;
}

export interface SettlementTimelineEventDto {
  event: 'Generated' | 'Submitted' | 'Rejected' | 'Approved' | 'Paid';
  atUtc: string;
  byUserId: number | null;
  note: string | null;
}

export interface SettlementLineTotalDto {
  lineType: SettlementLineType;
  count: number;
  amount: number;
  commissionAmount: number;
}

export interface SettlementLineDto {
  id: number;
  lineType: SettlementLineType;
  paymentId: number | null;
  refundId: number | null;
  parkingLotId: number;
  referenceCode: string | null;
  amount: number;
  commissionAmount: number;
  isOwnerCollected: boolean;
  occurredAtUtc: string | null;
  blockedReason: string | null;
}

export interface FinancialAdjustmentDto {
  id: number;
  ownerProfileId: number;
  settlementId: number | null;
  settlementCode: string | null;
  amount: number;
  reason: string;
  status: FinancialAdjustmentStatus;
  sourceType: FinancialAdjustmentSource;
  sourceReference: string | null;
  reversesAdjustmentId: number | null;
  createdByUserId: number;
  createdAtUtc: string;
  approvedByUserId: number | null;
  approvedAtUtc: string | null;
  rejectedByUserId: number | null;
  rejectedAtUtc: string | null;
  rejectReason: string | null;
  rowVersion: string;
}

export interface SettlementDetailDto {
  settlement: SettlementSummaryDto;
  formula: SettlementFormulaDto;
  timeline: SettlementTimelineEventDto[];
  lineTotals: SettlementLineTotalDto[];
  adjustments: FinancialAdjustmentDto[];
  allowedActions: SettlementAction[];
}

export interface SettlementStatsDto {
  draft: number;
  pendingReview: number;
  approved: number;
  paid: number;
  blocked: number;
  pendingReviewNetPayout: number;
  awaitingPayoutNetPayout: number;
}

export interface SettlementPeriodDto {
  id: number;
  periodStart: string;
  periodEnd: string;
  cycle: string;
  hasEnded: boolean;
  settlementCount: number;
}

/** Kết quả sinh kỳ cho một chủ bãi: outcome Created | Regenerated | Skipped (reason). */
export interface OwnerSettlementOutcomeDto {
  ownerProfileId: number;
  outcome: 'Created' | 'Regenerated' | 'Skipped';
  reason: string | null;
  settlementId: number | null;
  code: string | null;
  netPayout: number | null;
  isBlocked: boolean | null;
  blockedReasons: string[];
  rowVersion: string | null;
}

export interface SettlementGenerationResultDto {
  periodStart: string;
  periodEnd: string;
  cycle: string;
  owners: OwnerSettlementOutcomeDto[];
}

export interface SettlementListQuery {
  status?: SettlementStatus | null;
  ownerProfileId?: number | null;
  periodStart?: string | null;
  search?: string | null;
  blocked?: boolean | null;
}

export interface AdjustmentListQuery {
  ownerProfileId?: number | null;
  status?: FinancialAdjustmentStatus | null;
  attached?: boolean | null;
  search?: string | null;
}

export interface CreateAdjustmentRequest {
  ownerProfileId: number;
  amount: number;
  reason: string;
  sanctionId?: number | null;
  reversesAdjustmentId?: number | null;
}

// ----- AdminService: tài khoản nhận tiền (đã che 4 số cuối) + tên chủ bãi -----

export interface OwnerPayoutAccountDto {
  ownerProfileId: number;
  ownerName: string;
  partnerApplicationId: number;
  partnerApplicationCode: string;
  bankBin: string;
  bankName: string;
  accountName: string;
  accountNumberMasked: string;
  source: string;
}

export interface OwnerPayoutAccountListDto {
  items: OwnerPayoutAccountDto[];
  missingOwnerProfileIds: number[];
}

export type SettlementPage = PagedResult<SettlementSummaryDto>;
export type SettlementLinePage = PagedResult<SettlementLineDto>;
export type AdjustmentPage = PagedResult<FinancialAdjustmentDto>;
