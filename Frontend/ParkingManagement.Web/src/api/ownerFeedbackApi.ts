import { apiClient } from './client';
import {
  type ComplaintItem,
  type ComplaintDetail,
  type PayoutHoldItem,
  type LotReviewsResponse,
  type OwnerResponseRequest,
  type OwnerReplyRequest,
  type ReviewReasonRequest,
  normalizeComplaintStatus,
  normalizeComplaintCategory,
} from '@/types/feedback';

export const ownerFeedbackApi = {
  /**
   * Lấy danh sách tranh chấp / khiếu nại của chủ bãi (US-089 AC1, UC-38)
   */
  async listComplaints(ownerProfileId: number = 1): Promise<ComplaintItem[]> {
    try {
      const response = await apiClient.get<ComplaintItem[]>('/api/v1/complaints', {
        params: { ownerProfileId },
      });
      return (response.data || []).map((item) => ({
        ...item,
        status: normalizeComplaintStatus(item.status),
        category: normalizeComplaintCategory(item.category),
      }));
    } catch (err) {
      console.warn('API listComplaints failed, using cached/fallback:', err);
      return [];
    }
  },

  /**
   * Lấy danh sách các khoản thanh quyết toán bị phong tỏa (US-089 AC3)
   */
  async listPayoutHolds(ownerProfileId: number = 1): Promise<PayoutHoldItem[]> {
    try {
      const response = await apiClient.get<PayoutHoldItem[]>('/api/v1/complaints/payout-holds', {
        params: { ownerProfileId },
      });
      return (response.data || []).map((item) => ({
        ...item,
        status: normalizeComplaintStatus(item.status),
      }));
    } catch (err) {
      console.warn('API listPayoutHolds failed:', err);
      return [];
    }
  },

  /**
   * Lấy chi tiết tranh chấp để chủ bãi kiểm tra & giải trình (US-089 AC1)
   */
  async getComplaintDetail(code: string, ownerProfileId: number = 1): Promise<ComplaintDetail> {
    const response = await apiClient.get<ComplaintDetail>(`/api/v1/complaints/${code}/owner-view`, {
      params: { ownerProfileId },
    });
    const d = response.data;
    return {
      ...d,
      status: normalizeComplaintStatus(d.status),
      category: normalizeComplaintCategory(d.category),
    };
  },

  /**
   * Gửi phản hồi giải trình tranh chấp trong 48h kèm bằng chứng (US-089 AC1)
   */
  async respondToComplaint(
    code: string,
    data: OwnerResponseRequest,
    ownerProfileId: number = 1
  ): Promise<ComplaintDetail> {
    const response = await apiClient.post<ComplaintDetail>(
      `/api/v1/complaints/${code}/owner-response`,
      data,
      { params: { ownerProfileId } }
    );
    const d = response.data;
    return {
      ...d,
      status: normalizeComplaintStatus(d.status),
      category: normalizeComplaintCategory(d.category),
    };
  },

  /**
   * Lấy thống kê sao & danh sách đánh giá của bãi xe (US-094, UC-18)
   */
  async getLotReviews(parkingLotId: number = 1, take: number = 20): Promise<LotReviewsResponse> {
    try {
      const response = await apiClient.get<LotReviewsResponse>('/api/v1/reviews', {
        params: { parkingLotId, take },
      });
      return response.data;
    } catch (err) {
      console.warn('API getLotReviews failed, returning fallback:', err);
      return {
        parkingLotId,
        averageRating: 0,
        reviewCount: 0,
        starBreakdown: { 1: 0, 2: 0, 3: 0, 4: 0, 5: 0 },
        latest: [],
      };
    }
  },

  /**
   * Chủ bãi viết 1 phản hồi công khai cho đánh giá (US-094 AC1)
   */
  async replyToReview(
    reviewId: number,
    data: OwnerReplyRequest,
    ownerProfileId: number = 1
  ): Promise<void> {
    await apiClient.post(`/api/v1/reviews/${reviewId}/owner-reply`, data, {
      params: { ownerProfileId },
    });
  },

  /**
   * Chủ bãi khiếu nại đánh giá vi phạm gửi Admin xem xét (US-094 AC2)
   */
  async reportReview(
    reviewId: number,
    data: ReviewReasonRequest,
    ownerProfileId: number = 1
  ): Promise<void> {
    await apiClient.post(`/api/v1/reviews/${reviewId}/report`, data, {
      params: { ownerProfileId },
    });
  },
};
