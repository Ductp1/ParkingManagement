namespace ParkingManagement.SharedKernel.Enums;

public enum PaymentMethod { Vnpay = 1, Momo = 2, VietQr = 3, Cash = 4 }

public enum PaymentStatus { Pending = 0, Succeeded = 1, Failed = 2, Cancelled = 3, Refunded = 4, PartiallyRefunded = 5 }

public enum PaymentPurpose { Booking = 1, Extension = 2, Overstay = 3, WalkIn = 4, MonthlyPass = 5 }

public enum RefundStatus { Requested = 0, Approved = 1, Processing = 2, Completed = 3, Rejected = 4, Failed = 5 }

public enum RefundReason { CustomerCancel = 1, LotFault = 2, PaymentBookingMismatch = 3, DisputeOverride = 4, EmergencyClosure = 5, Other = 9 }

public enum DiscountType { Percentage = 1, FixedAmount = 2 }

/// <summary>Bên tài trợ khuyến mãi – quyết định trừ vào doanh thu ai (Nghiệp vụ v3 §1.3).</summary>
public enum PromotionSponsor { LotOwner = 1, Platform = 2 }
