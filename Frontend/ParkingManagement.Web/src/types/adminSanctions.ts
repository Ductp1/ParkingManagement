import type { SanctionLevel, SanctionStatus, SanctionSyncStatus } from './adminEnums';

/**
 * DTO AdminService – Sanctions (US-097, chế tài SLA 4 cấp).
 * Nguồn: Services/AdminService/AdminService.Application/Features/Sanctions/SanctionContracts.cs
 * status là trạng thái HIỆU LỰC: Active đã quá endsAtUtc trả về "Expired" dù job chưa đóng.
 */
export interface SanctionSummaryDto {
  id: number;
  ownerProfileId: number;
  ownerUserId: number | null;
  /** null = toàn chủ bãi. */
  parkingLotId: number | null;
  level: SanctionLevel | string;
  reason: string;
  startsAtUtc: string;
  /** null = không thời hạn (cấp 4). Cảnh cáo: trùng hạn khắc phục. */
  endsAtUtc: string | null;
  correctionDueAtUtc: string | null;
  appealDeadlineUtc: string | null;
  status: SanctionStatus | string;
  penaltyAmount: number | null;
  issuedByUserId: number;
  revokedAtUtc: string | null;
  userServiceSyncStatus: SanctionSyncStatus | string;
  parkingSyncStatus: SanctionSyncStatus | string;
  notificationSyncStatus: SanctionSyncStatus | string;
  createdAtUtc: string;
  updatedAtUtc: string | null;
}

export interface SanctionTimelineEntryDto {
  auditLogId: number;
  atUtc: string;
  action: string;
  userId: number | null;
  reason: string | null;
}

export interface SanctionDetailDto {
  sanction: SanctionSummaryDto;
  evidenceJson: string | null;
  revokedByUserId: number | null;
  revokeReason: string | null;
  userServiceSyncedAtUtc: string | null;
  parkingSyncedAtUtc: string | null;
  notificationSentAtUtc: string | null;
  riskFlagIds: number[];
  timeline: SanctionTimelineEntryDto[];
}

/** GET /api/v1/admin/sanctions/stats – đếm chế tài đang hiệu lực theo cấp, số đồng bộ lỗi / chờ, sắp hết hạn trong 7 ngày. */
export interface SanctionStatsDto {
  activeTotal: number;
  warning: number;
  rankingPenalty: number;
  temporarySuspension: number;
  permanentBan: number;
  syncFailed: number;
  syncPending: number;
  endingWithin7Days: number;
}

export interface SanctionSuggestionDto {
  ownerProfileId: number;
  suggestedLevel: SanctionLevel | string;
  windowDays: number;
  rationale: string;
  basis: SanctionSummaryDto[];
}

export interface IssueSanctionResultDto {
  sanctions: SanctionSummaryDto[];
}

/** POST /api/v1/admin/sanctions – người áp lấy từ JWT. parkingLotIds rỗng = toàn chủ bãi. Thời điểm là ISO UTC. */
export interface IssueSanctionRequest {
  ownerProfileId: number;
  ownerUserId: number;
  level: SanctionLevel;
  parkingLotIds: number[];
  reason: string;
  evidenceJson?: string | null;
  endsAtUtc?: string | null;
  correctionDueAtUtc?: string | null;
  penaltyAmount?: number | null;
  riskFlagId?: number | null;
}

export interface RevokeSanctionRequest {
  reason: string;
}

/** Bộ lọc của GET /api/v1/admin/sanctions (from / to là ISO UTC, to không bao gồm). */
export interface SanctionListQuery {
  status?: SanctionStatus | string;
  level?: SanctionLevel | string;
  ownerProfileId?: number | null;
  parkingLotId?: number | null;
  search?: string;
  from?: string | null;
  to?: string | null;
  syncFailed?: boolean;
}

export interface SanctionStateEntryDto {
  sanctionId: number;
  ownerProfileId: number;
  parkingLotId: number | null;
  kind: 'RankingPenalty' | 'ListingSuspension' | string;
  startsAtUtc: string;
  endsAtUtc: string | null;
  liftedAtUtc: string | null;
  isInEffect: boolean;
}

/**
 * ParkingService (US-097, Admin) – GET /api/v1/parking-lots/{lotId}/sanction-state: hiệu lực đang áp lên bãi
 * (theo bãi + theo chủ bãi) và chủ bãi hiện tại của bãi.
 */
export interface LotSanctionStateDto {
  parkingLotId: number;
  ownerProfileId: number;
  isListingSuspended: boolean;
  listingSuspendedUntilUtc: string | null;
  isRankingPenalized: boolean;
  rankingPenaltyUntilUtc: string | null;
  activeStates: SanctionStateEntryDto[];
}
