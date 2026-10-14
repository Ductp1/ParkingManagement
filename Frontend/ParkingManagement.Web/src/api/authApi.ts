import { apiClient } from './client';
import type { AuthUser, UserRole } from '@/types/auth';

interface Tokens { accessToken: string; refreshToken: string }
interface UserDto { id: number; fullName: string; email?: string; phoneNumber?: string; roles: string[] }

function save(tokens: Tokens) {
  sessionStorage.setItem('parkmaster_token', tokens.accessToken);
  sessionStorage.setItem('parkmaster_refresh', tokens.refreshToken);
}
export function clearSession() {
  sessionStorage.removeItem('parkmaster_token');
  sessionStorage.removeItem('parkmaster_refresh');
}
export async function currentUser(): Promise<AuthUser> {
  const { data } = await apiClient.get<UserDto>('/api/v1/users/me');
  const roles = data.roles.map(r => r === 'LotOwner' ? 'Owner' : r)
    .filter((r): r is UserRole => ['Driver', 'Owner', 'Staff', 'Admin'].includes(r));
  if (!roles.length) throw new Error('Tài khoản chưa có vai trò được hỗ trợ.');
  return { id: data.id, fullName: data.fullName, email: data.email, phone: data.phoneNumber,
    roles, role: roles[0] };
}
export async function login(emailOrPhone: string, password: string, captchaToken: string) {
  const { data } = await apiClient.post<Tokens>('/api/v1/auth/login', { emailOrPhone, password, captchaToken });
  save(data);
  try { return await currentUser(); }
  catch (error) {
    try { await apiClient.post('/api/v1/auth/logout', { refreshToken: data.refreshToken }); }
    catch { /* Preserve the original login/profile error. */ }
    clearSession();
    throw error;
  }
}
let restoring: Promise<AuthUser> | undefined;
export function restoreSession() {
  if (!restoring) restoring = restoreSessionCore().finally(() => { restoring = undefined; });
  return restoring;
}
async function restoreSessionCore() {
  try { return await currentUser(); }
  catch (error) {
    if (!(error instanceof Error && 'status' in error && error.status === 401)) throw error;
    const refreshToken = sessionStorage.getItem('parkmaster_refresh');
    if (!refreshToken) throw error;
    const { data } = await apiClient.post<Tokens>('/api/v1/auth/refresh', { refreshToken });
    save(data);
    return currentUser();
  }
}
export async function logoutSession() {
  const refreshToken = sessionStorage.getItem('parkmaster_refresh');
  if (refreshToken) await apiClient.post('/api/v1/auth/logout', { refreshToken });
  clearSession();
}
