import { adminRequest, cleanParams } from './adminClientApi';
import type { PagedResult } from '@/types/adminCommon';
import type {
  AdminKybApplicationDetailDto,
  AdminKybApplicationSummaryDto,
  AdminKybListQuery,
  BanLotOwnerRequest,
  LotMisdeclarationDto,
  MisdeclarationNoteRequest,
  OpenLotMisdeclarationCaseRequest,
  OwnerBanResultDto,
  RecordKybSurveyResultRequest,
  RejectKybRequest,
  RequestKybInfoRequest,
  ScheduleKybSurveyRequest,
} from '@/types/adminKyb';

/** US-055 – route dưới "parking-lots" để Gateway chuyển sang ParkingService (route /api/v1/admin/** thuộc AdminService). */
const BASE = '/api/v1/parking-lots/kyb-applications';

/** US-056 – xử lý bãi khai báo sai, cũng dưới "parking-lots". */
const misdeclarationBase = (lotId: number) => `/api/v1/parking-lots/${lotId}/misdeclaration-cases`;

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

  /**
   * Bãi (kể cả Suspended), case khai báo sai đang mở và lịch sử case (US-056).
   */
  getMisdeclaration: (lotId: number) =>
    adminRequest<LotMisdeclarationDto>({ method: 'GET', url: misdeclarationBase(lotId) }),

  /**
   * Mở case cho bãi Active: Minor = cảnh báo + hạn sửa; Suspected = tạm khóa + giải trình 24h (US-056 AC1, AC3).
   */
  openMisdeclarationCase: (lotId: number, data: OpenLotMisdeclarationCaseRequest) =>
    adminRequest<LotMisdeclarationDto>({ method: 'POST', url: misdeclarationBase(lotId), data }),

  /**
   * Cảnh báo → tạm khóa, hạn giải trình 24h tính lại (US-056).
   */
  escalateMisdeclarationCase: (lotId: number, caseId: number, data: MisdeclarationNoteRequest) =>
    adminRequest<LotMisdeclarationDto>({ method: 'POST', url: `${misdeclarationBase(lotId)}/${caseId}/escalate`, data }),

  /**
   * Đóng case; bãi đang tạm khóa → khôi phục Active (US-056).
   */
  resolveMisdeclarationCase: (lotId: number, caseId: number, data: MisdeclarationNoteRequest) =>
    adminRequest<LotMisdeclarationDto>({ method: 'POST', url: `${misdeclarationBase(lotId)}/${caseId}/resolve`, data }),

  /**
   * Xác nhận gian lận: khóa vĩnh viễn mọi bãi của chủ bãi, từ chối hồ sơ KYB còn mở (US-056 AC2).
   */
  banLotOwner: (lotId: number, caseId: number, data: BanLotOwnerRequest) =>
    adminRequest<OwnerBanResultDto>({ method: 'POST', url: `${misdeclarationBase(lotId)}/${caseId}/ban`, data }),
};
