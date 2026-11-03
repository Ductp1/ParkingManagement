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
  /** Service sở hữu số liệu (AdminService, ParkingService, "GateService + ParkingService"…). */
  source: string;
  /** Lý do chi tiết (tiếng Việt) khi unavailable. */
  detail: string | null;
}

// ----- Hàng đợi / governance (pm_admin + ParkingService KYB) -----

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

// ----- Nguồn ngoài -----

/** PaymentService (contract-first): thanh toán thành công trong ngày ICT. */
export interface RevenueTodayDto {
  succeededCount: number;
  gross: number;
  refunded: number;
  net: number;
}

/** GateService: phiên đang hoạt động; abnormal đếm riêng, không nằm trong total. */
export interface ActiveSessionsDto {
  total: number;
  walkIn: number;
  booked: number;
  abnormal: number;
}

/** BookingService (contract-first): 3 nguyên nhân hủy = null khi nguồn chưa tách được. */
export interface BookingsTodayDto {
  created: number;
  confirmed: number;
  completed: number;
  cancelled: number;
  cancelledNoRefund: number;
  expired: number;
  rejected: number;
  noShow: number | null;
  cancelledByDriver: number | null;
  cancelledByLot: number | null;
}

/** SupportService: tranh chấp chưa đóng. */
export interface OpenDisputesDto {
  open: number;
  awaitingOwner: number;
  ownerResponded: number;
  escalated: number;
  unresolved: number;
}

export interface RefundsRequestedDto {
  requested: number;
}

export interface SettlementsPendingReviewDto {
  pendingReview: number;
}

/** overdue = chờ chủ bãi đã quá hạn 48 giờ (nằm trong awaitingOwner). */
export interface DisputesAwaitingOwnerDto {
  overdue: number;
  awaitingOwner: number;
}

/** UserService (contract-first): yêu cầu dữ liệu cá nhân theo DueAtUtc. */
export interface DataSubjectRequestsDto {
  open: number;
  dueSoon: number;
  overdue: number;
}

export type CapacitySource = 'gate' | 'parking';

export interface LotOccupancyRowDto {
  parkingLotId: number;
  name: string;
  city: string;
  district: string | null;
  lotStatus: string;
  integrationTier: string;
  activeSessions: number;
  capacity: number;
  /** gate = LotCapacityConfigs (GateService, US-066); parking = ParkingLots.TotalSlots (dự phòng). */
  capacitySource: CapacitySource;
  occupancyPercent: number;
  isEmergencyStopped: boolean;
  instantBookingPaused: boolean;
}

export interface DistrictOccupancyRowDto {
  city: string;
  district: string | null;
  lotCount: number;
  activeSessions: number;
  capacity: number;
  occupancyPercent: number;
}

/** GateService + ParkingService: theo bãi và theo quận, sắp % lấp đầy giảm dần. */
export interface LotOccupancyDto {
  totalActive: number;
  totalCapacity: number;
  occupancyPercent: number;
  lots: LotOccupancyRowDto[];
  byDistrict: DistrictOccupancyRowDto[];
}

export interface GateDeviceTypeRowDto {
  deviceType: string;
  online: number;
  offline: number;
  maintenance: number;
  total: number;
}

export interface GateDevicesDto {
  online: number;
  offline: number;
  maintenance: number;
  total: number;
  byType: GateDeviceTypeRowDto[];
}

/** Chỉ số tổng theo phương thức – không có giao dịch / khách cụ thể (US-081 AC2). */
export interface PaymentMethodRowDto {
  method: string;
  count: number;
  gross: number;
}

export interface PaymentsByMethodDto {
  methods: PaymentMethodRowDto[];
}

export interface DashboardOverviewDto {
  generatedAtUtc: string;
  timeZone: string;
  todayFromUtc: string;
  todayToUtc: string;
  kpis: {
    revenueToday: DashboardWidget<RevenueTodayDto>;
    activeSessions: DashboardWidget<ActiveSessionsDto>;
    bookingsToday: DashboardWidget<BookingsTodayDto>;
    openDisputes: DashboardWidget<OpenDisputesDto>;
  };
  queues: {
    kybApplications: DashboardWidget<KybQueueDto>;
    partnerApplications: DashboardWidget<PartnerQueueDto>;
    lotContractsToSign: DashboardWidget<LotContractsQueueDto>;
    riskFlags: DashboardWidget<RiskFlagsDto>;
    refundsRequested: DashboardWidget<RefundsRequestedDto>;
    settlementsPendingReview: DashboardWidget<SettlementsPendingReviewDto>;
    disputesAwaitingOwner: DashboardWidget<DisputesAwaitingOwnerDto>;
    dataSubjectRequestsDue: DashboardWidget<DataSubjectRequestsDto>;
  };
  governance: {
    activeSanctions: DashboardWidget<ActiveSanctionsDto>;
    scheduledConfigChanges: DashboardWidget<ScheduledConfigChangesDto>;
    auditLogsToday: DashboardWidget<AuditTodayDto>;
  };
  tables: {
    lotOccupancy: DashboardWidget<LotOccupancyDto>;
    gateDevices: DashboardWidget<GateDevicesDto>;
    paymentsByMethodToday: DashboardWidget<PaymentsByMethodDto>;
  };
}
