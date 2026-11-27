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

/* POST …/{id}/approve không gọi từ FE: AdminService duyệt KYB khi hợp đồng bãi được ký (adminLotContractsApi.sign). */

/** POST …/{id}/reject – lưu KybApplications.RejectReason, ≤ 1000 ký tự. */
export interface RejectKybRequest {
  reason: string;
}

/** POST …/{id}/request-info – lưu KybApplications.ReviewNote, ≤ 1000 ký tự. */
export interface RequestKybInfoRequest {
  note: string;
}

/* ===== Xử lý bãi khai báo sai (US-056) – khớp Features/KybMisdeclaration/MisdeclarationDtos.cs ===== */

/** Minor = sai sót nhẹ, chỉ cảnh báo + hạn sửa (AC3); Suspected = nghi khai sai / ảnh giả, tạm khóa bãi + giải trình 24h (AC1). */
export const MisdeclarationSeverity = { Minor: 'Minor', Suspected: 'Suspected' } as const;
export type MisdeclarationSeverity = (typeof MisdeclarationSeverity)[keyof typeof MisdeclarationSeverity];

export const MisdeclarationCategory = {
  Address: 'Address',
  Photos: 'Photos',
  Capacity: 'Capacity',
  Documents: 'Documents',
  Other: 'Other',
} as const;
export type MisdeclarationCategory = (typeof MisdeclarationCategory)[keyof typeof MisdeclarationCategory];

/** Warned / Suspended = đang mở; Resolved = đã xử lý; Banned = xác nhận gian lận (AC2). */
export const MisdeclarationCaseStatus = { Warned: 'Warned', Suspended: 'Suspended', Resolved: 'Resolved', Banned: 'Banned' } as const;
export type MisdeclarationCaseStatus = (typeof MisdeclarationCaseStatus)[keyof typeof MisdeclarationCaseStatus];

/** Bãi đang xử lý – đọc được cả khi Suspended (API public trả 404 với bãi không công khai). */
export interface MisdeclarationLotDto {
  id: number;
  name: string;
  address: string;
  ownerProfileId: number;
  status: ParkingLotStatus | string;
  isPubliclyVisible: boolean;
}

/** Một case; isOverdue = case đang mở đã quá dueAtUtc (chỉ gắn cờ, không tự xử lý). */
export interface LotMisdeclarationCaseDto {
  id: number;
  severity: MisdeclarationSeverity | string;
  category: MisdeclarationCategory | string;
  status: MisdeclarationCaseStatus | string;
  finding: string;
  dueAtUtc: string;
  isOverdue: boolean;
  openedByUserId: number;
  openedAtUtc: string;
  escalationNote: string | null;
  escalatedAtUtc: string | null;
  closedByUserId: number | null;
  closedAtUtc: string | null;
  resolutionNote: string | null;
}

/** GET /api/v1/parking-lots/{lotId}/misdeclaration-cases – cases: mới mở trước; openCase tối đa 1. */
export interface LotMisdeclarationDto {
  parkingLot: MisdeclarationLotDto;
  openCase: LotMisdeclarationCaseDto | null;
  cases: LotMisdeclarationCaseDto[];
  /** Hạn sửa mặc định (ngày) cho cảnh báo – cấu hình KybMisdeclaration:MinorCorrectionDays. */
  minorCorrectionDays: number;
  /** Hạn giải trình khi tạm khóa (giờ) – cố định 24. */
  responseWindowHours: number;
}

/** POST …/misdeclaration-cases – finding ≤ 1000; correctionDays 1–30 chỉ cho Minor (bỏ trống = mặc định). */
export interface OpenLotMisdeclarationCaseRequest {
  severity: MisdeclarationSeverity;
  category: MisdeclarationCategory;
  finding: string;
  correctionDays?: number | null;
}

/** POST …/{caseId}/escalate và …/{caseId}/resolve – note bắt buộc, ≤ 1000. */
export interface MisdeclarationNoteRequest {
  note: string;
}

/** POST …/{caseId}/ban – reason bắt buộc, ≤ 1000. */
export interface BanLotOwnerRequest {
  reason: string;
}

/** Kết quả cấm chủ bãi: bãi bị khóa vĩnh viễn, hồ sơ KYB bị từ chối, case bị đóng. Khóa tài khoản làm ở Users (US-096). */
export interface OwnerBanResultDto {
  lot: LotMisdeclarationDto;
  ownerProfileId: number;
  suspendedLotIds: number[];
  rejectedKybApplicationIds: number[];
  bannedCaseIds: number[];
}
