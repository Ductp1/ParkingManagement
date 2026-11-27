import { adminRequest } from './adminClientApi';
import type { CreateHolidayRequest, HolidayDto } from '@/types/adminHolidays';

/** US-071: danh mục ngày lễ (PaymentService) qua Gateway. Đọc công khai; thêm / xóa yêu cầu role Admin. */
const HOLIDAYS = '/api/v1/holidays';

export const adminHolidaysApi = {
  /** Ngày lễ của một năm (2000–2100), sắp theo ngày tăng dần. */
  list: (year: number, signal?: AbortSignal) =>
    adminRequest<HolidayDto[]>({ method: 'GET', url: HOLIDAYS, params: { year }, signal }),

  /** Thêm ngày lễ toàn quốc → 201 HolidayDto; trùng ngày → 400 ProblemDetails. */
  create: (body: CreateHolidayRequest) => adminRequest<HolidayDto>({ method: 'POST', url: HOLIDAYS, data: body }),

  /** Xóa ngày lễ → 204; không tồn tại → 404 ProblemDetails. */
  remove: (id: number) => adminRequest<void>({ method: 'DELETE', url: `${HOLIDAYS}/${id}` }),
};
