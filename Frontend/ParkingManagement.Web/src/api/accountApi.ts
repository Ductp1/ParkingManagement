import { apiClient } from './client';
import type {
  DataRequestItem, DataRequestType, HistoryFilter, KycDocumentType, KycImageSide, KycStatusView,
  ParkingHistoryPage, PersonalDataView,
} from '@/types/account';

// ===== US-005: KYC =====
export async function getMyKyc() {
  const { data } = await apiClient.get<KycStatusView>('/api/v1/users/me/kyc');
  return data;
}
export async function submitKyc(documentType: KycDocumentType, documentNumber: string, front: File, back: File) {
  const form = new FormData();
  form.append('documentType', documentType);
  form.append('documentNumber', documentNumber);
  form.append('frontImage', front);
  form.append('backImage', back);
  const { data } = await apiClient.post<KycStatusView>('/api/v1/users/me/kyc', form,
    { headers: { 'Content-Type': undefined }, timeout: 60000 });
  return data;
}
/** Ảnh giấy tờ không có URL công khai: tải qua phiên đăng nhập rồi hiển thị bằng object URL. */
/** Xác minh chỉ bằng số CCCD 12 chữ số (không cần ảnh). */
export async function submitCitizenId(documentNumber: string) {
  const { data } = await apiClient.post<KycStatusView>('/api/v1/users/me/kyc/citizen-id', { documentNumber });
  return data;
}

export async function getMyKycImage(side: KycImageSide, signal?: AbortSignal) {
  const { data } = await apiClient.get<Blob>(`/api/v1/users/me/kyc/images/${side}`, { responseType: 'blob', signal });
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
