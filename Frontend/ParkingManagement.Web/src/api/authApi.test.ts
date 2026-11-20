import { afterEach, describe, expect, it, vi } from 'vitest';

const h = vi.hoisted(() => ({ get: vi.fn() }));
vi.mock('./client', () => ({ apiClient: { get: h.get, post: vi.fn() } }));

import { currentUser } from './authApi';

afterEach(() => h.get.mockReset());

describe('currentUser', () => {
  it('exposes the owner profile id (tenant) separately from the user id', async () => {
    h.get.mockResolvedValue({ data: { id: 10, fullName: 'Hoàng Hà', roles: ['LotOwner'], ownerProfile: { id: 5 } } });

    const user = await currentUser();

    expect(user).toMatchObject({ id: 10, ownerProfileId: 5, role: 'Owner' });
  });

  it('leaves ownerProfileId empty for accounts without an owner profile', async () => {
    h.get.mockResolvedValue({ data: { id: 3, fullName: 'Driver', roles: ['Driver'], ownerProfile: null } });

    expect((await currentUser()).ownerProfileId).toBeUndefined();
  });
});
