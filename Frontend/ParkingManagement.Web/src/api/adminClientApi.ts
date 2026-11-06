import type { AxiosRequestConfig } from 'axios';
import { apiClient } from './client';
import type { ProblemDetails } from '@/types/common';

/**
 * Lỗi API của Admin Portal: giữ lại HTTP status để màn hình phân biệt 403 (No permission),
 * 404/405/501 (endpoint chưa có) và lỗi khác. status = 0 khi không kết nối được Gateway.
 */
export class AdminApiError extends Error {
  readonly status: number;
  readonly problem: ProblemDetails | null;

  constructor(status: number, message: string, problem: ProblemDetails | null = null) {
    super(message);
    this.name = 'AdminApiError';
    this.status = status;
    this.problem = problem;
  }
}

const isProblemDetails = (data: unknown): data is ProblemDetails =>
  typeof data === 'object' && data !== null && ('title' in data || 'detail' in data || 'errors' in data);

/** Lấy message người dùng từ ProblemDetails (backend trả tiếng Việt), giống interceptor của client.ts. */
const messageFrom = (problem: ProblemDetails | null, status: number): string => {
  if (problem?.detail) return problem.detail;
  if (problem?.errors) {
    const list = Object.values(problem.errors).flat();
    if (list.length > 0) return list.join(', ');
  }
  if (problem?.title) return problem.title;
  return `HTTP ${status}`;
};

/**
 * Gọi API qua apiClient (Gateway, gắn Bearer token sẵn) nhưng tự xử lý mã lỗi:
 * validateStatus luôn true nên interceptor lỗi của client.ts không đổi lỗi thành Error mất status.
 */
export const adminRequest = async <T>(config: AxiosRequestConfig): Promise<T> => {
  let response;
  try {
    response = await apiClient.request<unknown>({ ...config, validateStatus: () => true });
  } catch (err: unknown) {
    const message = err instanceof Error ? err.message : 'Không thể kết nối đến API Gateway.';
    throw new AdminApiError(0, message);
  }
  if (response.status >= 200 && response.status < 300) return response.data as T;
  const problem = isProblemDetails(response.data) ? response.data : null;
  throw new AdminApiError(response.status, messageFrom(problem, response.status), problem);
};

/** Tên file trong Content-Disposition (filename*=UTF-8''… ưu tiên hơn filename=…). */
const fileNameFrom = (disposition: unknown): string | null => {
  if (typeof disposition !== 'string') return null;
  const encoded = /filename\*=UTF-8''([^;]+)/i.exec(disposition);
  if (encoded) return decodeURIComponent(encoded[1]);
  const plain = /filename="?([^";]+)"?/i.exec(disposition);
  return plain ? plain[1] : null;
};

/**
 * Như adminRequest nhưng nhận file (Blob). Lỗi vẫn là ProblemDetails JSON nên đọc lại Blob để lấy thông điệp tiếng Việt.
 */
export const adminFileRequest = async (config: AxiosRequestConfig): Promise<{ blob: Blob; fileName: string | null }> => {
  let response;
  try {
    response = await apiClient.request<Blob>({ ...config, responseType: 'blob', validateStatus: () => true });
  } catch (err: unknown) {
    const message = err instanceof Error ? err.message : 'Không thể kết nối đến API Gateway.';
    throw new AdminApiError(0, message);
  }
  if (response.status >= 200 && response.status < 300) {
    return { blob: response.data, fileName: fileNameFrom(response.headers['content-disposition']) };
  }
  let problem: ProblemDetails | null = null;
  try {
    const parsed: unknown = JSON.parse(await response.data.text());
    problem = isProblemDetails(parsed) ? parsed : null;
  } catch {
    problem = null;
  }
  throw new AdminApiError(response.status, messageFrom(problem, response.status), problem);
};

/** Bỏ các tham số rỗng để query string chỉ gửi bộ lọc thật sự được chọn. */
export const cleanParams = (params: Record<string, string | number | boolean | null | undefined>) =>
  Object.fromEntries(Object.entries(params).filter(([, v]) => v !== undefined && v !== null && v !== ''));
