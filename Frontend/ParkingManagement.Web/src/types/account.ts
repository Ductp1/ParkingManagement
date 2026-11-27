export type KycDocumentType = 'CitizenId' | 'DriverLicense';
export type KycState = 'NotSubmitted' | 'Pending' | 'Verified' | 'Rejected';

export interface KycStatusView {
  status: KycState;
  documentType?: KycDocumentType | null;
  maskedNumber?: string | null;
  submittedAtUtc?: string | null;
  verifiedAtUtc?: string | null;
  rejectionReason?: string | null;
}

export type HistoryFilter = 'all' | 'upcoming' | 'active' | 'completed' | 'cancelled';

export interface PaymentRecord {
  code: string;
  method: string;
  status: string;
  amount: number;
  refundedAmount: number;
  paidAtUtc?: string | null;
  invoiceNumber?: string | null;
}
export interface ParkingHistoryItem {
  bookingId: number;
  code: string;
  status: string;
  parkingLotName: string;
  slotCode?: string | null;
  plateNumber: string;
  startAtUtc: string;
  endAtUtc: string;
  totalAmount: number;
  payments: PaymentRecord[];
}
export interface ParkingHistoryPage {
  items: ParkingHistoryItem[];
  page: number;
  pageSize: number;
  total: number;
  paymentsAvailable: boolean;
  summary: { totalBookings: number; completedBookings: number; totalSpent: number };
}

export type DataRequestType = 'Export' | 'Correction' | 'Deletion';
export interface DataRequestItem {
  id: number;
  type: string;
  status: string;
  createdAtUtc: string;
  dueAtUtc: string;
  reason?: string | null;
  resultNote?: string | null;
  completedAtUtc?: string | null;
}
export interface PersonalDataView {
  id: number;
  fullName: string;
  email?: string | null;
  emailConfirmed: boolean;
  phoneNumber?: string | null;
  phoneConfirmed: boolean;
  avatarUrl?: string | null;
  roles: string[];
  status: string;
  createdAtUtc: string;
  lastLoginAtUtc?: string | null;
  kycStatus: string;
  kycDocumentType?: string | null;
  kycMaskedNumber?: string | null;
  kycVerifiedAtUtc?: string | null;
  businessName?: string | null;
  activeSessions: number;
  recentSecurityEvents: { type: string; occurredAtUtc: string; ipAddress?: string | null }[];
}
