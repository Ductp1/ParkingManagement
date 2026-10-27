/**
 * DTO AdminService – Admin Dashboard overview (US-081). Nguồn: Services/AdminService/AdminService.Application/Features/Dashboard.
 * Mỗi widget: available (value + asOfUtc) hoặc unavailable (reason no_source / source_error). "0 thật" = available với số 0.
 */
export type DashboardWidgetStatus = 'available' | 'unavailable';
export type DashboardUnavailableReason = 'no_source' | 'source_error';

export interface DashboardWidget<T> {
  status: DashboardWidgetStatus;
  value: T | null;
  asOfUtc: string | null;
  reason: DashboardUnavailableReason | null;
  /** Service sở hữu số liệu (AdminService, ParkingService, PaymentService…). */
  source: string;
  /** Lý do chi tiết (tiếng Việt) khi unavailable. */
  detail: string | null;
}

export type NoValue = Record<string, never>;

export interface KybQueueDto {
  submitted: number;
  underReview: number;
  needsMoreInfo: number;
  toReview: number;
}

export interface PartnerQueueDto {
  submitted: number;
  underReview: number;
  needsMoreInfo: number;
  accountIssued: number;
  toReview: number;
}

export interface LotContractsQueueDto {
  pending: number;
}

export interface RiskFlagsDto {
  open: number;
  investigating: number;
  critical: number;
  warning: number;
  info: number;
  overdue: number;
  unresolved: number;
}

export interface ActiveSanctionsDto {
  total: number;
  warning: number;
  rankingPenalty: number;
  temporarySuspension: number;
  permanentBan: number;
}

export interface ScheduledConfigChangesDto {
  count: number;
  nextKey: string | null;
  nextEffectiveFromUtc: string | null;
}

export interface AuditTodayDto {
  total: number;
  bySource: Array<{ sourceService: string; count: number }>;
}

export interface DashboardOverviewDto {
  generatedAtUtc: string;
  timeZone: string;
  todayFromUtc: string;
  todayToUtc: string;
  kpis: {
    revenueToday: DashboardWidget<NoValue>;
    activeSessions: DashboardWidget<NoValue>;
    bookingsToday: DashboardWidget<NoValue>;
    openDisputes: DashboardWidget<NoValue>;
  };
  queues: {
    kybApplications: DashboardWidget<KybQueueDto>;
    partnerApplications: DashboardWidget<PartnerQueueDto>;
    lotContractsToSign: DashboardWidget<LotContractsQueueDto>;
    riskFlags: DashboardWidget<RiskFlagsDto>;
    refundsRequested: DashboardWidget<NoValue>;
    settlementsPendingReview: DashboardWidget<NoValue>;
    disputesAwaitingOwner: DashboardWidget<NoValue>;
    dataSubjectRequestsDue: DashboardWidget<NoValue>;
  };
  governance: {
    activeSanctions: DashboardWidget<ActiveSanctionsDto>;
    scheduledConfigChanges: DashboardWidget<ScheduledConfigChangesDto>;
    auditLogsToday: DashboardWidget<AuditTodayDto>;
  };
  tables: {
    lotOccupancy: DashboardWidget<NoValue>;
    recentPayments: DashboardWidget<NoValue>;
    gateDevices: DashboardWidget<NoValue>;
  };
}
