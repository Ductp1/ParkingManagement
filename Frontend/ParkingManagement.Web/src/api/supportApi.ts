import { apiClient } from './client';
import type {
  ComplaintDetailDto,
  ComplaintSummaryDto,
  CreateComplaintRequest,
  CreateReviewRequest,
  FaqItemDto,
  HelpCenterDto,
  LotReviewsDto,
  ReviewDetailDto,
  ReviewEligibilityDto,
} from '@/types/support';

/**
 * API Hỗ trợ, FAQ, Khiếu nại & Đánh giá — route qua Gateway :5000 → SupportService :5107.
 */
export const supportApi = {
  // ===== FAQs =====
  getHelpCenter: async (audience = 'Driver', category?: string): Promise<HelpCenterDto> => {
    const response = await apiClient.get<HelpCenterDto>('/api/v1/faqs', {
      params: category ? { audience, category } : { audience },
    });
    return response.data;
  },

  getFaqById: async (id: number): Promise<FaqItemDto> => {
    const response = await apiClient.get<FaqItemDto>(`/api/v1/faqs/${id}`);
    return response.data;
  },

  // ===== Complaints =====
  createComplaint: async (request: CreateComplaintRequest): Promise<ComplaintDetailDto> => {
    const response = await apiClient.post<ComplaintDetailDto>('/api/v1/complaints', request);
    return response.data;
  },

  listMyComplaints: async (userId: number): Promise<ComplaintSummaryDto[]> => {
    const response = await apiClient.get<ComplaintSummaryDto[]>('/api/v1/complaints/my', {
      params: { userId },
    });
    return response.data;
  },

  getComplaintDetail: async (code: string, userId: number): Promise<ComplaintDetailDto> => {
    const response = await apiClient.get<ComplaintDetailDto>(
      `/api/v1/complaints/${encodeURIComponent(code)}`,
      { params: { userId } }
    );
    return response.data;
  },

  // ===== Reviews =====
  checkReviewEligibility: async (bookingCode: string): Promise<ReviewEligibilityDto> => {
    const response = await apiClient.get<ReviewEligibilityDto>(
      `/api/v1/reviews/bookings/${encodeURIComponent(bookingCode)}/eligibility`
    );
    return response.data;
  },

  createReview: async (request: CreateReviewRequest): Promise<ReviewDetailDto> => {
    const response = await apiClient.post<ReviewDetailDto>('/api/v1/reviews', request);
    return response.data;
  },

  getLotReviews: async (parkingLotId: number, take = 10): Promise<LotReviewsDto> => {
    const response = await apiClient.get<LotReviewsDto>('/api/v1/reviews', {
      params: { parkingLotId, take },
    });
    return response.data;
  },
};
