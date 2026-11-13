import { apiClient } from './client';

/** Loại hình kinh doanh – khớp AdminService PartnerBusinessType. */
export type PartnerBusinessType = 'Company' | 'HouseholdBusiness';

/** Body POST /api/v1/partner-applications (US-116 T1 / US-053) – form public "Đăng ký đối tác", không cần đăng nhập. */
export interface PartnerApplicationRequest {
  fullName: string;
  email: string;
  phone: string;
  position: string | null;
  businessType: PartnerBusinessType;
  businessName: string;
  taxCode: string;
  businessAddress: string;
  legalRepresentativeName: string;
  legalRepresentativePosition: string | null;
  lotCount: number;
  primaryLotAddress: string;
  estimatedCarSlots: number | null;
  estimatedMotorbikeSlots: number | null;
  operatingHours: string | null;
  consentAccepted: boolean;
}

/** 201: chỉ mã hồ sơ + trạng thái, không trả lại dữ liệu cá nhân. */
export interface PartnerApplicationReceipt {
  code: string;
  status: string;
  submittedAtUtc: string;
}

/** 400 dữ liệu sai · 409 trùng email / SĐT / MST với hồ sơ đang xử lý · 429 quá số hồ sơ cho phép mỗi giờ (AdminService qua Gateway). */
export async function submitPartnerApplication(request: PartnerApplicationRequest): Promise<PartnerApplicationReceipt> {
  const { data } = await apiClient.post<PartnerApplicationReceipt>('/api/v1/partner-applications', request);
  return data;
}
