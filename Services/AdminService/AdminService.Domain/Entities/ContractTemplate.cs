using ParkingManagement.SharedKernel.Domain;

namespace AdminService.Domain.Entities;

/// <summary>
/// [AdminService] Mẫu hợp đồng đối tác (US-116). Nội dung pháp lý do BA / pháp chế cung cấp; Body chứa placeholder
/// {{ContractNumber}}, {{BusinessName}}… được thay khi sinh hợp đồng (T5). Chỉ một mẫu IsActive tại một thời điểm.
/// </summary>
public class ContractTemplate : BaseEntity
{
    public int Version { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Body { get; set; } = string.Empty;
    public DateTime EffectiveFromUtc { get; set; }
    public bool IsActive { get; set; }
}
