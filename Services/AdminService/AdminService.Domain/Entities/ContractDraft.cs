using AdminService.Domain.Enums;
using ParkingManagement.SharedKernel.Domain;

namespace AdminService.Domain.Entities;

/// <summary>
/// [AdminService] Một phiên bản hợp đồng tự sinh gửi đối tác (T5 = v1, T5b = v+1). Cùng ContractNumber qua mọi phiên bản;
/// file lưu qua IFileStorage, kèm SHA-256 để kiểm tra toàn vẹn khi tải lại.
/// </summary>
public class ContractDraft : BaseEntity
{
    public int PartnerApplicationId { get; set; }
    public string ContractNumber { get; set; } = string.Empty;
    public int Version { get; set; }
    public int TemplateVersion { get; set; }

    // Điều khoản
    /// <summary>Tỷ lệ hoa hồng (%), 0–100.</summary>
    public decimal CommissionRate { get; set; }
    public DateOnly EffectiveFrom { get; set; }
    public DateOnly EffectiveTo { get; set; }
    public int TermMonths { get; set; }

    // Tài khoản nhận tiền của đối tác
    public string BankBin { get; set; } = string.Empty;
    public string BankName { get; set; } = string.Empty;
    public string BankAccountNumber { get; set; } = string.Empty;
    public string BankAccountName { get; set; } = string.Empty;

    // File sinh ra
    public string FileKey { get; set; } = string.Empty;
    public string FileContentType { get; set; } = string.Empty;
    public string FileSha256 { get; set; } = string.Empty;

    public ContractDraftStatus Status { get; set; } = ContractDraftStatus.Current;
    public string? RevisionReason { get; set; }
    public DateTime SentAtUtc { get; set; }
    public string SentToEmail { get; set; } = string.Empty;
    /// <summary>→ UserService (không FK).</summary>
    public int CreatedByAdminId { get; set; }

    public PartnerApplication PartnerApplication { get; set; } = null!;
}
