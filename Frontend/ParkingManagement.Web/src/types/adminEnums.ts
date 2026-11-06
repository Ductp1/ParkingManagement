/**
 * Enum hiển thị trong Admin Portal – nguồn duy nhất: BuildingBlocks/ParkingManagement.SharedKernel/Enums
 * (backend serialize enum dạng chuỗi bằng JsonStringEnumConverter). Enum riêng của AdminService ghi rõ nguồn.
 * Viết dạng const object + union type (tsconfig bật erasableSyntaxOnly nên không dùng `enum`).
 */
type ValueOf<T> = T[keyof T];

// LotEnums.cs
export const KybStatus = {
  Draft: 'Draft',
  Submitted: 'Submitted',
  UnderReview: 'UnderReview',
  Approved: 'Approved',
  Rejected: 'Rejected',
  NeedsMoreInfo: 'NeedsMoreInfo',
} as const;
export type KybStatus = ValueOf<typeof KybStatus>;

export const IntegrationTier = { Manual: 'Manual', PmsApi: 'PmsApi', EdgeIot: 'EdgeIot' } as const;
export type IntegrationTier = ValueOf<typeof IntegrationTier>;

// ParkingLotStatus.cs
export const ParkingLotStatus = { PendingApproval: 'PendingApproval', Active: 'Active', Suspended: 'Suspended' } as const;
export type ParkingLotStatus = ValueOf<typeof ParkingLotStatus>;

// IdentityEnums.cs
export const UserRoleType = { Driver: 'Driver', LotOwner: 'LotOwner', Staff: 'Staff', Admin: 'Admin' } as const;
export type UserRoleType = ValueOf<typeof UserRoleType>;

export const UserStatus = {
  PendingVerification: 'PendingVerification',
  Active: 'Active',
  Locked: 'Locked',
  Banned: 'Banned',
} as const;
export type UserStatus = ValueOf<typeof UserStatus>;

export const KycStatus = { NotSubmitted: 'NotSubmitted', Pending: 'Pending', Verified: 'Verified', Rejected: 'Rejected' } as const;
export type KycStatus = ValueOf<typeof KycStatus>;

// BookingEnums.cs
export const BookingStatus = {
  Created: 'Created',
  PendingPayment: 'PendingPayment',
  PendingOwnerApproval: 'PendingOwnerApproval',
  Confirmed: 'Confirmed',
  CheckedIn: 'CheckedIn',
  Parking: 'Parking',
  CheckedOut: 'CheckedOut',
  Completed: 'Completed',
  Expired: 'Expired',
  Cancelled: 'Cancelled',
  CancelledNoRefund: 'CancelledNoRefund',
  NoShow: 'NoShow',
  Rejected: 'Rejected',
} as const;
export type BookingStatus = ValueOf<typeof BookingStatus>;

// GateEnums.cs
export const ParkingSessionStatus = { Active: 'Active', Completed: 'Completed', Abnormal: 'Abnormal' } as const;
export type ParkingSessionStatus = ValueOf<typeof ParkingSessionStatus>;

export const ShiftStatus = { Open: 'Open', Closed: 'Closed', Flagged: 'Flagged' } as const;
export type ShiftStatus = ValueOf<typeof ShiftStatus>;

// ExtendedEnums.cs
export const GateDeviceType = {
  AnprCamera: 'AnprCamera',
  Webcam: 'Webcam',
  Barrier: 'Barrier',
  Kiosk: 'Kiosk',
  QrScanner: 'QrScanner',
  SlotSensor: 'SlotSensor',
} as const;
export type GateDeviceType = ValueOf<typeof GateDeviceType>;

export const GateDeviceStatus = { Online: 'Online', Offline: 'Offline', Maintenance: 'Maintenance' } as const;
export type GateDeviceStatus = ValueOf<typeof GateDeviceStatus>;

export const SecurityEventType = {
  LoginSucceeded: 'LoginSucceeded',
  LoginFailed: 'LoginFailed',
  OtpFailed: 'OtpFailed',
  AccountLocked: 'AccountLocked',
  AccountUnlocked: 'AccountUnlocked',
  PasswordChanged: 'PasswordChanged',
  TokenRevoked: 'TokenRevoked',
} as const;
export type SecurityEventType = ValueOf<typeof SecurityEventType>;

export const DataRequestType = {
  Access: 'Access',
  Correction: 'Correction',
  Deletion: 'Deletion',
  WithdrawConsent: 'WithdrawConsent',
  Export: 'Export',
} as const;
export type DataRequestType = ValueOf<typeof DataRequestType>;

export const DataRequestStatus = { Submitted: 'Submitted', InProgress: 'InProgress', Completed: 'Completed', Rejected: 'Rejected' } as const;
export type DataRequestStatus = ValueOf<typeof DataRequestStatus>;

export const OwnerStatus = { Active: 'Active', Suspended: 'Suspended', Leaving: 'Leaving', Terminated: 'Terminated' } as const;
export type OwnerStatus = ValueOf<typeof OwnerStatus>;

export const VoucherStatus = { Issued: 'Issued', Redeemed: 'Redeemed', Expired: 'Expired', Revoked: 'Revoked' } as const;
export type VoucherStatus = ValueOf<typeof VoucherStatus>;

export const RiskSeverity = { Info: 'Info', Warning: 'Warning', Critical: 'Critical' } as const;
export type RiskSeverity = ValueOf<typeof RiskSeverity>;

