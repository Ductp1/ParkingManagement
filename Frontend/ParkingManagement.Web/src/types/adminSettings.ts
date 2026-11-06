import type { SystemConfigChangeStatus } from './adminEnums';

/**
 * DTO AdminService – System Settings (US-098) và Feature Flags.
 * Nguồn: Services/AdminService/AdminService.Application/Features/Settings/*
 */
export interface SystemConfigDto {
  key: string;
  value: string;
  dataType: string;
  description: string | null;
  updatedAtUtc: string | null;
}

export interface FeatureFlagDto {
  key: string;
  isEnabled: boolean;
  description: string | null;
}

/** GET /api/v1/admin/settings?at= */
export interface PlatformSettingsDto {
  configs: SystemConfigDto[];
  featureFlags: FeatureFlagDto[];
}

/** GET /api/v1/admin/settings/{key}?at= – effectiveFromUtc = null khi đang dùng giá trị mặc định. */
export interface SystemConfigDetailDto {
  key: string;
  value: string;
  dataType: string;
  description: string | null;
  defaultValue: string;
  effectiveFromUtc: string | null;
  minValue: string | null;
  maxValue: string | null;
  allowedValues: string[] | null;
}

export interface SystemConfigChangeDto {
  id: number;
  key: string;
  value: string;
  previousValue: string;
  effectiveFromUtc: string;
  status: SystemConfigChangeStatus | string;
  reason: string;
  createdByUserId: number;
  createdAtUtc: string;
  cancelledAtUtc: string | null;
  cancelledByUserId: number | null;
  cancelReason: string | null;
}

/** PUT /api/v1/admin/settings/{key} – effectiveFromUtc null = hiệu lực ngay. */
export interface UpdateSystemConfigRequest {
  value: string;
  reason: string;
  effectiveFromUtc?: string | null;
}

/** POST /api/v1/admin/settings/{key}/changes/{changeId}/cancel */
export interface CancelSystemConfigChangeRequest {
  reason: string;
}
