import { cleanup, render, screen, within } from '@testing-library/react';
import { MemoryRouter } from 'react-router-dom';
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import type { AdminDataRequestDto, AdminKycItemDto } from '@/types/adminIdentity';
import { AdminToastProvider } from '../../components/AdminToastProvider';
import { IdentityReviewPage } from '../operations/identity/IdentityReviewPage';
import { DataRequestsPage } from './DataRequestsPage';

const h = vi.hoisted(() => ({ listPending: vi.fn(), list: vi.fn() }));
vi.mock('@/api/adminIdentityApi', () => ({
  adminKycApi: { listPending: h.listPending, approve: vi.fn(), reject: vi.fn() },
  adminDataRequestsApi: { list: h.list, approveDeletion: vi.fn(), completeCorrection: vi.fn(), reject: vi.fn() },
}));

const request = (): AdminDataRequestDto => ({
  id: 12, userId: 7, type: 'Correction', status: 'Submitted', createdAtUtc: '2026-11-10T02:00:00Z', dueAtUtc: '2099-01-01T00:00:00Z',
  completedAtUtc: null, handledByUserId: null, resultNote: null,
});
const kyc = (): AdminKycItemDto => ({ userId: 7, fullName: 'Lan Driver', documentType: 'CitizenId', maskedNumber: '********1234', submittedAtUtc: '2026-11-10T02:00:00Z' });

const renderAt = (url: string, page: React.ReactElement) =>
  render(
    <AdminToastProvider>
      <MemoryRouter initialEntries={[url]}>{page}</MemoryRouter>
    </AdminToastProvider>
  );

const href = (el: HTMLElement) => el.getAttribute('href');

beforeEach(() => {
  h.listPending.mockReset().mockResolvedValue([kyc()]);
  h.list.mockReset().mockResolvedValue([request()]);
});
afterEach(cleanup);

describe('T-803 audit log links', () => {
  it('Data requests: title link lists every request decision, drawer links the request history', async () => {
    renderAt('/admin/access/security-privacy?request=12', <DataRequestsPage subTabs={[]} />);

    const history = await screen.findByRole('link', { name: 'View in audit log' });
    expect(href(history)).toBe('/admin/access/audit-log?service=UserService&entity=DataSubjectRequest&entityId=12');
    expect(href(screen.getByRole('link', { name: 'Audit log' }))).toBe('/admin/access/audit-log?service=UserService&entity=DataSubjectRequest');
  });

  it('Identity review: title link lists KYC decisions, drawer links the user and their history', async () => {
    renderAt('/admin/operations/identity-review?user=7', <IdentityReviewPage />);

    const history = await screen.findByRole('link', { name: 'View in audit log' });
    expect(href(history)).toBe('/admin/access/audit-log?service=UserService&entity=User&entityId=7');
    expect(href(screen.getByRole('link', { name: 'Audit log' }))).toBe('/admin/access/audit-log?service=UserService&entity=User');

    const drawer = screen.getByRole('dialog');
    expect(href(within(drawer).getByRole('link', { name: 'User #7' }))).toBe('/admin/operations/users/7');
  });
});
