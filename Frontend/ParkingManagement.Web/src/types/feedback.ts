export type ComplaintStatus =
  | 'AwaitingOwner'
  | 'OwnerResponded'
  | 'Escalated'
  | 'Resolved'
  | 'Rejected'
  | 'Open';

export type ComplaintCategory =
  | 'LotFull'
  | 'Overcharge'
  | 'RefundRequest'
  | 'Damage'
  | 'StaffBehavior'
  | 'AppError'
  | 'Other';

export interface ComplaintItem {
  id: number;
  code: string;
  status: ComplaintStatus;
  category: ComplaintCategory;
  userId: number;
  bookingCode: string;
  parkingLotId: number;
  description: string;
  requestedAmount?: number;
  ownerResponseDueAtUtc?: string | null;
  ownerResponse?: string | null;
  createdAtUtc: string;
}

export interface ComplaintDetail extends ComplaintItem {
  priority?: string;
  assignedTeam?: string;
  evidenceUrls?: string[];
  messages?: ComplaintMessage[];
  payoutHeld?: boolean;
  ownerRespondedAtUtc?: string | null;
  escalatedAtUtc?: string | null;
}

export interface ComplaintMessage {
  id?: number;
  senderParty?: number | string;
  senderUserId?: number;
  message: string;
  attachmentUrlsJson?: string;
  createdAtUtc?: string;
}

export interface PayoutHoldItem {
  complaintCode: string;
  status: ComplaintStatus;
  parkingLotId: number;
  ownerProfileId: number;
  bookingCode: string;
  amount: number;
  heldSinceUtc: string;
}

export interface ReviewItem {
  id: number;
  parkingLotId?: number;
  reviewerName: string;
  rating: number;
  comment?: string;
  ownerReply?: string | null;
  ownerRepliedAtUtc?: string | null;
  createdAtUtc: string;
  isFlagged?: boolean;
  flagReason?: string | null;
}

export interface LotReviewsResponse {
  parkingLotId: number;
  averageRating: number;
  reviewCount: number;
  starBreakdown: Record<number, number>;
  latest: ReviewItem[];
}

export interface OwnerResponseRequest {
  response: string;
  evidenceUrls?: string[];
}

export interface OwnerReplyRequest {
  reply: string;
}

export interface ReviewReasonRequest {
  reason: string;
}

export function normalizeComplaintStatus(raw: unknown): ComplaintStatus {
  if (typeof raw === 'string') {
    const s = raw.trim();
    if (s === 'AwaitingOwner' || s === '1') return 'AwaitingOwner';
    if (s === 'OwnerResponded' || s === '2') return 'OwnerResponded';
    if (s === 'Escalated' || s === '3') return 'Escalated';
    if (s === 'Resolved' || s === '4') return 'Resolved';
    if (s === 'Rejected' || s === '5') return 'Rejected';
    if (s === 'Open' || s === '0') return 'Open';
  }
  if (typeof raw === 'number') {
    switch (raw) {
      case 0: return 'Open';
      case 1: return 'AwaitingOwner';
      case 2: return 'OwnerResponded';
      case 3: return 'Escalated';
      case 4: return 'Resolved';
      case 5: return 'Rejected';
    }
  }
  return 'AwaitingOwner';
}

export function normalizeComplaintCategory(raw: unknown): ComplaintCategory {
  if (typeof raw === 'string') {
    const c = raw.trim();
    if (['LotFull', 'Overcharge', 'RefundRequest', 'Damage', 'StaffBehavior', 'AppError', 'Other'].includes(c)) {
      return c as ComplaintCategory;
    }
    if (c === '1') return 'LotFull';
    if (c === '2') return 'Overcharge';
    if (c === '3') return 'RefundRequest';
    if (c === '4') return 'Damage';
    if (c === '5') return 'StaffBehavior';
    if (c === '6') return 'AppError';
    if (c === '9') return 'Other';
  }
  if (typeof raw === 'number') {
    switch (raw) {
      case 1: return 'LotFull';
      case 2: return 'Overcharge';
      case 3: return 'RefundRequest';
      case 4: return 'Damage';
      case 5: return 'StaffBehavior';
      case 6: return 'AppError';
      case 9: return 'Other';
    }
  }
  return 'Other';
}

export const COMPLAINT_CATEGORY_LABELS: Record<ComplaintCategory, string> = {
  LotFull: 'Hết chỗ đỗ',
  Overcharge: 'Tính sai tiền',
  RefundRequest: 'Yêu cầu hoàn tiền',
  Damage: 'Hư hỏng / Trầy xước',
  StaffBehavior: 'Thái độ nhân viên',
  AppError: 'Lỗi ứng dụng',
  Other: 'Vấn đề khác',
};

export const COMPLAINT_STATUS_LABELS: Record<ComplaintStatus, string> = {
  AwaitingOwner: 'Chờ chủ bãi',
  OwnerResponded: 'Đã phản hồi',
  Escalated: 'Chuyển nền tảng',
  Resolved: 'Đã giải quyết',
  Rejected: 'Đã từ chối',
  Open: 'Mới tiếp nhận',
};
