import { request } from '@playwright/test';
import { login } from './lib/api';
import { runsProject } from './lib/cli';
import { DISPUTE_AWAITING_OWNER } from './lib/demo';
import { GATEWAY_URL } from './lib/env';

const RESTART = 'Chạy lại ./scripts/start-e2e-stack.ps1 (tự xóa và seed lại DB pm_e2e_*) rồi chạy test.';

/**
 * Kiểm tra trước khi chạy: hệ thống E2E đang chạy và dữ liệu seed còn "sạch".
 * Luồng tranh chấp (US-089) dùng CP-0001 – chỉ phản hồi được 1 lần – nên chạy lại test trên dữ liệu cũ sẽ đỏ vô lý.
 */
export default async function globalSetup() {
  const ctx = await request.newContext({ baseURL: GATEWAY_URL });
  try {
    try {
      await ctx.get('/', { timeout: 5_000 });
    } catch {
      throw new Error(`Không gọi được Gateway ${GATEWAY_URL}. ${RESTART}`);
    }
    if (!runsProject('api')) return;

    const owner = await login(ctx, 'owner', true);
    const res = await ctx.get(`/api/v1/complaints/${DISPUTE_AWAITING_OWNER}/owner-view`, {
      headers: { Authorization: `Bearer ${owner.accessToken}` },
    });
    const status = res.ok() ? (await res.json()).status : `HTTP ${res.status()}`;
    if (status !== 'AwaitingOwner') {
      throw new Error(`Dữ liệu E2E đã được dùng ở lần chạy trước (${DISPUTE_AWAITING_OWNER} = ${status}). ${RESTART}`);
    }
  } finally {
    await ctx.dispose();
  }
}
