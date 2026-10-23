import { adminRequest, cleanParams } from './adminClientApi';
import type { PagedResult } from '@/types/adminCommon';
import type {
  AdminKybApplicationDetailDto,
  AdminKybApplicationSummaryDto,
  AdminKybListQuery,
  RecordKybSurveyResultRequest,
  RejectKybRequest,
  RequestKybInfoRequest,
  ScheduleKybSurveyRequest,
} from '@/types/adminKyb';

/** US-055 – route dưới "parking-lots" để Gateway chuyển sang ParkingService (route /api/v1/admin/** thuộc AdminService). */
const BASE = '/api/v1/parking-lots/kyb-applications';

export const adminKybApi = {
  /**
   * Danh sách hồ sơ KYB có lọc trạng thái, tìm kiếm, phân trang (US-055).
   */
  list: (query: AdminKybListQuery) =>
    adminRequest<PagedResult<AdminKybApplicationSummaryDto>>({
      method: 'GET',
      url: BASE,
      params: cleanParams({ status: query.status, search: query.search, page: query.page, pageSize: query.pageSize }),
    }),

  /**
   * Chi tiết hồ sơ KYB kèm bãi đỗ, khoảng cách GPS và điều kiện duyệt còn thiếu (US-055).
   */
  getById: (id: number) => adminRequest<AdminKybApplicationDetailDto>({ method: 'GET', url: `${BASE}/${id}` }),

  /**
   * Submitted → UnderReview, người duyệt lấy từ JWT (US-055).
   */
  startReview: (id: number) =>
    adminRequest<AdminKybApplicationDetailDto>({ method: 'POST', url: `${BASE}/${id}/start-review` }),

  /**
   * Lên lịch khảo sát thực địa khi UnderReview (US-055 AC4).
   */
  scheduleSurvey: (id: number, data: ScheduleKybSurveyRequest) =>
    adminRequest<AdminKybApplicationDetailDto>({ method: 'POST', url: `${BASE}/${id}/field-survey`, data }),

  /**
   * Ghi nhận kết quả khảo sát sau giờ hẹn (US-055 AC4).
   */
  recordSurveyResult: (id: number, data: RecordKybSurveyResultRequest) =>
    adminRequest<AdminKybApplicationDetailDto>({ method: 'POST', url: `${BASE}/${id}/field-survey/result`, data }),

  /**
   * UnderReview → Rejected, lý do bắt buộc (US-055 AC3).
   */
  reject: (id: number, data: RejectKybRequest) =>
    adminRequest<AdminKybApplicationDetailDto>({ method: 'POST', url: `${BASE}/${id}/reject`, data }),

  /**
   * UnderReview → NeedsMoreInfo, nội dung bắt buộc (US-055 AC3).
   */
  requestInfo: (id: number, data: RequestKybInfoRequest) =>
    adminRequest<AdminKybApplicationDetailDto>({ method: 'POST', url: `${BASE}/${id}/request-info`, data }),
};
