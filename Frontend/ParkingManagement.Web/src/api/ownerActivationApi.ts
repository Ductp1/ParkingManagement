import { apiClient } from './client';

/** U4: thông tin tài khoản chủ bãi của link kích hoạt (US-116). */
export interface OwnerActivationInfo {
  fullName: string;
  email: string;
  businessName: string;
  expiresAtUtc: string;
}

const TOKEN_HEADER = 'X-Activation-Token';

/** GET /api/v1/auth/owner-activation – token trong header; link hỏng / đã dùng / hết hạn → 400 kèm thông báo. */
export async function validateOwnerActivation(token: string): Promise<OwnerActivationInfo> {
  const { data } = await apiClient.get<OwnerActivationInfo>('/api/v1/auth/owner-activation', { headers: { [TOKEN_HEADER]: token } });
  return data;
}

/** POST /api/v1/auth/owner-activation – đặt mật khẩu, tài khoản Active; sau đó đăng nhập bằng email + mật khẩu này. */
export async function activateOwnerAccount(token: string, password: string): Promise<void> {
  await apiClient.post('/api/v1/auth/owner-activation', { token, password });
}

/** US-075: link mời nhân viên (/staff/activate) – cùng hợp đồng, khác đường dẫn. */
export async function validateStaffActivation(token: string): Promise<OwnerActivationInfo> {
  const { data } = await apiClient.get<OwnerActivationInfo>('/api/v1/auth/staff-activation', { headers: { [TOKEN_HEADER]: token } });
  return data;
}

export async function activateStaffAccount(token: string, password: string): Promise<void> {
  await apiClient.post('/api/v1/auth/staff-activation', { token, password });
}
