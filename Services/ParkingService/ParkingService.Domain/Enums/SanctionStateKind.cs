namespace ParkingService.Domain.Enums;

/// <summary>
/// Hiệu lực của chế tài SLA (US-097) mà ParkingService phải áp khi tìm bãi / đặt chỗ.
/// RankingPenalty = cấp 2 (hạ thứ hạng tìm kiếm); ListingSuspension = cấp 3 theo bãi (ẩn khỏi tìm kiếm, chặn đặt chỗ mới).
/// </summary>
public enum SanctionStateKind { RankingPenalty = 2, ListingSuspension = 3 }
