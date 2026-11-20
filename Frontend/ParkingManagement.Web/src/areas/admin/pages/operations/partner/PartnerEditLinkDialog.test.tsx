import { cleanup, render, screen, waitFor, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { AdminApiError } from '@/api/adminClientApi';
import type { PartnerApplicationDetailDto } from '@/types/adminPartnerOnboarding';
import { PartnerEditLinkDialog } from './PartnerEditLinkDialog';
import { partnerEditUrl } from './partnerFormat';

const h = vi.hoisted(() => ({ issueEditLink: vi.fn(), failWith: null as Error | null }));
// Lỗi từ API đi qua hàm thường (không qua vi.fn) để vitest không coi promise bị reject của spy là lỗi của test.
vi.mock('@/api/adminPartnerOnboardingApi', () => ({
  adminPartnerOnboardingApi: {
    issueEditLink: async (id: number) => {
      if (h.failWith) throw h.failWith;
      return h.issueEditLink(id);
    },
  },
}));

const app = { id: 12, code: 'PA-2026-0012', businessName: 'Công ty A', email: 'minh@example.vn', infoRequestNote: 'Bổ sung GPKD' } as PartnerApplicationDetailDto;

beforeEach(() => {
  h.issueEditLink.mockReset();
  h.failWith = null;
});
afterEach(cleanup);

describe('Partner edit link (T3)', () => {
  it('builds the public link with code and token after "#"', () => {
    expect(partnerEditUrl('https://parkmaster.vn', { code: 'PA-2026-0012', token: 'a+b/c' })).toBe(
      'https://parkmaster.vn/partner/apply/edit#code=PA-2026-0012&token=a%2Bb%2Fc'
    );
  });

  it('creates the link only on confirm, then shows and copies it', async () => {
    const user = userEvent.setup();
    const writeText = vi.fn().mockResolvedValue(undefined);
    Object.defineProperty(navigator, 'clipboard', { value: { writeText }, configurable: true });
    h.issueEditLink.mockResolvedValue({ code: 'PA-2026-0012', token: 'tok_1', expiresAtUtc: '2026-11-20T02:00:00Z' });
    render(<PartnerEditLinkDialog app={app} onClose={vi.fn()} />);

    expect(h.issueEditLink).not.toHaveBeenCalled();
    await user.click(screen.getByRole('button', { name: 'Create link' }));

    await waitFor(() => expect(h.issueEditLink).toHaveBeenCalledWith(12));
    const field = (await screen.findByLabelText(/Update link/)) as HTMLInputElement;
    expect(field.value).toBe(`${window.location.origin}/partner/apply/edit#code=PA-2026-0012&token=tok_1`);
    await user.click(screen.getByRole('button', { name: 'Copy' }));
    expect(writeText).toHaveBeenCalledWith(field.value);
    expect(screen.getByRole('button', { name: 'Copied' })).toBeTruthy();
  });

  it('shows the server message when the application is no longer waiting for info', async () => {
    const user = userEvent.setup();
    h.failWith = new AdminApiError(409, 'Chỉ cấp link bổ sung khi hồ sơ đang chờ bổ sung.');
    render(<PartnerEditLinkDialog app={app} onClose={vi.fn()} />);

    await user.click(screen.getByRole('button', { name: 'Create link' }));

    const dialog = screen.getByRole('dialog');
    expect(await within(dialog).findByText('Chỉ cấp link bổ sung khi hồ sơ đang chờ bổ sung.')).toBeTruthy();
    await waitFor(() => expect(within(dialog).getByRole('button', { name: 'Create link' })).toBeTruthy());
  });
});
