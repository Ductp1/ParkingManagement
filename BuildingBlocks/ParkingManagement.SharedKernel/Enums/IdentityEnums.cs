namespace ParkingManagement.SharedKernel.Enums;

/// <summary>4 vai trò hệ thống (Nghiệp vụ v3 §2.1). Một user có thể mang nhiều role.</summary>
public enum UserRoleType { Driver = 1, LotOwner = 2, Staff = 3, Admin = 4 }

public enum UserStatus { PendingVerification = 0, Active = 1, Locked = 2, Banned = 3 }

/// <summary>Trạng thái xác thực CCCD/GPLX (Nghị định 13/2023).</summary>
public enum KycStatus { NotSubmitted = 0, Pending = 1, Verified = 2, Rejected = 3 }

public enum KycDocumentType { CitizenId = 1, DriverLicense = 2 }

/// <summary>OwnerActivation (US-116 U2–U5): link kích hoạt tài khoản chủ bãi do Admin cấp – lưu SHA-256 của token, không phải mã 6 số.</summary>
public enum OtpPurpose { Register = 1, ResetPassword = 2, ChangeContact = 3, Login = 4, OwnerActivation = 5 }
