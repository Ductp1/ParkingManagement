import { cleanup, fireEvent, render, screen, waitFor, within } from '@testing-library/react';
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { AdminToastContext } from '../../hooks/useAdminToast';
import { FeatureFlagsPage } from './FeatureFlagsPage';

const h = vi.hoisted(() => ({ getAll: vi.fn(), updateFeatureFlag: vi.fn() }));
vi.mock('@/api/adminSettingsApi', () => ({ adminSettingsApi: { getAll: h.getAll, updateFeatureFlag: h.updateFeatureFlag } }));

const flag = { key: 'FEATURE_3D_MAP', isEnabled: false, description: 'Sơ đồ 3D', updatedByUserId: null, updatedAtUtc: null };
const toast = vi.fn();

const renderPage = async () => {
  render(
    <AdminToastContext.Provider value={{ show: toast }}>
      <FeatureFlagsPage />
    </AdminToastContext.Provider>,
  );
  fireEvent.click(await screen.findByRole('switch'));
  return screen.findByRole('dialog');
};

describe('FeatureFlagsPage (US-099)', () => {
  beforeEach(() => {
    h.getAll.mockReset().mockResolvedValue({ configs: [], featureFlags: [flag] });
    h.updateFeatureFlag.mockReset();
    toast.mockReset();
  });
  afterEach(cleanup);

  it('requires a reason before calling the API', async () => {
    const dialog = await renderPage();

    fireEvent.click(within(dialog).getByRole('button', { name: 'Enable flag' }));

    expect(await within(dialog).findByText('A reason is required.')).toBeTruthy();
    expect(h.updateFeatureFlag).not.toHaveBeenCalled();
  });

  it('sends the opposite value with the reason, then shows a toast and reloads', async () => {
    h.updateFeatureFlag.mockResolvedValue({ ...flag, isEnabled: true, updatedByUserId: 1, updatedAtUtc: '2026-11-06T01:00:00Z' });
    const dialog = await renderPage();

    fireEvent.change(within(dialog).getByRole('textbox'), { target: { value: '  Mở thử 3D  ' } });
    fireEvent.click(within(dialog).getByRole('button', { name: 'Enable flag' }));

    await waitFor(() => expect(toast).toHaveBeenCalledWith(expect.objectContaining({ type: 'success', message: 'FEATURE_3D_MAP is now On.' })));
    expect(h.updateFeatureFlag).toHaveBeenCalledWith('FEATURE_3D_MAP', { isEnabled: true, reason: 'Mở thử 3D' });
    await waitFor(() => expect(h.getAll).toHaveBeenCalledTimes(2));
    await waitFor(() => expect(screen.queryByRole('dialog')).toBeNull());
  });

  it('keeps the dialog open and shows the API error on conflict', async () => {
    h.updateFeatureFlag.mockRejectedValue(new Error('Feature flag FEATURE_3D_MAP vừa được đổi từ một yêu cầu khác.'));
    const dialog = await renderPage();

    fireEvent.change(within(dialog).getByRole('textbox'), { target: { value: 'Mở thử' } });
    fireEvent.click(within(dialog).getByRole('button', { name: 'Enable flag' }));

    expect(await within(dialog).findByText(/vừa được đổi/)).toBeTruthy();
    expect(toast).not.toHaveBeenCalled();
    await waitFor(() => expect(h.getAll).toHaveBeenCalledTimes(2));   // danh sách phía sau được tải lại
    expect(screen.getByRole('dialog')).toBeTruthy();
  });
});
