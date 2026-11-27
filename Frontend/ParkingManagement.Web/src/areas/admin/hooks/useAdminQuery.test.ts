import { afterEach, describe, expect, it, vi } from 'vitest';
import { AdminApiError } from '@/api/adminClientApi';
import { toQueryStatus } from './useAdminQuery';

const problem = { title: 'Not Found', detail: 'Lot 99 does not exist.' };

afterEach(() => {
  vi.unstubAllGlobals();
});

describe('toQueryStatus', () => {
  it.each([
    [new AdminApiError(401, 'Unauthorized'), 'sessionExpired'],
    [new AdminApiError(403, 'Forbidden'), 'forbidden'],
    [new AdminApiError(404, 'Lot 99 does not exist.', problem), 'notFound'],
    [new AdminApiError(404, 'HTTP 404'), 'unavailable'],
    [new AdminApiError(405, 'HTTP 405'), 'unavailable'],
    [new AdminApiError(501, 'HTTP 501'), 'unavailable'],
    [new AdminApiError(0, 'Network Error', null, true), 'offline'],
    [new AdminApiError(500, 'HTTP 500'), 'error'],
    [new AdminApiError(409, 'Conflict', { title: 'Conflict' }), 'error'],
    [new AdminApiError(0, 'Unexpected token'), 'error'],
  ] as const)('%o → %s', (error, expected) => {
    expect(toQueryStatus(error)).toBe(expected);
  });

  it('status 0 khi trình duyệt báo offline → offline', () => {
    vi.stubGlobal('navigator', { onLine: false });
    expect(toQueryStatus(new AdminApiError(0, 'Failed'))).toBe('offline');
  });
});
