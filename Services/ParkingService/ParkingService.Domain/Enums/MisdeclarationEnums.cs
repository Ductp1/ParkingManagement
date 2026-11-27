namespace ParkingService.Domain.Enums;

/// <summary>Mức độ khai báo sai (US-056): Minor = sai sót nhẹ, chỉ cảnh báo (AC3); Suspected = nghi khai sai/ảnh giả, tạm khóa (AC1).</summary>
public enum MisdeclarationSeverity { Minor = 1, Suspected = 2 }

/// <summary>Nội dung bị khai sai (US-056): địa chỉ, ảnh hiện trường, công suất, giấy tờ, khác.</summary>
public enum MisdeclarationCategory { Address = 1, Photos = 2, Capacity = 3, Documents = 4, Other = 9 }

/// <summary>
/// Trạng thái case khai báo sai (US-056). Warned / Suspended = đang mở; Resolved = đã xử lý xong (bãi Active);
/// Banned = xác nhận gian lận, bãi bị khóa vĩnh viễn (AC2).
/// </summary>
public enum MisdeclarationCaseStatus { Warned = 0, Suspended = 1, Resolved = 2, Banned = 3 }
