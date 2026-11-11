/* Fixture + render helper dùng chung cho test US-080 (không dùng trong ứng dụng). Mọi số trong test đến từ đây – component không có số mẫu. */
import { render } from '@testing-library/react';
import { MemoryRouter, Route, Routes, useLocation } from 'react-router-dom';
import type React from 'react';
import type {
  FinancialAdjustmentDto,
  OwnerPayoutAccountDto,
  SettlementDetailDto,
  SettlementStatsDto,
  SettlementSummaryDto,
} from '@/types/adminSettlements';
import { AdminToastProvider } from '../../../components/AdminToastProvider';

export const settlement = (patch: Partial<SettlementSummaryDto> = {}): SettlementSummaryDto => ({
  id: 5,
  code: 'STL-20261102-0001',
  ownerProfileId: 1,
  periodStart: '2026-11-02',
  periodEnd: '2026-11-08',
  cycle: 'WEEKLY',
  grossAmount: 150_000,
  refundAmount: 0,
  ownerCollectedAmount: 50_000,
  commissionAmount: 15_000,
  commissionRate: 0.1,
  heldAmount: 0,
  adjustmentAmount: 0,
  carryOverAmount: 0,
  netPayout: 85_000,
  status: 'Draft',
  isBlocked: false,
  blockedReasons: [],
  generatedAtUtc: '2026-11-10T03:00:00Z',
  submittedAtUtc: null,
  approvedAtUtc: null,
  paidAtUtc: null,
  payoutReference: null,
  rowVersion: 'rv1',
  ...patch,
});

export const detail = (s: SettlementSummaryDto, allowedActions: SettlementDetailDto['allowedActions']): SettlementDetailDto => ({
  settlement: s,
  formula: {
    gross: s.grossAmount, refund: s.refundAmount, ownerCollected: s.ownerCollectedAmount, commission: s.commissionAmount, held: s.heldAmount,
    adjustment: s.adjustmentAmount, carryOver: s.carryOverAmount, netPayout: s.netPayout,
    expression: 'NetPayout = Gross − Refund − OwnerCollected − Commission − Held + Adjustment + CarryOver',
  },
  timeline: [{ event: 'Generated', atUtc: s.generatedAtUtc, byUserId: null, note: null }],
  lineTotals: [{ lineType: 'Booking', count: 1, amount: 100_000, commissionAmount: 10_000 }],
  adjustments: [],
  allowedActions,
});

export const stats = (patch: Partial<SettlementStatsDto> = {}): SettlementStatsDto => ({
  draft: 3, pendingReview: 1, approved: 2, paid: 4, blocked: 1, pendingReviewNetPayout: 90_000, awaitingPayoutNetPayout: 120_000, ...patch,
});

export const account = (ownerProfileId: number, ownerName: string): OwnerPayoutAccountDto => ({
  ownerProfileId, ownerName, partnerApplicationId: 40 + ownerProfileId, partnerApplicationCode: `PA-2026-00${ownerProfileId}`, bankBin: '970422',
  bankName: 'MB Bank', accountName: 'CONG TY BAI XANH', accountNumberMasked: '****6789', source: 'PartnerApplication',
});

export const adjustment = (patch: Partial<FinancialAdjustmentDto> = {}): FinancialAdjustmentDto => ({
  id: 11, ownerProfileId: 1, settlementId: null, settlementCode: null, amount: -150_000, reason: 'Chargeback BK-0002', status: 'PendingApproval',
  sourceType: 'Manual', sourceReference: null, reversesAdjustmentId: null, createdByUserId: 7, createdAtUtc: '2026-11-10T02:00:00Z',
  approvedByUserId: null, approvedAtUtc: null, rejectedByUserId: null, rejectedAtUtc: null, rejectReason: null, rowVersion: 'arv1', ...patch,
});

export const page = <T,>(items: T[]) => ({ items, page: 1, pageSize: 20, totalCount: items.length, totalPages: 1 });

// Component chỉ dùng trong test (không có fast refresh).
// oxlint-disable-next-line react/only-export-components
const LocationProbe = () => {
  const location = useLocation();
  return <output data-testid="location">{location.pathname + location.search}</output>;
};

/** Render một trang admin tại URL, có toast provider (như AdminLayout) và probe để đọc URL hiện tại. */
export const renderPage = (path: string, url: string, element: React.ReactNode) =>
  render(
    <AdminToastProvider>
      <MemoryRouter initialEntries={[url]}>
        <Routes>
          <Route
            path={path}
            element={
              <>
                {element}
                <LocationProbe />
              </>
            }
          />
        </Routes>
      </MemoryRouter>
    </AdminToastProvider>
  );
