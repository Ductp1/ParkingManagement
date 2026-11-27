import { adminRequest } from './adminClientApi';
import type { ModeratedReviewDto, ReviewReasonRequest } from '@/types/adminReviews';

const REVIEWS = '/api/v1/reviews';

/** US-095: kiểm duyệt đánh giá bị khiếu nại (SupportService ReviewsController, RequireAdmin; mỗi thao tác ghi AuditRecorded). */
export const adminReviewsApi = {
  /** Hàng chờ: đánh giá đang bị chủ bãi khiếu nại, cũ nhất trước, tối đa 100. */
  reported: () => adminRequest<ModeratedReviewDto[]>({ method: 'GET', url: `${REVIEWS}/reported` }),

  /** Gỡ đánh giá khỏi trang bãi. 409 nếu đã gỡ. */
  hide: (id: number, reason: string) =>
    adminRequest<ModeratedReviewDto>({ method: 'POST', url: `${REVIEWS}/${id}/hide`, data: { reason } satisfies ReviewReasonRequest }),

  /** Bác khiếu nại, giữ đánh giá. 409 nếu không còn khiếu nại đang chờ. */
  dismissReport: (id: number, reason: string) =>
    adminRequest<ModeratedReviewDto>({ method: 'POST', url: `${REVIEWS}/${id}/dismiss-report`, data: { reason } satisfies ReviewReasonRequest }),
};
