import { afterEach, describe, expect, it } from 'vitest';
import { ADMIN_SECTIONS, SHOW_UNAVAILABLE_KEY, firstTabPath, isAdminPathAvailable, visibleTabs } from './navigation';

const section = (key: string) => ADMIN_SECTIONS.find((s) => s.key === key)!;

afterEach(() => {
  window.localStorage.removeItem(SHOW_UNAVAILABLE_KEY);
});

describe('navigation – tab chưa có nguồn dữ liệu (P0-10)', () => {
  it('ẩn tab chưa có nguồn khỏi menu', () => {
    expect(visibleTabs(section('finance')).map((t) => t.key)).toEqual(['settlements', 'payouts', 'commission', 'adjustments']);
    expect(visibleTabs(section('reports')).map((t) => t.key)).toEqual(['revenue']);
    expect(visibleTabs(section('support')).map((t) => t.key)).toEqual(['sanctions', 'reviews']);
    expect(visibleTabs(section('communications')).map((t) => t.key)).toEqual(['send-notification', 'faq']);
    expect(visibleTabs(section('operations')).some((t) => t.key === 'bookings')).toBe(false);
  });

  it('mọi nhóm còn ít nhất một tab dùng được', () => {
    ADMIN_SECTIONS.forEach((s) => expect(visibleTabs(s).length).toBeGreaterThan(0));
  });

  it('cờ dev admin.showUnavailable hiện lại tab ẩn', () => {
    window.localStorage.setItem(SHOW_UNAVAILABLE_KEY, '1');
    expect(visibleTabs(section('reports')).map((t) => t.key)).toContain('exports');
  });

  it('firstTabPath bỏ qua tab ẩn', () => {
    expect(firstTabPath(section('support'))).toBe('/admin/support/sanctions');
    expect(firstTabPath(section('reports'))).toBe('/admin/reports/revenue');
  });

  it.each([
    ['/admin/finance/transactions', false],
    ['/admin/reports/exports?x=1', false],
    ['/admin/operations/bookings', false],
    ['/admin/operations/bookings/monthly-passes', false],
    ['/admin/operations/gate-activity/shifts', true],
    ['/admin/access/security-privacy/security-events', false],
    ['/admin/operations/bookings/BK-001', true],
    ['/admin/operations/gate-activity/devices', true],
    ['/admin/finance/settlements', true],
    ['/admin/access/security-privacy', true],
  ])('isAdminPathAvailable(%s) = %s', (path, expected) => {
    expect(isAdminPathAvailable(path)).toBe(expected);
  });
});
