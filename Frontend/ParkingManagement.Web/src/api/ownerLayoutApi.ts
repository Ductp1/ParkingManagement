import { apiClient } from './client';
import type {
  FloorLayoutDto,
  LayoutVersionSummaryDto,
  FloorLayoutDetailDto,
  FloorLayoutDiffDto,
  FloorLayoutSafetyCheckRequest,
  FloorLayoutSafetyCheckResultDto,
  RollbackFloorLayoutRequest,
} from '@/types/ownerLayout';

/**
 * Service API quản lý Sơ đồ lưới mặt bằng 2D & Lịch sử phiên bản tầng (US-059 & US-060).
 * Backend: ParkingService / FloorLayoutsController.
 */
export const ownerLayoutApi = {
  /**
   * Lấy sơ đồ mặt bằng lưới 2D hiện tại của tầng.
   */
  getLayout: async (floorId: number): Promise<FloorLayoutDto> => {
    const response = await apiClient.get<FloorLayoutDto>(
      `/api/v1/parking-lots/floors/${floorId}/layout`
    );
    return response.data;
  },

  /**
   * Lấy danh sách lịch sử tất cả phiên bản sơ đồ mặt bằng đã xuất bản của tầng (US-059).
   */
  getVersions: async (floorId: number): Promise<LayoutVersionSummaryDto[]> => {
    const response = await apiClient.get<LayoutVersionSummaryDto[]>(
      `/api/v1/parking-lots/floors/${floorId}/layout/versions`
    );
    return response.data;
  },

  /**
   * Lấy chi tiết sơ đồ của một phiên bản lịch sử cụ thể theo versionNo (US-059).
   */
  getVersionByNo: async (
    floorId: number,
    versionNo: number
  ): Promise<FloorLayoutDetailDto> => {
    const response = await apiClient.get<FloorLayoutDetailDto>(
      `/api/v1/parking-lots/floors/${floorId}/layout/versions/${versionNo}`
    );
    return response.data;
  },

  /**
   * So sánh chi tiết khác biệt (Diff) giữa 2 phiên bản sơ đồ (US-060).
   * toVersion bỏ trống hoặc undefined sẽ so sánh với bản xuất bản hiện tại.
   */
  getDiff: async (
    floorId: number,
    fromVersion: number,
    toVersion?: number | null
  ): Promise<FloorLayoutDiffDto> => {
    const params: Record<string, number> = { fromVersion };
    if (toVersion !== null && toVersion !== undefined) {
      params.toVersion = toVersion;
    }
    const response = await apiClient.get<FloorLayoutDiffDto>(
      `/api/v1/parking-lots/floors/${floorId}/layout/versions/diff`,
      { params }
    );
    return response.data;
  },

  /**
   * Khôi phục sơ đồ tầng về phiên bản lịch sử TargetVersionNo (US-060 Rollback).
   * Kích hoạt Slot Synchronization Engine, bảo vệ ô đỗ có xe (Occupied/Reserved).
   */
  rollback: async (
    floorId: number,
    versionNo: number,
    changeNote?: string
  ): Promise<FloorLayoutDto> => {
    const payload: RollbackFloorLayoutRequest = {
      changeNote: changeNote || undefined,
    };
    const response = await apiClient.post<FloorLayoutDto>(
      `/api/v1/parking-lots/floors/${floorId}/layout/versions/${versionNo}/rollback`,
      payload
    );
    return response.data;
  },

  /**
   * Kiểm tra trước tác động an toàn (Pre-flight safety check) của sơ đồ (US-060).
   */
  safetyCheck: async (
    floorId: number,
    data: FloorLayoutSafetyCheckRequest
  ): Promise<FloorLayoutSafetyCheckResultDto> => {
    const response = await apiClient.post<FloorLayoutSafetyCheckResultDto>(
      `/api/v1/parking-lots/floors/${floorId}/layout/safety-check`,
      data
    );
    return response.data;
  },
};
