import { adminFileRequest, adminRequest, cleanParams } from './adminClientApi';
import type { AdminFile } from '@/types/adminPartnerOnboarding';
import type {
  AdjustmentListQuery,
  AdjustmentPage,
  CreateAdjustmentRequest,
  FinancialAdjustmentDto,
  OwnerPayoutAccountListDto,
  OwnerSettlementOutcomeDto,
  SettlementDetailDto,
  SettlementGenerationResultDto,
  SettlementLinePage,
  SettlementLineType,
  SettlementListQuery,
  SettlementPage,
  SettlementPeriodDto,
  SettlementStatsDto,
  SettlementSummaryDto,
} from '@/types/adminSettlements';

/** US-080: kỳ quyết toán + bút toán điều chỉnh (PaymentService) và tài khoản nhận tiền (AdminService) – qua Gateway, chỉ role Admin. */
const SETTLEMENTS = '/api/v1/payments/admin/settlements';
const ADJUSTMENTS = '/api/v1/payments/admin/adjustments';
const OWNERS = '/api/v1/admin/owners';

/** Giới hạn của AdminService cho một lần tra tài khoản nhận tiền. */
export const PAYOUT_ACCOUNTS_MAX_OWNERS = 100;

const listParams = (q: SettlementListQuery) =>
  cleanParams({ status: q.status, ownerProfileId: q.ownerProfileId, periodStart: q.periodStart, search: q.search, blocked: q.blocked });

export const adminSettlementsApi = {
  list: (query: SettlementListQuery, page: number, pageSize: number) =>
    adminRequest<SettlementPage>({ method: 'GET', url: SETTLEMENTS, params: { ...listParams(query), page, pageSize } }),

  stats: (periodStart: string | null) =>
    adminRequest<SettlementStatsDto>({ method: 'GET', url: `${SETTLEMENTS}/stats`, params: cleanParams({ periodStart }) }),

  periods: (take = 26) => adminRequest<SettlementPeriodDto[]>({ method: 'GET', url: `${SETTLEMENTS}/periods`, params: { take } }),

  get: (id: number) => adminRequest<SettlementDetailDto>({ method: 'GET', url: `${SETTLEMENTS}/${id}` }),

  lines: (id: number, lineType: SettlementLineType | null, page: number, pageSize: number) =>
    adminRequest<SettlementLinePage>({ method: 'GET', url: `${SETTLEMENTS}/${id}/lines`, params: cleanParams({ lineType, page, pageSize }) }),

  exportList: (query: SettlementListQuery): Promise<AdminFile> =>
    adminFileRequest({ method: 'GET', url: `${SETTLEMENTS}/export`, params: listParams(query) }),

  exportLines: (id: number): Promise<AdminFile> => adminFileRequest({ method: 'GET', url: `${SETTLEMENTS}/${id}/lines/export` }),

  /** Sinh kỳ (hoặc tính lại Draft) – periodStart null = kỳ gần nhất đã kết thúc; ownerProfileId null = mọi chủ bãi có phát sinh. */
  generate: (periodStart: string | null, ownerProfileId: number | null) =>
    adminRequest<SettlementGenerationResultDto>({ method: 'POST', url: `${SETTLEMENTS}/generate`, data: { periodStart, ownerProfileId } }),

  regenerate: (id: number, rowVersion: string) =>
    adminRequest<OwnerSettlementOutcomeDto>({ method: 'POST', url: `${SETTLEMENTS}/${id}/regenerate`, data: { rowVersion } }),

  submit: (id: number, rowVersion: string) =>
    adminRequest<SettlementSummaryDto>({ method: 'POST', url: `${SETTLEMENTS}/${id}/submit`, data: { rowVersion } }),

  approve: (id: number, rowVersion: string) =>
    adminRequest<SettlementSummaryDto>({ method: 'POST', url: `${SETTLEMENTS}/${id}/approve`, data: { rowVersion } }),

  reject: (id: number, rowVersion: string, reason: string) =>
    adminRequest<SettlementSummaryDto>({ method: 'POST', url: `${SETTLEMENTS}/${id}/reject`, data: { rowVersion, reason } }),

  markPaid: (id: number, rowVersion: string, payoutReference: string, paidAtUtc: string) =>
    adminRequest<SettlementSummaryDto>({ method: 'POST', url: `${SETTLEMENTS}/${id}/mark-paid`, data: { rowVersion, payoutReference, paidAtUtc } }),

  adjustments: (query: AdjustmentListQuery, page: number, pageSize: number) =>
    adminRequest<AdjustmentPage>({
      method: 'GET',
      url: ADJUSTMENTS,
      params: { ...cleanParams({ ownerProfileId: query.ownerProfileId, status: query.status, attached: query.attached, search: query.search }), page, pageSize },
    }),

  createAdjustment: (data: CreateAdjustmentRequest) => adminRequest<FinancialAdjustmentDto>({ method: 'POST', url: ADJUSTMENTS, data }),

  approveAdjustment: (id: number, rowVersion: string) =>
    adminRequest<FinancialAdjustmentDto>({ method: 'POST', url: `${ADJUSTMENTS}/${id}/approve`, data: { rowVersion } }),

  rejectAdjustment: (id: number, rowVersion: string, reason: string) =>
    adminRequest<FinancialAdjustmentDto>({ method: 'POST', url: `${ADJUSTMENTS}/${id}/reject`, data: { rowVersion, reason } }),

  /** Tên chủ bãi + tài khoản nhận tiền (đã che) của nhiều chủ bãi một lần (AdminService, tối đa 100). */
  payoutAccounts: (ownerProfileIds: number[]) =>
    adminRequest<OwnerPayoutAccountListDto>({
      method: 'GET',
      url: `${OWNERS}/payout-accounts`,
      params: { ownerProfileIds: ownerProfileIds.slice(0, PAYOUT_ACCOUNTS_MAX_OWNERS).join(',') },
    }),
};
