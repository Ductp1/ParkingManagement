import { readFileSync } from 'node:fs';
import { dirname, resolve } from 'node:path';
import { fileURLToPath } from 'node:url';

export const REPO_ROOT = resolve(dirname(fileURLToPath(import.meta.url)), '../../..');
export const GATEWAY_URL = process.env.E2E_GATEWAY_URL ?? 'http://localhost:5000';
export const WEB_URL = process.env.E2E_WEB_URL ?? 'http://localhost:5173';

/** Mật khẩu chung của 6 tài khoản demo – đọc từ DemoIds.cs (nguồn seed của UserService), không chép vào test. */
export function demoPassword(): string {
  if (process.env.E2E_DEMO_PASSWORD) return process.env.E2E_DEMO_PASSWORD;
  const source = readFileSync(
    resolve(REPO_ROOT, 'BuildingBlocks/ParkingManagement.SharedKernel/Contracts/DemoIds.cs'), 'utf8');
  const match = source.match(/Password\s*=\s*"([^"]+)"/);
  if (!match) throw new Error('Không đọc được mật khẩu demo trong DemoIds.cs');
  return match[1];
}
