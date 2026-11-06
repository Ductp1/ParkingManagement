import type { DataRequestStatus, DataRequestType } from './adminEnums';

/**
 * US-005 – hồ sơ định danh chờ duyệt, khớp UserService AdminKycItem (KycUseCases.cs).
 * Số giấy tờ đã che phía server; API trả mảng (20 dòng/trang), không có tổng.
 */
export interface AdminKycItemDto {
  userId: number;
  fullName: string;
  documentType: 'CitizenId' | 'DriverLicense' | string;
  maskedNumber: string;
  submittedAtUtc: string | null;
}

export type KycImageSide = 'Front' | 'Back';

/** US-007 – yêu cầu dữ liệu cá nhân, khớp UserService AdminDataRequestItem (DataRequestUseCases.cs). */
export interface AdminDataRequestDto {
  id: number;
  userId: number;
  type: DataRequestType | string;
  status: DataRequestStatus | string;
  createdAtUtc: string;
  dueAtUtc: string;
  completedAtUtc: string | null;
  handledByUserId: number | null;
  resultNote: string | null;
}

export interface AdminDataRequestQuery {
  status?: DataRequestStatus;
  type?: DataRequestType;
  page: number;
}

/** Cỡ trang cố định phía UserService cho cả hai danh sách. */
export const IDENTITY_PAGE_SIZE = 20;
