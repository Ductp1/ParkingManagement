import { test as base, request, type APIRequestContext } from '@playwright/test';
import { ACCOUNTS, type Role } from './demo';
import { demoPassword, GATEWAY_URL } from './env';

export interface ApiResult<T = any> { status: number; body: T }
export interface Tokens { accessToken: string; refreshToken: string }

/** Client gọi API qua Gateway với 1 vai (hoặc ẩn danh). Không ném lỗi theo mã HTTP – test tự kiểm tra status. */
export class Api {
  constructor(private readonly ctx: APIRequestContext, private readonly token?: string) {}

  private headers(): Record<string, string> {
    return this.token ? { Authorization: `Bearer ${this.token}` } : {};
  }

  private static async wrap(response: Awaited<ReturnType<APIRequestContext['get']>>): Promise<ApiResult> {
    const text = await response.text();
    let body: any = text;
    try { body = text ? JSON.parse(text) : null; } catch { /* body không phải JSON */ }
    return { status: response.status(), body };
  }

  async get<T = any>(url: string): Promise<ApiResult<T>> {
    return Api.wrap(await this.ctx.get(url, { headers: this.headers() }));
  }

  async post<T = any>(url: string, data?: unknown): Promise<ApiResult<T>> {
    return Api.wrap(await this.ctx.post(url, { headers: this.headers(), data: data ?? {} }));
  }
}

const tokenCache = new Map<Role, Tokens>();

/** Đăng nhập tài khoản demo qua Gateway (CAPTCHA tắt trong môi trường E2E). Token được dùng lại trong cùng worker. */
export async function login(ctx: APIRequestContext, role: Role, fresh = false): Promise<Tokens> {
  const cached = tokenCache.get(role);
  if (cached && !fresh) return cached;
  const response = await ctx.post('/api/v1/auth/login', {
    data: { emailOrPhone: ACCOUNTS[role].email, password: demoPassword() },
  });
  if (!response.ok()) {
    throw new Error(`Đăng nhập ${role} thất bại: HTTP ${response.status()} – đã chạy start-e2e-stack.ps1 chưa?`);
  }
  const tokens = (await response.json()) as Tokens;
  if (!fresh) tokenCache.set(role, tokens);
  return tokens;
}

export interface ApiFixtures {
  /** api.as('driver') → client mang JWT của tài xế; api.anonymous → không token. */
  api: { as(role: Role): Promise<Api>; anonymous: Api };
}

export const test = base.extend<ApiFixtures>({
  api: async ({}, use) => {
    const ctx = await request.newContext({ baseURL: GATEWAY_URL });
    await use({
      as: async (role: Role) => new Api(ctx, (await login(ctx, role)).accessToken),
      anonymous: new Api(ctx),
    });
    await ctx.dispose();
  },
});

export { expect } from '@playwright/test';
