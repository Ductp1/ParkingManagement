import type { KybStatus } from './adminEnums';

/**
 * DTO KYB Review (US-055) – ParkingService.
 * Các trường hồ sơ lấy đúng theo KybApplicationDto của US-054
 * (Services/ParkingService/ParkingService.Application/Features/Kyb/KybApplicationDtos.cs).
 * Phần admin (danh sách, thông tin bãi/chủ bãi, người duyệt) là hợp đồng DỰ KIẾN của US-055 BE,
 * route dưới /api/v1/parking-lots/kyb-applications để đi qua route "parking-lots" sẵn có của Gateway.
 */
export interface AdminKybApplicationSummaryDto {
  id: number;
  parkingLotId: number;
  parkingLotName: string;
  parkingLotAddress: string;
  ownerProfileId: number;
  ownerBusinessName: string | null;
  status: KybStatus | string;
  submittedAtUtc: string | null;
  fieldSurveyRequired: boolean;
  fieldSurveyAtUtc: string | null;
  reviewedByUserId: number | null;
}

export interface AdminKybApplicationDetailDto extends AdminKybApplicationSummaryDto {
  businessLicenseUrl: string | null;
  sitePhotoUrls: string[];
  photoLatitude: number | null;
  photoLongitude: number | null;
  fireSafetyCertificateUrl: string | null;
  fieldSurveyNote: string | null;
  createdAtUtc: string;
  reviewedAtUtc: string | null;
  rejectReason: string | null;
}

/** GET /api/v1/parking-lots/kyb-applications?status=&search=&page=&pageSize= */
export interface AdminKybListQuery {
  status?: KybStatus;
  search?: string;
  page?: number;
  pageSize?: number;
}

/** POST …/{id}/schedule-survey */
export interface ScheduleKybSurveyRequest {
  fieldSurveyAtUtc: string;
  note?: string | null;
}

/** POST …/{id}/approve – note lưu vào AuditLogs.Reason */
export interface ApproveKybRequest {
  note?: string | null;
}

/** POST …/{id}/reject – lưu KybApplications.RejectReason */
export interface RejectKybRequest {
  reason: string;
}

/** POST …/{id}/request-info */
export interface RequestKybInfoRequest {
  note: string;
}
