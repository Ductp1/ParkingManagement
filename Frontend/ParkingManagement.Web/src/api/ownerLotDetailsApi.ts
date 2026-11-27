import { apiClient } from './client';
import type {
  LotOperatingHoursWeeklyDto,
  UpdateLotOperatingHoursRequest,
  AmenityCatalogItemDto,
  LotAmenityDto,
  UpdateLotAmenitiesRequest,
  LotAttributesDto,
  UpdateLotAttributesRequest,
} from '@/types/ownerLotDetails';

/**
 * Service API quản lý Lịch hoạt động 7 ngày & Tiện ích bãi xe (UI-19, EP09).
 * Backend: ParkingService (Port 5103)
 */
export const ownerLotDetailsApi = {
  // ==================== LỊCH HOẠT ĐỘNG (OPERATING HOURS) ====================

  /**
   * Lấy lịch hoạt động 7 ngày (Thứ 2 - Chủ Nhật) của bãi xe (US-062).
   * Hỗ trợ lấy lịch cơ sở chung hoặc lịch ngoại lệ theo tuần cụ thể (?year=2026&weekOfYear=46).
   */
  getOperatingHours: async (
    lotId: number,
    ownerProfileId?: number,
    year?: number,
    weekOfYear?: number
  ): Promise<LotOperatingHoursWeeklyDto> => {
    const params: Record<string, unknown> = {};
    if (ownerProfileId) params.ownerProfileId = ownerProfileId;
    if (year && weekOfYear) {
      params.year = year;
      params.weekOfYear = weekOfYear;
    }
    const response = await apiClient.get<LotOperatingHoursWeeklyDto>(
      `/api/v1/parking-lots/${lotId}/operating-hours`,
      { params }
    );
    return response.data;
  },

  /**
   * Cập nhật / Đồng bộ lịch hoạt động 7 ngày cho bãi xe (US-062).
   * Nếu request có year & weekOfYear -> Lưu lịch ngoại lệ của tuần đó.
   * Nếu không có -> Lưu lịch tuần cơ sở.
   */
  updateOperatingHours: async (
    lotId: number,
    request: UpdateLotOperatingHoursRequest
  ): Promise<LotOperatingHoursWeeklyDto> => {
    const response = await apiClient.put<LotOperatingHoursWeeklyDto>(
      `/api/v1/parking-lots/${lotId}/operating-hours`,
      request
    );
    return response.data;
  },

  /**
   * Xóa cấu hình ngoại lệ của tuần cụ thể để khôi phục về lịch cơ sở.
   */
  deleteWeekOverrides: async (
    lotId: number,
    year: number,
    weekOfYear: number,
    ownerProfileId?: number
  ): Promise<LotOperatingHoursWeeklyDto> => {
    const params: Record<string, unknown> = { year, weekOfYear };
    if (ownerProfileId) params.ownerProfileId = ownerProfileId;
    const response = await apiClient.delete<LotOperatingHoursWeeklyDto>(
      `/api/v1/parking-lots/${lotId}/operating-hours/overrides`,
      { params }
    );
    return response.data;
  },

  // ==================== TIỆN ÍCH BÃI XE (AMENITIES) ====================

  /**
   * Lấy danh mục 12 tiện ích tiêu chuẩn của hệ thống (US-064).
   */
  getAmenitiesCatalog: async (): Promise<AmenityCatalogItemDto[]> => {
    const response = await apiClient.get<AmenityCatalogItemDto[]>(
      '/api/v1/parking-lots/amenities/catalog'
    );
    return response.data;
  },

  /**
   * Lấy danh sách tiện ích đang kích hoạt của bãi xe (US-064).
   */
  getLotAmenities: async (lotId: number): Promise<LotAmenityDto[]> => {
    const response = await apiClient.get<LotAmenityDto[]>(
      `/api/v1/parking-lots/${lotId}/amenities`
    );
    return response.data;
  },

  /**
   * Cập nhật toàn bộ tiện ích bãi xe (Batch replace) (US-064).
   */
  updateLotAmenities: async (
    lotId: number,
    request: UpdateLotAmenitiesRequest,
    ownerProfileId?: number
  ): Promise<LotAmenityDto[]> => {
    const response = await apiClient.put<LotAmenityDto[]>(
      `/api/v1/parking-lots/${lotId}/amenities`,
      request,
      {
        params: ownerProfileId ? { ownerProfileId } : undefined,
      }
    );
    return response.data;
  },

  // ==================== THUỘC TÍNH BÃI XE (ATTRIBUTES & HOTLINE) ====================

  /**
   * Lấy thông tin thuộc tính bổ trợ của bãi xe: hotline, chiều cao tối đa, mô tả.
   */
  getLotAttributes: async (lotId: number): Promise<LotAttributesDto> => {
    const response = await apiClient.get<LotAttributesDto>(
      `/api/v1/parking-lots/${lotId}/attributes`
    );
    return response.data;
  },

  /**
   * Cập nhật thuộc tính bổ trợ của bãi xe (hotline, chiều cao tối đa, mô tả).
   */
  updateLotAttributes: async (
    lotId: number,
    request: UpdateLotAttributesRequest,
    ownerProfileId?: number
  ): Promise<LotAttributesDto> => {
    const response = await apiClient.put<LotAttributesDto>(
      `/api/v1/parking-lots/${lotId}/attributes`,
      request,
      {
        params: ownerProfileId ? { ownerProfileId } : undefined,
      }
    );
    return response.data;
  },
};
