import { apiClient } from './client';

export interface OwnerRevenueTotalsDto {
  grossRevenue: number;
  refundAmount: number;
  netRevenue: number;
  transactionCount: number;
}

export interface OwnerRevenueByLotDto {
  parkingLotId: number;
  grossRevenue: number;
  refundAmount: number;
  netRevenue: number;
  transactionCount: number;
}

export interface OwnerRevenueByKeyDto {
  key: string;
  grossRevenue: number;
  refundAmount: number;
  netRevenue: number;
  transactionCount: number;
}

export interface OwnerRevenueByHourDto {
  hour: number;
  grossRevenue: number;
  netRevenue: number;
  transactionCount: number;
}

export interface OwnerRefundByReasonDto {
  reason: string;
  refundCount: number;
  amount: number;
}

export interface OwnerSettlementSummaryDto {
  settlementStatus: string;
  settledPeriodCount: number;
  settledFrom?: string | null;
  settledTo?: string | null;
  commissionAmount?: number | null;
  payoutAmount?: number | null;
}

export interface OwnerCommissionByLotDto {
  parkingLotId: number;
  commissionAmount: number;
  pendingCount: number;
  status: string;
}

export interface OwnerCommissionSummaryDto {
  status: string;
  commissionAmount: number;
  pendingCount: number;
  byLot: OwnerCommissionByLotDto[];
}

export interface OwnerRevenueSummaryDto {
  from: string;
  to: string;
  parkingLotId?: number | null;
  currency: string;
  refundBasis: string;
  totals: OwnerRevenueTotalsDto;
  byLot: OwnerRevenueByLotDto[];
  byMethod: OwnerRevenueByKeyDto[];
  byPurpose: OwnerRevenueByKeyDto[];
  byHourOfDay: OwnerRevenueByHourDto[];
  byRefundReason: OwnerRefundByReasonDto[];
  settlement: OwnerSettlementSummaryDto;
  commission: OwnerCommissionSummaryDto;
}

export interface OwnerRevenuePointDto {
  periodStart?: string;
  periodEnd?: string;
  bucket?: string;
  grossRevenue: number;
  refundAmount: number;
  netRevenue: number;
  transactionCount: number;
  commissionAmount?: number;
  commissionPendingCount?: number;
}

export interface OwnerRevenueSeriesDto {
  from: string;
  to: string;
  parkingLotId?: number | null;
  granularity: 'Hour' | 'Day' | 'Week' | 'Month' | string;
  currency: string;
  refundBasis: string;
  points: OwnerRevenuePointDto[];
}

export interface OwnerRevenueTransactionDto {
  id: number;
  code: string;
  paidAtUtc: string;
  parkingLotId: number;
  bookingCode?: string | null;
  parkingSessionId?: number | null;
  method: string;
  purpose: string;
  status: string;
  amount: number;
  refundedAmount: number;
  netAmount: number;
  commissionAmount?: number | null;
  commissionRate?: number | null;
}

export interface PagedResult<T> {
  items: T[];
  page: number;
  pageSize: number;
  totalCount: number;
}

export interface OwnerSettlementDto {
  id: number;
  code: string;
  periodStart: string;
  periodEnd: string;
  grossAmount: number;
  refundAmount: number;
  ownerCollectedAmount: number;
  commissionAmount: number;
  heldAmount: number;
  adjustmentAmount: number;
  carryOverAmount: number;
  netPayout: number;
  status: string;
  approvedAtUtc?: string | null;
  paidAtUtc?: string | null;
  payoutReference?: string | null;
}

export interface SettlementLineTotalDto {
  lineType: string;
  lineCount: number;
  grossAmount: number;
  refundAmount: number;
  commissionAmount: number;
}

export interface OwnerAdjustmentDto {
  id: number;
  amount: number;
  reason: string;
  sourceType: string;
  createdAtUtc: string;
}

export interface SettlementFormulaDto {
  formulaExpression: string;
  explanation: string;
}

export interface OwnerSettlementDetailDto {
  settlement: OwnerSettlementDto;
  formula: SettlementFormulaDto;
  lineTotals: SettlementLineTotalDto[];
  adjustments: OwnerAdjustmentDto[];
}

