export type ComplaintCategory =
  | 'LotFull'
  | 'Overcharge'
  | 'RefundRequest'
  | 'Damage'
  | 'StaffBehavior'
  | 'AppError'
  | 'SlotOccupied'
  | 'Other';

export interface CreateComplaintRequest {
  userId: number;
  bookingCode: string;
  category: ComplaintCategory;
  description: string;
  requestedAmount?: number | null;
  evidenceUrls?: string[] | null;
}

export interface ComplaintSummaryDto {
  id: number;
  code: string;
  status: string;
  category: string;
  priority: string;
  bookingCode?: string | null;
  createdAtUtc: string;
  updatedAtUtc?: string | null;
}

export interface ComplaintMessageDto {
  id: number;
  senderParty: string;
  message: string;
  attachmentUrls: string[];
  createdAtUtc: string;
}

export interface ComplaintDetailDto {
  id: number;
  code: string;
  status: string;
  category: string;
  priority: string;
  assignedTeam: string;
  bookingCode?: string | null;
  parkingLotId: number;
  description: string;
  evidenceUrls: string[];
  requestedAmount?: number | null;
  resolution?: string | null;
  messages?: ComplaintMessageDto[];
  createdAtUtc: string;
}

export interface FaqItemDto {
  id: number;
  category: string;
  audience: string;
  question: string;
  answer: string;
  sortOrder: number;
  viewCount: number;
}

export interface FaqCategoryDto {
  category: string;
  items: FaqItemDto[];
}

export interface ContactChannelDto {
  channel: string;
  value: string;
  note?: string | null;
}

export interface HelpCenterDto {
  audience: string;
  categories: FaqCategoryDto[];
  contacts: ContactChannelDto[];
}

export interface CreateReviewRequest {
  bookingCode: string;
  rating: number;
  comment?: string | null;
  reviewerName?: string | null;
}

export interface ReviewDetailDto {
  id: number;
  bookingCode: string;
  parkingLotId: number;
  rating: number;
  comment?: string | null;
  reviewerName: string;
  createdAtUtc: string;
}

export interface ReviewEligibilityDto {
  bookingCode: string;
  canReview: boolean;
  reason?: string | null;
  reviewId?: number | null;
}

export interface LotReviewItemDto {
  id: number;
  bookingCode?: string | null;
  rating: number;
  comment?: string | null;
  reviewerName: string;
  ownerReply?: string | null;
  createdAtUtc: string;
}

export interface LotReviewsDto {
  parkingLotId: number;
  averageRating: number;
  reviewCount: number;
  starBreakdown: Record<number, number>;
  latest: LotReviewItemDto[];
}
