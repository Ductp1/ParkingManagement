namespace ParkingManagement.SharedKernel.Enums;

public enum NotificationChannel { InApp = 1, Email = 2, Sms = 3, Push = 4 }

public enum NotificationStatus { Pending = 0, Sent = 1, Failed = 2 }

/// <summary>Khung chế tài SLA 4 cấp (Nghiệp vụ v3 §4.1).</summary>
public enum SanctionLevel { Warning = 1, RankingPenalty = 2, TemporarySuspension = 3, PermanentBan = 4 }

public enum SanctionStatus { Active = 0, Expired = 1, Revoked = 2 }

public enum SettlementStatus { Draft = 0, PendingReview = 1, Approved = 2, Paid = 3 }

public enum SettlementLineType { Booking = 1, WalkIn = 2, Refund = 3, Adjustment = 4, DisputeHold = 5 }

public enum ComplaintCategory { LotFull = 1, Overcharge = 2, RefundRequest = 3, Damage = 4, StaffBehavior = 5, AppError = 6, Other = 9 }

public enum ComplaintStatus { Open = 0, AwaitingOwner = 1, OwnerResponded = 2, Escalated = 3, Resolved = 4, Rejected = 5 }