export const RiskFlagStatus = { Open: 'Open', Investigating: 'Investigating', Resolved: 'Resolved', Dismissed: 'Dismissed' } as const;
export type RiskFlagStatus = ValueOf<typeof RiskFlagStatus>;

// OperationEnums.cs
export const NotificationChannel = { InApp: 'InApp', Email: 'Email', Sms: 'Sms', Push: 'Push' } as const;
export type NotificationChannel = ValueOf<typeof NotificationChannel>;

export const NotificationStatus = { Pending: 'Pending', Sent: 'Sent', Failed: 'Failed' } as const;
export type NotificationStatus = ValueOf<typeof NotificationStatus>;

export const SanctionLevel = {
  Warning: 'Warning',
  RankingPenalty: 'RankingPenalty',
  TemporarySuspension: 'TemporarySuspension',
  PermanentBan: 'PermanentBan',
} as const;
export type SanctionLevel = ValueOf<typeof SanctionLevel>;

export const SanctionStatus = { Active: 'Active', Expired: 'Expired', Revoked: 'Revoked' } as const;
export type SanctionStatus = ValueOf<typeof SanctionStatus>;

export const SettlementStatus = { Draft: 'Draft', PendingReview: 'PendingReview', Approved: 'Approved', Paid: 'Paid' } as const;
export type SettlementStatus = ValueOf<typeof SettlementStatus>;

export const ComplaintCategory = {
  LotFull: 'LotFull',
  Overcharge: 'Overcharge',
  RefundRequest: 'RefundRequest',
  Damage: 'Damage',
  StaffBehavior: 'StaffBehavior',
  AppError: 'AppError',
  Other: 'Other',
} as const;
export type ComplaintCategory = ValueOf<typeof ComplaintCategory>;

export const ComplaintStatus = {
  Open: 'Open',
  AwaitingOwner: 'AwaitingOwner',
  OwnerResponded: 'OwnerResponded',
  Escalated: 'Escalated',
  Resolved: 'Resolved',
  Rejected: 'Rejected',
} as const;
export type ComplaintStatus = ValueOf<typeof ComplaintStatus>;

// PaymentEnums.cs
export const PaymentMethod = { Vnpay: 'Vnpay', Momo: 'Momo', VietQr: 'VietQr', Cash: 'Cash' } as const;
export type PaymentMethod = ValueOf<typeof PaymentMethod>;

export const PaymentStatus = {
  Pending: 'Pending',
  Succeeded: 'Succeeded',
  Failed: 'Failed',
  Cancelled: 'Cancelled',
  Refunded: 'Refunded',
  PartiallyRefunded: 'PartiallyRefunded',
} as const;
export type PaymentStatus = ValueOf<typeof PaymentStatus>;

export const RefundStatus = {
  Requested: 'Requested',
  Approved: 'Approved',
  Processing: 'Processing',
  Completed: 'Completed',
  Rejected: 'Rejected',
  Failed: 'Failed',
} as const;
export type RefundStatus = ValueOf<typeof RefundStatus>;

export const RefundReason = {
  CustomerCancel: 'CustomerCancel',
  LotFault: 'LotFault',
  PaymentBookingMismatch: 'PaymentBookingMismatch',
  DisputeOverride: 'DisputeOverride',
  EmergencyClosure: 'EmergencyClosure',
  Other: 'Other',
} as const;
export type RefundReason = ValueOf<typeof RefundReason>;

export const DiscountType = { Percentage: 'Percentage', FixedAmount: 'FixedAmount' } as const;
export type DiscountType = ValueOf<typeof DiscountType>;

export const PromotionSponsor = { LotOwner: 'LotOwner', Platform: 'Platform' } as const;
export type PromotionSponsor = ValueOf<typeof PromotionSponsor>;

// AdminService.Domain – SystemConfigChangeStatus (US-098)
export const SystemConfigChangeStatus = {
  Scheduled: 'Scheduled',
  Effective: 'Effective',
  Superseded: 'Superseded',
  Cancelled: 'Cancelled',
} as const;
export type SystemConfigChangeStatus = ValueOf<typeof SystemConfigChangeStatus>;

// AdminService.Domain – SanctionSyncStatus (US-096)
export const SanctionSyncStatus = { NotRequired: 'NotRequired', Pending: 'Pending', Synced: 'Synced', Failed: 'Failed' } as const;
export type SanctionSyncStatus = ValueOf<typeof SanctionSyncStatus>;

// AdminService.Domain/Enums/PartnerOnboardingEnums.cs (US-116)
export const PartnerApplicationStatus = {
  Submitted: 'Submitted',
  UnderReview: 'UnderReview',
  NeedsMoreInfo: 'NeedsMoreInfo',
  AccountIssued: 'AccountIssued',
  Activated: 'Activated',
  Rejected: 'Rejected',
  Cancelled: 'Cancelled',
} as const;
export type PartnerApplicationStatus = ValueOf<typeof PartnerApplicationStatus>;

export const LotContractStatus = { Pending: 'Pending', Signed: 'Signed', Cancelled: 'Cancelled', Terminated: 'Terminated' } as const;
export type LotContractStatus = ValueOf<typeof LotContractStatus>;

export const LotContractVersionStatus = { Current: 'Current', Superseded: 'Superseded', Signed: 'Signed' } as const;
export type LotContractVersionStatus = ValueOf<typeof LotContractVersionStatus>;
