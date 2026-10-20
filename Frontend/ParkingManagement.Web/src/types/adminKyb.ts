import type { KybStatus, ParkingLotStatus } from './adminEnums';

/**
 * DTO KYB Review (US-055) – ParkingService, khớp
 * Services/ParkingService/ParkingService.Application/Features/KybReview/AdminKybDtos.cs.
 * Route dưới /api/v1/parking-lots/kyb-applications để đi qua route "parking-lots" sẵn có của Gateway.
 * Tên doanh nghiệp chủ bãi thuộc UserService nên không có trong DTO (chỉ OwnerProfileId).
 */
export interface AdminKybApplicationSummaryDto {
  id: number;
  parkingLotId: number;
  parkingLotName: string;
  parkingLotAddress: string;
  city: string;
  district: string | null;
  ownerProfileId: number;
  totalSlots: number;
  status: KybStatus | string;
  lotStatus: ParkingLotStatus | string;
  submittedAtUtc: string | null;
  fieldSurveyRequired: boolean;
  fieldSurveyAtUtc: string | null;
  fieldSurveyCompletedAtUtc: string | null;
  reviewedByUserId: number | null;
  reviewedAtUtc: string | null;
}

/** Bãi đang chờ duyệt hiển thị cạnh hồ sơ (AdminKybLotDto). openTime / closeTime dạng "HH:mm:ss". */
export interface AdminKybLotDto {
  id: number;
  name: string;
  address: string;
  city: string;
  district: string | null;
  latitude: number;
  longitude: number;
  totalSlots: number;
  maxHeightCm: number;
  openTime: string;
  closeTime: string;
  status: ParkingLotStatus | string;
  ownerProfileId: number;
  activeSlotCount: number;
}

/**
 * Chi tiết hồ sơ: pháp lý, ảnh + GPS, PCCC, khảo sát thực địa.
 * gpsDistanceMeters / gpsWarning do BE tính (cảnh báo khi lệch > 100 m hoặc không có GPS);
 * approvalBlockers = lý do chưa duyệt được (tiếng Việt), rỗng = nút Approve dùng được;
 * fieldSurveyMandatory = bãi trên 50 chỗ phải khảo sát trước khi duyệt.
 */
export interface AdminKybApplicationDetailDto {
  id: number;
  status: KybStatus | string;
  businessLicenseUrl: string | null;
  sitePhotoUrls: string[];
  photoLatitude: number | null;
  photoLongitude: number | null;
  gpsDistanceMeters: number | null;
  gpsWarning: boolean;
  fireSafetyCertificateUrl: string | null;
  fieldSurveyRequired: boolean;
  fieldSurveyMandatory: boolean;
  fieldSurveyAtUtc: string | null;
  fieldSurveyCompletedAtUtc: string | null;
  fieldSurveyNote: string | null;
  createdAtUtc: string;
  submittedAtUtc: string | null;
  reviewedByUserId: number | null;
  reviewedAtUtc: string | null;
  rejectReason: string | null;
  /** Ghi chú khi duyệt hoặc nội dung yêu cầu bổ sung. */
  reviewNote: string | null;
  approvalBlockers: string[];
  parkingLot: AdminKybLotDto;
}

/** GET /api/v1/parking-lots/kyb-applications?status=&search=&page=&pageSize= (search: mã #id, tên / địa chỉ bãi). */
export interface AdminKybListQuery {
  status?: KybStatus;
  search?: string;
  page?: number;
  pageSize?: number;
}

/** POST …/{id}/field-survey – thời điểm phải ở tương lai; note ≤ 2000 ký tự. */
export interface ScheduleKybSurveyRequest {
  fieldSurveyAtUtc: string;
  note?: string | null;
}

/** POST …/{id}/field-survey/result – sau giờ hẹn; note bắt buộc, ≤ 2000 ký tự. */
export interface RecordKybSurveyResultRequest {
  note: string;
}

/** POST …/{id}/approve – note lưu KybApplications.ReviewNote, ≤ 1000 ký tự. */
export interface ApproveKybRequest {
  note?: string | null;
}

/** POST …/{id}/reject – lưu KybApplications.RejectReason, ≤ 1000 ký tự. */
export interface RejectKybRequest {
  reason: string;
}

/** POST …/{id}/request-info – lưu KybApplications.ReviewNote, ≤ 1000 ký tự. */
export interface RequestKybInfoRequest {
  note: string;
}
