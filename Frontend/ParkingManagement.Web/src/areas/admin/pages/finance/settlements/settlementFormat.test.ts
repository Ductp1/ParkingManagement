import { describe, expect, it } from 'vitest';
import { AdminApiError } from '@/api/adminClientApi';
import { account } from './settlementTestFixtures';
import {
  actionError,
  bankLabel,
  blockReasonLabel,
  commissionLink,
  formatDay,
  formatPeriod,
  formatSignedVnd,
  isPayable,
  ownerLabel,
  ownerLink,
  periodOptionLabel,
} from './settlementFormat';

describe('settlementFormat (US-080)', () => {
  it('formats ICT dates without shifting the day', () => {
    expect(formatDay('2026-11-02')).toBe('02/11/2026');
    expect(formatPeriod('2026-11-02', '2026-11-08')).toBe('02/11/2026 – 08/11/2026');
    expect(formatDay(null)).toBe('—');
    expect(periodOptionLabel({ periodStart: '2026-11-09', periodEnd: '2026-11-15', cycle: 'WEEKLY', hasEnded: false })).toBe(
      '09/11/2026 – 15/11/2026 · weekly (running)'
    );
  });

  it('409 is reported as a data conflict, other errors keep the server message', () => {
    expect(actionError(new AdminApiError(409, 'đã thay đổi'))).toEqual({
      message: 'Data has changed since you opened it – the latest version has been reloaded. (đã thay đổi)',
      conflict: true,
    });
    expect(actionError(new AdminApiError(400, 'Lý do từ chối không được để trống.'))).toEqual({
      message: 'Lý do từ chối không được để trống.',
      conflict: false,
    });
  });

  it('owner, bank and links', () => {
    const a = account(1, 'Công ty Bãi Xanh');
    expect(ownerLabel(1, a)).toBe('Công ty Bãi Xanh');
    expect(ownerLabel(2, undefined)).toBe('OwnerProfile #2');
    expect(ownerLink(a)).toBe('/admin/operations/partner-onboarding/41');
    expect(ownerLink(undefined)).toBeNull();
    expect(bankLabel(a)).toBe('MB Bank · ****6789');
    expect(commissionLink({ periodStart: '2026-11-02', periodEnd: '2026-11-08' })).toBe(
      '/admin/finance/commission?period=custom&from=2026-11-02&to=2026-11-08'
    );
  });

  it('negative money puts the minus before the currency sign', () => {
    expect(formatSignedVnd(-150_000)).toBe('−₫150.000');
    expect(formatSignedVnd(85_000)).toBe('₫85.000');
  });

  it('payable only when net payout > 0; unknown block reason falls back to the code', () => {
    expect(isPayable({ netPayout: 1 })).toBe(true);
    expect(isPayable({ netPayout: 0 })).toBe(false);
    expect(blockReasonLabel('Unknown')).toBe('Unknown');
    expect(blockReasonLabel('CommissionPending')).toMatch(/Commission not calculated/);
  });
});
