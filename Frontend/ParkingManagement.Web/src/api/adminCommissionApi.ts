import { adminRequest, cleanParams } from './adminClientApi';
import type {
  AdminCommissionSummaryDto,
  CommissionLotRateDto,
  CommissionRateListDto,
  CommissionRateListQuery,
} from '@/types/adminCommission';

/** US-079: tỷ lệ hoa hồng theo bãi (AdminService) + tổng hợp hoa hồng toàn sàn (PaymentService) – qua Gateway, chỉ role Admin. */
const RATES = '/api/v1/admin/commission-rates';
const SUMMARY = '/api/v1/payments/admin/commission-summary';

/** pageSize tối đa của AdminService. */
export const COMMISSION_RATES_MAX_PAGE_SIZE = 100;
/** Chốt chặn số trang khi đọc toàn bộ danh sách tỷ lệ (100 × 50 = 5.000 bãi). */
const MAX_RATE_PAGES = 50;

export const adminCommissionApi = {
  /** Một trang tỷ lệ hiện tại của các bãi (lọc tên bãi / chủ bãi / số hợp đồng, nguồn tỷ lệ). */
  rates: (query: CommissionRateListQuery, signal?: AbortSignal) =>
    adminRequest<CommissionRateListDto>({
      method: 'GET',
      url: RATES,
      params: cleanParams({ search: query.search, source: query.source, page: query.page, pageSize: query.pageSize }),
      signal,
    }),

  /**
   * Toàn bộ tỷ lệ khớp bộ lọc (đọc lần lượt các trang 100 bãi) để ghép với số hoa hồng theo parkingLotId.
   * Thông tin chung (tỷ lệ mặc định, partial…) lấy từ trang đầu; partial = true nếu bất kỳ trang nào thiếu ParkingService.
   */
  allRates: async (query: Omit<CommissionRateListQuery, 'page' | 'pageSize'>, signal?: AbortSignal): Promise<CommissionRateListDto> => {
    const first = await adminCommissionApi.rates({ ...query, page: 1, pageSize: COMMISSION_RATES_MAX_PAGE_SIZE }, signal);
    const items: CommissionLotRateDto[] = [...first.lots.items];
    let partial = first.partial;
    const pages = Math.min(MAX_RATE_PAGES, Math.ceil(first.lots.totalCount / COMMISSION_RATES_MAX_PAGE_SIZE));
    for (let page = 2; page <= pages; page++) {
      const next = await adminCommissionApi.rates({ ...query, page, pageSize: COMMISSION_RATES_MAX_PAGE_SIZE }, signal);
      items.push(...next.lots.items);
      partial ||= next.partial;
    }
    return {
      ...first,
      partial,
      lots: { ...first.lots, items, page: 1, pageSize: items.length, totalPages: 1 },
    };
  },

  /** Tổng hợp hoa hồng trong [fromUtc, toUtc) theo PaidAtUtc của giao dịch gốc (tối đa 366 ngày). */
  summary: (fromUtc: string, toUtc: string, signal?: AbortSignal) =>
    adminRequest<AdminCommissionSummaryDto>({ method: 'GET', url: SUMMARY, params: { fromUtc, toUtc }, signal }),
};
