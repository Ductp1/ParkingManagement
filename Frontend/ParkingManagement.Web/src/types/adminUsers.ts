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
