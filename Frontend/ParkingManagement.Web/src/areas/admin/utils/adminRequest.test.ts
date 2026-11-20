import { afterEach, describe, expect, it, vi } from 'vitest';
import { AdminApiError, adminRequest, setAdminUnauthorizedHandler } from '@/api/adminClientApi';

const h = vi.hoisted(() => ({ request: vi.fn() }));
vi.mock('@/api/client', () => ({ apiClient: { request: h.request } }));

const failWith = async (promise: Promise<unknown>): Promise<AdminApiError> => {
  try {
    await promise;
  } catch (err) {
    return err as AdminApiError;
  }
  throw new Error('expected rejection');
};

afterEach(() => {
  setAdminUnauthorizedHandler(null);
  h.request.mockReset();
});

describe('adminRequest', () => {
  it('401 → gọi handler hết phiên rồi vẫn ném AdminApiError(401)', async () => {
    const onUnauthorized = vi.fn();
    setAdminUnauthorizedHandler(onUnauthorized);
    h.request.mockResolvedValue({ status: 401, data: '' });
    const err = await failWith(adminRequest({ url: '/api/v1/admin/dashboard' }));
    expect(onUnauthorized).toHaveBeenCalledTimes(1);
    expect(err).toBeInstanceOf(AdminApiError);
    expect(err.status).toBe(401);
  });

  it('403 / 500 không gọi handler hết phiên', async () => {
    const onUnauthorized = vi.fn();
    setAdminUnauthorizedHandler(onUnauthorized);
    h.request.mockResolvedValueOnce({ status: 403, data: { title: 'Forbidden' } });
    h.request.mockResolvedValueOnce({ status: 500, data: '' });
    expect((await failWith(adminRequest({ url: '/x' }))).status).toBe(403);
    expect((await failWith(adminRequest({ url: '/x' }))).status).toBe(500);
    expect(onUnauthorized).not.toHaveBeenCalled();
  });

  it('không có handler (ngoài admin, test) thì 401 chỉ ném lỗi', async () => {
    h.request.mockResolvedValue({ status: 401, data: '' });
    expect((await failWith(adminRequest({ url: '/x' }))).status).toBe(401);
  });

  it('lỗi mạng → status 0, network = true', async () => {
    h.request.mockImplementation(() => Promise.reject(new Error('Network Error')));
    const err = await failWith(adminRequest({ url: '/x' }));
    expect(err.status).toBe(0);
    expect(err.network).toBe(true);
  });

  it('404 có ProblemDetails giữ problem, message lấy từ detail', async () => {
    h.request.mockResolvedValue({ status: 404, data: { title: 'Not Found', detail: 'Lot 99 does not exist.' } });
    const err = await failWith(adminRequest({ url: '/x' }));
    expect(err.problem).not.toBeNull();
    expect(err.message).toBe('Lot 99 does not exist.');
  });
});
