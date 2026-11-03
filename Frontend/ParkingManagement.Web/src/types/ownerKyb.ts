/**
 * Types & DTOs cho phân hệ Chủ bãi (Owner Portal)
 * Bao gồm: Quản lý bãi đỗ xe (US-053) và Thẩm định hồ sơ KYB 4 bước (US-054).
 */

export type KybStatus =
  | 'Draft'
  | 'Submitted'
  | 'UnderReview'
  | 'Approved'
  | 'Rejected'
  | 'NeedsMoreInfo';

export type ParkingLotStatus =
  | 'PendingApproval'
  | 'Active'
  | 'Suspended';

export type IntegrationTier = 'Manual' | 'PmsApi' | 'EdgeIot' | 0 | 1 | 2;

/**
 * Thông tin chi tiết hồ sơ thẩm định KYB 4 bước của bãi đỗ.
 */
export interface KybApplicationDto {
  id: number;
  parkingLotId: number;
  status: KybStatus | string;
  businessLicenseUrl: string | null;
  sitePhotoUrls: string[];
  photoLatitude: number | null;
  photoLongitude: number | null;
  fireSafetyCertificateUrl: string | null;
  fieldSurveyRequired: boolean;
  fieldSurveyAtUtc: string | null;
  fieldSurveyNote: string | null;
  submittedAtUtc: string | null;
  reviewedAtUtc: string | null;
  rejectReason: string | null;
}

/**
 * Payload lưu nháp tiến độ KYB (cho phép lưu từng bước độc lập, nullable các trường).
 */
export interface SaveKybDraftRequest {
  ownerProfileId: number;
  businessLicenseUrl?: string | null;
  sitePhotoUrls?: string[] | null;
  photoLatitude?: number | null;
  photoLongitude?: number | null;
  fireSafetyCertificateUrl?: string | null;
  fieldSurveyAtUtc?: string | null;
  fieldSurveyNote?: string | null;
}

/**
 * Payload nộp hồ sơ KYB chính thức (yêu cầu đầy đủ các trường theo quy tắc nghiệp vụ).
 */
export interface SubmitKybApplicationRequest {
  ownerProfileId: number;
  businessLicenseUrl: string;
  sitePhotoUrls: string[];
  photoLatitude?: number | null;
  photoLongitude?: number | null;
  fireSafetyCertificateUrl: string;
  fieldSurveyAtUtc?: string | null;
  fieldSurveyNote?: string | null;
}

/**
 * DTO bãi đỗ xe trong danh sách của chủ bãi (Owner Portal).
 */
export interface OwnerParkingLotItemDto {
  id: number;
  name: string;
  address: string;
  city: string;
  district: string | null;
  totalSlots: number;
  availableSlots: number;
  maxHeightCm: number;
  status: ParkingLotStatus | string;
  openingHours: string;
  createdAtUtc: string;
}

/**
 * Payload tạo bãi đỗ xe mới (US-053).
 */
export interface CreateParkingLotRequest {
  ownerProfileId: number;
  name: string;
  address: string;
  city: string;
  district?: string | null;
  latitude: number;
  longitude: number;
  totalSlots: number;
  maxHeightCm: number;
  openTime: string; // Định dạng "HH:mm" hoặc "HH:mm:ss"
  closeTime: string;
  integrationTier?: IntegrationTier;
  description?: string | null;
  hotlinePhone?: string | null;
}

/**
 * DTO chi tiết bãi đỗ xe dùng khi đối chiếu tọa độ và thông tin.
 */
export interface OwnerLotDetailDto {
  id: number;
  name: string;
  address: string;
  latitude: number;
  longitude: number;
  totalSlots: number;
  availableSlots: number;
  maxHeightCm: number;
  status: string;
  openTime?: string;
  closeTime?: string;
  description?: string | null;
  hotlinePhone?: string | null;
}
