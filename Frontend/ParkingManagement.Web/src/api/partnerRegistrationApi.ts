import { apiClient } from './client';

/**
 * US-053 – form public "Trở thành đối tác chủ bãi" (AdminService, không cần đăng nhập).
 * Khớp Services/AdminService/AdminService.API/Controllers/PartnerRegistrationsController.cs.
 * Giấy tờ pháp lý chỉ gửi lên, không có API public đọc lại: chỉ Admin xem trong Partner Onboarding.
 */
export type PartnerBusinessType = 'Company' | 'HouseholdBusiness';

export interface PartnerRegistrationFields {
  fullName: string;
  position: string;
  email: string;
  phone: string;
  businessType: PartnerBusinessType;
  businessName: string;
  taxCode: string;
  businessAddress: string;
  legalRepresentativeName: string;
  legalRepresentativePosition: string;
  lotCount: string;
  primaryLotAddress: string;
  estimatedCarSlots: string;
  estimatedMotorbikeSlots: string;
  operatingHours: string;
  consentAccepted: boolean;
}

export type PartnerDocumentKind = 'businessLicense' | 'representativeId' | 'lotLegalProof' | 'other';

export type PartnerDocumentFiles = Record<PartnerDocumentKind, File[]>;

export interface PartnerApplicationReceipt {
  code: string;
  status: string;
  submittedAtUtc: string;
}

/** Giới hạn khớp PartnerDocumentRules ở backend. */
export const PARTNER_DOCUMENT_LIMITS = {
  maxFileBytes: 10 * 1024 * 1024,
  maxTotalBytes: 25 * 1024 * 1024,
  maxFiles: 6,
  accept: '.pdf,.jpg,.jpeg,.png,application/pdf,image/jpeg,image/png',
} as const;

const FILE_FIELDS: Record<PartnerDocumentKind, string> = {
  businessLicense: 'businessLicenseFiles',
  representativeId: 'representativeIdFiles',
  lotLegalProof: 'lotLegalProofFiles',
  other: 'otherFiles',
};

export const partnerRegistrationApi = {
  submit: async (fields: PartnerRegistrationFields, files: PartnerDocumentFiles): Promise<PartnerApplicationReceipt> => {
    const form = new FormData();
    Object.entries(fields).forEach(([key, value]) => {
      const text = typeof value === 'boolean' ? String(value) : value.trim();
      if (text !== '') form.append(key, text);
    });
    (Object.keys(FILE_FIELDS) as PartnerDocumentKind[]).forEach((kind) => {
      files[kind].forEach((file) => form.append(FILE_FIELDS[kind], file, file.name));
    });
    // Ghi đè Content-Type JSON mặc định của apiClient để axios gửi multipart (trình duyệt tự thêm boundary).
    const res = await apiClient.post<PartnerApplicationReceipt>('/api/v1/admin/partner-registrations', form, {
      headers: { 'Content-Type': 'multipart/form-data' },
      timeout: 60000,
    });
    return res.data;
  },
};
