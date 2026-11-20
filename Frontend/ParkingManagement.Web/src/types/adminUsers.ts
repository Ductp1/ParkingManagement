import type { SanctionLevel, SanctionStatus, SanctionSyncStatus, UserRoleType } from './adminEnums';

/**
 * DTO AdminService – Users (US-096).
 * Nguồn: Services/AdminService/AdminService.Application/Features/Users/UserServiceContracts.cs
 */
export interface AdminUserSummaryDto {
  id: number;
  fullName: string;
  email: string | null;
  status: string;
  roles: string[];
  businessName?: string | null;
}

export interface AdminUserOwnerProfileDto {
  id: number;
  businessName: string;
}

export interface AdminUserDto {
  id: number;
  fullName: string;
  email: string | null;
  phoneNumber: string | null;
  status: string;
  kycStatus: string;
  roles: string[];
  ownerProfile: AdminUserOwnerProfileDto | null;
  /** base64 – gửi lại khi đình chỉ / mở lại / xóa; người khác vừa sửa user → 409. */
  rowVersion?: string | null;
  /** Đình chỉ có hạn (Active + lockedUntilUtc tương lai) hoặc vô thời hạn (status Locked). */
  lockedUntilUtc?: string | null;
  lockReason?: string | null;
  ownerLocked?: boolean;
}

/** Kết quả POST admin/users/{id}/suspend | reactivate | delete. changed = false khi gọi lại thao tác đã xong. */
export interface UserLifecycleResultDto {
  userId: number;
  status: string;
  lockedUntilUtc: string | null;
  rowVersion: string | null;
  changed: boolean;
}

export interface SuspendUserRequest {
  reason: string;
  /** null = vô thời hạn. */
  lockedUntilUtc: string | null;
  rowVersion?: string | null;
}

export interface ReactivateUserRequest {
  reason: string;
  rowVersion?: string | null;
}

/** confirmEmail = email của user gõ lại để xác nhận xóa (không hoàn tác). */
export interface DeleteUserRequest {
  reason: string;
  confirmEmail: string;
  rowVersion?: string | null;
}

/** GET /api/v1/admin/users?role=&page=&pageSize= */
export interface AdminUserListQuery {
  role?: UserRoleType;
  page?: number;
  pageSize?: number;
}

/**
 * DTO AdminService – Sanctions (US-096).
 * Nguồn: Services/AdminService/AdminService.Application/Features/Owners/*
 */
export interface SanctionDto {
  id: number;
  ownerProfileId: number;
  parkingLotId: number | null;
  level: SanctionLevel | string;
  reason: string;
  startsAtUtc: string;
  endsAtUtc: string | null;
  status: SanctionStatus | string;
  userServiceSyncStatus: SanctionSyncStatus | string;
  userServiceSyncedAtUtc: string | null;
  penaltyAmount: number | null;
  issuedByUserId: number;
  createdAtUtc: string;
  updatedAtUtc: string | null;
}

/** POST /api/v1/admin/owners/{ownerProfileId}/lock – người khóa lấy từ JWT, không gửi trong body. */
export interface LockOwnerRequest {
  ownerUserId: number;
  reason: string;
  lockedUntilUtc?: string | null;
}

/** POST /api/v1/admin/owners/{ownerProfileId}/unlock */
export interface UnlockOwnerRequest {
  reason: string;
}

/** UserService – GET /api/v1/users/me (UserDto) */
export interface AdminCurrentUserDto {
  id: number;
  fullName: string;
  email: string | null;
  phoneNumber: string | null;
  status: string;
  kycStatus: string;
  roles: string[];
}
