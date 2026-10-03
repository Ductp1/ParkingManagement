namespace ParkingManagement.SharedKernel.Enums;

// ===== UserService =====
/// <summary>Sự kiện bảo mật cần lưu vết (US-002).</summary>
public enum SecurityEventType { LoginSucceeded = 1, LoginFailed = 2, OtpFailed = 3, AccountLocked = 4, AccountUnlocked = 5, PasswordChanged = 6, TokenRevoked = 7 }

/// <summary>Quyền của chủ thể dữ liệu theo Nghị định 13/2023 (US-007).</summary>
public enum DataRequestType { Access = 1, Correction = 2, Deletion = 3, WithdrawConsent = 4, Export = 5 }
public enum DataRequestStatus { Submitted = 0, InProgress = 1, Completed = 2, Rejected = 3 }

/// <summary>Trạng thái hợp tác của chủ bãi (US-057 rời nền tảng).</summary>
public enum OwnerStatus { Active = 1, Suspended = 2, Leaving = 3, Terminated = 4 }

// ===== VehicleService =====
public enum VehicleShareStatus { Pending = 0, Active = 1, Revoked = 2 }

// ===== ParkingService =====
public enum ClosureTargetType { Lot = 1, Zone = 2, Floor = 3, Slot = 4 }
public enum SlotStateSource { Booking = 1, Gate = 2, Sensor = 3, StaffOverride = 4, Pms = 5, System = 6 }
public enum IntegrationStatus { Pending = 0, Active = 1, Disabled = 2, Error = 3 }

// ===== BookingService =====
public enum BookingModificationType { ChangeTime = 1, ChangeVehicle = 2, Extend = 3, ChangeSlot = 4 }
public enum MonthlyPassStatus { PendingPayment = 0, Active = 1, Expired = 2, Cancelled = 3 }

// ===== PaymentService =====
public enum InvoiceType { ParkingFee = 1, PlatformCommission = 2 }
public enum VoucherStatus { Issued = 0, Redeemed = 1, Expired = 2, Revoked = 3 }

// ===== GateService =====
public enum GateDeviceType { AnprCamera = 1, Webcam = 2, Barrier = 3, Kiosk = 4, QrScanner = 5, SlotSensor = 6 }
public enum GateDeviceStatus { Online = 0, Offline = 1, Maintenance = 2 }

// ===== NotificationService =====
public enum DevicePlatform { Android = 1, Ios = 2, Web = 3 }

// ===== AdminService =====
public enum RiskSubjectType { User = 1, Staff = 2, ParkingLot = 3, OwnerProfile = 4 }
public enum RiskSeverity { Info = 1, Warning = 2, Critical = 3 }
public enum RiskFlagStatus { Open = 0, Investigating = 1, Resolved = 2, Dismissed = 3 }

// ===== SupportService =====
public enum ComplaintParty { Driver = 1, LotOwner = 2, Admin = 3, System = 4 }
