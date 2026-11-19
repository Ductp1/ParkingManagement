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
/** P2 (T4): hồ sơ đang chờ bổ sung, đọc bằng link ParkMaster gửi – token đi trong header, không nằm trong URL gọi API. */
export interface PartnerApplicationEditView {
  code: string;
  status: string;
  infoRequestNote: string | null;
  editLinkExpiresAtUtc: string | null;
  fullName: string;
  email: string;
  phone: string;
  position: string | null;
  businessType: PartnerBusinessType | string;
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
}

const EDIT_TOKEN_HEADER = 'X-Edit-Token';

export async function getPartnerApplicationForEdit(code: string, token: string): Promise<PartnerApplicationEditView> {
  const { data } = await apiClient.get<PartnerApplicationEditView>(`/api/v1/partner-applications/${encodeURIComponent(code)}`, {
    headers: { [EDIT_TOKEN_HEADER]: token },
  });
  return data;
}

/** P3 (T4): gửi bổ sung → hồ sơ quay lại "đang thẩm định"; link hết hiệu lực. */
export async function resubmitPartnerApplication(code: string, token: string, request: PartnerApplicationRequest): Promise<PartnerApplicationReceipt> {
  const { data } = await apiClient.put<PartnerApplicationReceipt>(`/api/v1/partner-applications/${encodeURIComponent(code)}/resubmit`, request, {
    headers: { [EDIT_TOKEN_HEADER]: token },
  });
  return data;
}
