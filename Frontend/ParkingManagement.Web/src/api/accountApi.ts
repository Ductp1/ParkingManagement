import { apiClient } from './client';
import type {
  DataRequestItem, DataRequestType, HistoryFilter, KycStatusView,
  ParkingHistoryPage, PersonalDataView,
} from '@/types/account';

// ===== US-005: KYC =====
export async function getMyKyc() {
  const { data } = await apiClient.get<KycStatusView>('/api/v1/users/me/kyc');
  return data;
}
/** Xác minh bằng số CCCD 12 chữ số (giả lập liên kết VNeID). */
export async function submitCitizenId(documentNumber: string) {
  const { data } = await apiClient.post<KycStatusView>('/api/v1/users/me/kyc/citizen-id', { documentNumber });
  return data;
}

// ===== US-082: lịch sử =====
export async function getMyHistory(filter: HistoryFilter, page: number, signal?: AbortSignal) {
  const { data } = await apiClient.get<ParkingHistoryPage>('/api/v1/users/me/history', {
    params: { filter: filter === 'all' ? undefined : filter, page }, signal,
  });
  return data;
}

// ===== US-007: quyền dữ liệu =====
const requestsEndpoint = '/api/v1/users/me/data-requests';
export async function getPersonalData(signal?: AbortSignal) {
  const { data } = await apiClient.get<PersonalDataView>('/api/v1/users/me/personal-data', { signal });
  return data;
}
export async function listDataRequests(page: number, signal?: AbortSignal) {
  const { data } = await apiClient.get<DataRequestItem[]>(requestsEndpoint, { params: { page }, signal });
  return data;
}
export async function submitDataRequest(type: DataRequestType, password: string, reason?: string) {
  const { data } = await apiClient.post<DataRequestItem>(requestsEndpoint, { type, password, reason });
  return data;
}
export async function downloadExport(id: number, password: string) {
  const { data } = await apiClient.post<Blob>(`${requestsEndpoint}/${id}/download`, { password }, { responseType: 'blob' });
  const url = URL.createObjectURL(data);
  const link = document.createElement('a');
  link.href = url;
  link.download = `parkmaster-du-lieu-ca-nhan-${id}.json`;
  link.click();
  window.setTimeout(() => URL.revokeObjectURL(url), 1000);
}