export interface SettlementLineDto {
  id: number;
  lineType: string;
  referenceCode: string;
  occurredAtUtc: string;
  grossAmount: number;
  commissionRate: number;
  commissionAmount: number;
  netAmount: number;
}

/**
 * Service API báo cáo doanh thu & quyết toán cho Chủ bãi (UI-25 / US-078, US-079, US-080).
 * Backend: PaymentService / OwnerRevenueReportsController & OwnerSettlementsController
 */
export const ownerRevenueApi = {
  /**
   * Lấy tổng quan doanh thu (US-078).
   */
  getRevenueSummary: async (params?: {
    from?: string;
    to?: string;
    parkingLotId?: number;
  }): Promise<OwnerRevenueSummaryDto> => {
    const response = await apiClient.get<OwnerRevenueSummaryDto>(
      '/api/v1/payments/reports/owner-revenue/summary',
      { params }
    );
    return response.data;
  },

  /**
   * Lấy chuỗi dữ liệu biểu đồ doanh thu theo giờ/ngày/tuần/tháng (US-078).
   */
  getRevenueSeries: async (params?: {
    from?: string;
    to?: string;
    parkingLotId?: number;
    granularity?: 'Hour' | 'Day' | 'Week' | 'Month';
  }): Promise<OwnerRevenueSeriesDto> => {
    const response = await apiClient.get<OwnerRevenueSeriesDto>(
      '/api/v1/payments/reports/owner-revenue/series',
      { params }
    );
    return response.data;
  },

  /**
   * Danh sách chi tiết các giao dịch phát sinh doanh thu (US-078).
   */
  getRevenueTransactions: async (params?: {
    from?: string;
    to?: string;
    parkingLotId?: number;
    method?: string;
    purpose?: string;
    status?: string;
    page?: number;
    pageSize?: number;
  }): Promise<PagedResult<OwnerRevenueTransactionDto>> => {
    const response = await apiClient.get<PagedResult<OwnerRevenueTransactionDto>>(
      '/api/v1/payments/reports/owner-revenue/transactions',
      { params }
    );
    return response.data;
  },

  /**
   * Tải file CSV báo cáo doanh thu.
   */
  exportRevenueCsv: async (params?: {
    from?: string;
    to?: string;
    parkingLotId?: number;
    method?: string;
    purpose?: string;
    status?: string;
  }): Promise<Blob> => {
    const response = await apiClient.get(
      '/api/v1/payments/reports/owner-revenue/export',
      {
        params,
        responseType: 'blob',
      }
    );
    return response.data;
  },

  /**
   * Danh sách các kỳ quyết toán đã chốt (US-080).
   */
  getSettlements: async (params?: {
    page?: number;
    pageSize?: number;
  }): Promise<PagedResult<OwnerSettlementDto>> => {
    const response = await apiClient.get<PagedResult<OwnerSettlementDto>>(
      '/api/v1/payments/owner/settlements',
      { params }
    );
    return response.data;
  },

  /**
   * Chi tiết một kỳ quyết toán (US-080).
   */
  getSettlementDetail: async (id: number): Promise<OwnerSettlementDetailDto> => {
    const response = await apiClient.get<OwnerSettlementDetailDto>(
      `/api/v1/payments/owner/settlements/${id}`
    );
    return response.data;
  },

  /**
   * Các dòng giao dịch chi tiết trong kỳ quyết toán (US-080).
   */
  getSettlementLines: async (
    id: number,
    params?: { lineType?: string; page?: number; pageSize?: number }
  ): Promise<PagedResult<SettlementLineDto>> => {
    const response = await apiClient.get<PagedResult<SettlementLineDto>>(
      `/api/v1/payments/owner/settlements/${id}/lines`,
      { params }
    );
    return response.data;
  },

  /**
   * Tải file CSV đối soát của một kỳ quyết toán.
   */
  exportSettlementCsv: async (id: number): Promise<Blob> => {
    const response = await apiClient.get(
      `/api/v1/payments/owner/settlements/${id}/export`,
      {
        responseType: 'blob',
      }
    );
    return response.data;
  },
};
