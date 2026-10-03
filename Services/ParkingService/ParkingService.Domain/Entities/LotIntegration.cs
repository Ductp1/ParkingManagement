using ParkingManagement.SharedKernel.Domain;
using ParkingManagement.SharedKernel.Enums;

namespace ParkingService.Domain.Entities;

/// <summary>
/// [ParkingService] Kết nối phần mềm quản lý bãi (PMS) – Mức 1 (US-052, UC-51, Kiến trúc v3 §2.1).
/// Nền tảng đẩy booking mới qua WebhookUrl; PMS gọi API của nền tảng bằng API key để báo số chỗ trống và xe vào/ra.
/// Chỉ lưu HASH của API key và secret, không lưu bản gốc.
/// </summary>
public class LotIntegration : BaseEntity
{
    public int ParkingLotId { get; set; }
    /// <summary>Tên hệ thống PMS của bãi, VD "ParkPro 3.2".</summary>
    public string ProviderName { get; set; } = string.Empty;
    public string ApiKeyPrefix { get; set; } = string.Empty;
    public string ApiKeyHash { get; set; } = string.Empty;
    public string? WebhookUrl { get; set; }
    public string? WebhookSecretHash { get; set; }
    public IntegrationStatus Status { get; set; } = IntegrationStatus.Pending;
    public DateTime? LastSyncAtUtc { get; set; }
    public string? LastError { get; set; }

    public ParkingLot ParkingLot { get; set; } = null!;
}
