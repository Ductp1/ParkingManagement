using AdminService.Domain.Enums;
using ParkingManagement.SharedKernel.Domain;

namespace AdminService.Domain.Entities;

/// <summary>
/// [AdminService] Hợp đồng đối tác đã ký (tạo ở T6 từ phiên bản hợp đồng hiện tại). Nguồn của tỷ lệ hoa hồng
/// (OwnerProfile.CommissionRateOverride bên UserService được đặt từ đây).
/// </summary>
public class OwnerContract : BaseEntity
{
    /// <summary>→ UserService (không FK).</summary>
    public int OwnerProfileId { get; set; }
    public int PartnerApplicationId { get; set; }
    public int ContractDraftId { get; set; }
    public string ContractNumber { get; set; } = string.Empty;
    public int DraftVersion { get; set; }

    public string SignedFileKey { get; set; } = string.Empty;
    public string SignedFileContentType { get; set; } = string.Empty;
    public string SignedFileSha256 { get; set; } = string.Empty;

    public decimal CommissionRate { get; set; }
    public DateOnly EffectiveFrom { get; set; }
    public DateOnly EffectiveTo { get; set; }
    public OwnerContractStatus Status { get; set; } = OwnerContractStatus.Active;
    /// <summary>→ UserService (không FK).</summary>
    public int CreatedByAdminId { get; set; }

    public PartnerApplication PartnerApplication { get; set; } = null!;
    public ContractDraft ContractDraft { get; set; } = null!;
}
