import { act, cleanup, render, screen } from '@testing-library/react';
import { Provider } from 'react-redux';
import { MemoryRouter, Route, Routes, useLocation } from 'react-router-dom';
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { store } from '@/store';
import { setToken, setUser } from '@/store/slices/authSlice';
import { AdminApiError, adminRequest } from '@/api/adminClientApi';
import { useAdminSessionExpiry } from './useAdminSessionExpiry';

const h = vi.hoisted(() => ({ request: vi.fn() }));
vi.mock('@/api/client', () => ({ apiClient: { request: h.request } }));

const Probe = () => {
  const location = useLocation();
  const from = (location.state as { from?: { pathname?: string } } | null)?.from?.pathname ?? '';
  return (
    <>
      <output data-testid="location">{location.pathname + location.search}</output>
      <output data-testid="from">{from}</output>
    </>
  );
};

const AdminShell = () => {
  useAdminSessionExpiry();
  return <Probe />;
};

const renderAt = (url: string) =>
  render(
    <Provider store={store}>
      <MemoryRouter initialEntries={[url]}>
        <Routes>
          <Route path="/admin/*" element={<AdminShell />} />
          <Route path="/login" element={<Probe />} />
        </Routes>
      </MemoryRouter>
    </Provider>
  );

const call401 = async () => {
  h.request.mockResolvedValue({ status: 401, data: '' });
  await act(async () => {
    await adminRequest({ url: '/api/v1/admin/x' }).catch((err: unknown) => {
      expect(err).toBeInstanceOf(AdminApiError);
    });
  });
};

beforeEach(() => {
  sessionStorage.setItem('parkmaster_token', 'token');
  sessionStorage.setItem('parkmaster_refresh', 'refresh');
  store.dispatch(setToken('token'));
  store.dispatch(setUser({ id: 1, fullName: 'Admin', role: 'Admin', roles: ['Admin'] } as never));
});

afterEach(() => {
  cleanup();
  h.request.mockReset();
  sessionStorage.clear();
});

describe('useAdminSessionExpiry (401)', () => {
  it('xóa phiên, đăng xuất store và chuyển /login?returnUrl=<trang hiện tại>', async () => {
    renderAt('/admin/access/holidays?year=2026');
    await call401();

    expect(screen.getByTestId('location').textContent).toBe('/login?returnUrl=%2Fadmin%2Faccess%2Fholidays%3Fyear%3D2026');
    // LoginPage đọc state.from để quay lại sau khi đăng nhập.
    expect(screen.getByTestId('from').textContent).toBe('/admin/access/holidays?year=2026');
    expect(sessionStorage.getItem('parkmaster_token')).toBeNull();
    expect(sessionStorage.getItem('parkmaster_refresh')).toBeNull();
    expect(store.getState().auth.isAuthenticated).toBe(false);
    expect(store.getState().auth.user).toBeNull();
  });

  it('sau khi rời admin (đã ở /login) thì 401 tiếp theo không điều hướng nữa', async () => {
    renderAt('/admin/finance/revenue');
    await call401();
    expect(screen.getByTestId('location').textContent).toBe('/login?returnUrl=%2Fadmin%2Ffinance%2Frevenue');
    await call401();
    expect(screen.getByTestId('location').textContent).toBe('/login?returnUrl=%2Fadmin%2Ffinance%2Frevenue');
  });
});
