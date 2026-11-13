/**
 * US-095 (US-094 AC2) – đánh giá bị chủ bãi khiếu nại, khớp SupportService ModeratedReviewDto (ReviewModerationDtos.cs).
 * GET /api/v1/reviews/reported trả tối đa REPORTED_QUEUE_SIZE đánh giá (khiếu nại cũ nhất trước), chưa bị gỡ.
 */
export interface ModeratedReviewDto {
  id: number;
  parkingLotId: number;
  reviewerName: string;
  rating: number;
  comment: string | null;
  ownerReply: string | null;
  ownerRepliedAtUtc: string | null;
  isReported: boolean;
  reportReason: string | null;
  reportedAtUtc: string | null;
  isHidden: boolean;
  createdAtUtc: string;
}

/** Body POST /reviews/{id}/hide và /dismiss-report – lý do bắt buộc. */
export interface ReviewReasonRequest {
  reason: string;
}

/** ReviewModerationService.ReportedQueueSize. */
export const REPORTED_QUEUE_SIZE = 100;

/** ReviewRules.MinReasonLength / MaxReasonLength (SupportService.Domain). */
export const REVIEW_REASON_MIN = 10;
export const REVIEW_REASON_MAX = 500;
