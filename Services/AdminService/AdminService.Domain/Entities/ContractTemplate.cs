using AdminService.Domain.Enums;
using ParkingManagement.SharedKernel.Domain;
using ParkingManagement.SharedKernel.Exceptions;

namespace AdminService.Domain.Entities;

/// <summary>Thông tin Bên A (nền tảng) in vào hợp đồng bãi qua placeholder {{Platform…}}; đi theo từng mẫu.</summary>
public sealed record PlatformParty(string Name, string Address, string TaxCode, string Phone, string Email, string RepresentativeName,
    string RepresentativePosition);

/// <summary>File DOCX gốc của mẫu (nhập từ file).</summary>
public sealed record TemplateSourceFile(string Key, string FileName, string Sha256);

/// <summary>
/// [AdminService] Mẫu hợp đồng bãi = một "loại hợp đồng" có tên do Admin đặt (US-116). Version chỉ là số nội bộ (hợp đồng bãi lưu kèm).
/// Kind: Structured (form → Body sinh tự động), Markup (mẫu cũ v1–v4, Body gõ tay) hoặc Document (file DOCX gốc).
/// Vòng đời: Draft (sửa được) → Active (chọn được khi lập hợp đồng; nhiều mẫu Active cùng lúc) → Retired.
/// Hợp đồng đã sinh giữ file + TemplateVersion của nó, không bị ảnh hưởng khi đổi mẫu.
/// </summary>
public class ContractTemplate : BaseEntity
{
    public int Version { get; set; }
    public string Name { get; set; } = string.Empty;
    public ContractTemplateKind Kind { get; set; } = ContractTemplateKind.Structured;
    /// <summary>Markup cho renderer: sinh từ ContentJson (Structured) hoặc gõ tay (Markup); rỗng với Document.</summary>
    public string Body { get; set; } = string.Empty;
    /// <summary>TemplateContent dạng JSON (chỉ Structured).</summary>
    public string? ContentJson { get; set; }
    public ContractTemplateStatus Status { get; set; } = ContractTemplateStatus.Draft;
    /// <summary>Ghi chú thay đổi so với mẫu nguồn (VD "Đổi địa chỉ trụ sở Bên A").</summary>
    public string? ChangeNote { get; set; }
    public DateTime EffectiveFromUtc { get; set; }
    public DateTime? ActivatedAtUtc { get; set; }
    /// <summary>→ UserService (không FK); null với mẫu seed.</summary>
    public int? CreatedByAdminId { get; set; }
    /// <summary>Mẫu được sao chép / làm phiên bản mới (tùy chọn thôi áp dụng mẫu này khi áp dụng bản nháp).</summary>
    public int? SourceTemplateId { get; set; }

    // File DOCX gốc (mẫu nhập từ file)
    public string? SourceFileKey { get; set; }
    public string? SourceFileName { get; set; }
    public string? SourceFileSha256 { get; set; }
    /// <summary>Cảnh báo của AI / bộ tách khi nhập file (mỗi dòng một cảnh báo).</summary>
    public string? ImportNotes { get; set; }

    // Bên A
    public string PlatformName { get; set; } = string.Empty;
    public string PlatformAddress { get; set; } = string.Empty;
    public string PlatformTaxCode { get; set; } = string.Empty;
    public string PlatformPhone { get; set; } = string.Empty;
    public string PlatformEmail { get; set; } = string.Empty;
    public string PlatformRepresentativeName { get; set; } = string.Empty;
    public string PlatformRepresentativePosition { get; set; } = string.Empty;

    /// <summary>Optimistic concurrency (ServiceDefaults đóng dấu mỗi lần lưu).</summary>
    public byte[] RowVersion { get; set; } = [];

    public bool IsActive => Status == ContractTemplateStatus.Active;

    public PlatformParty Platform => new(PlatformName, PlatformAddress, PlatformTaxCode, PlatformPhone, PlatformEmail,
        PlatformRepresentativeName, PlatformRepresentativePosition);

    public TemplateSourceFile? SourceFile => SourceFileKey is null ? null : new(SourceFileKey, SourceFileName ?? "contract.docx", SourceFileSha256 ?? "");

    /// <summary>Mẫu mới (Draft) – nội dung đặt sau qua <see cref="UpdateDraft"/>.</summary>
    public static ContractTemplate CreateDraft(int version, string name, ContractTemplateKind kind, int adminUserId, int? sourceTemplateId = null)
    {
        if (adminUserId <= 0) throw new ArgumentOutOfRangeException(nameof(adminUserId), "Admin không hợp lệ.");
        return new ContractTemplate
        {
            Version = version,
            Name = name,
            Kind = kind,
            Status = ContractTemplateStatus.Draft,
            CreatedByAdminId = adminUserId,
            SourceTemplateId = sourceTemplateId,
        };
    }

    /// <summary>Bản nháp chép toàn bộ nội dung + Bên A + file gốc từ <paramref name="source"/>.</summary>
    public static ContractTemplate CreateDraft(int version, string name, ContractTemplate source, int adminUserId)
    {
        var draft = CreateDraft(version, name, source.Kind, adminUserId, source.Id);
        draft.Body = source.Body;
        draft.ContentJson = source.ContentJson;
        draft.SetSourceFile(source.SourceFile);
        draft.SetPlatform(source.Platform);
        return draft;
    }

    public void UpdateDraft(string name, string? changeNote, ContractTemplateKind kind, string body, string? contentJson, PlatformParty platform)
    {
        EnsureDraft("sửa");
        if (kind == ContractTemplateKind.Document && SourceFileKey is null)
            throw new ConflictException($"Mẫu \"{Name}\" chưa có file DOCX gốc, không thể dùng loại Document.");
        Name = name;
        ChangeNote = changeNote;
        Kind = kind;
        Body = body;
        ContentJson = contentJson;
        SetPlatform(platform);
    }

    /// <summary>Đổi tên – được ở mọi trạng thái (chỉ tên, không đổi nội dung).</summary>
    public void Rename(string name) => Name = name;

    public void SetSourceFile(TemplateSourceFile? file)
    {
        SourceFileKey = file?.Key;
        SourceFileName = file?.FileName;
        SourceFileSha256 = file?.Sha256;
    }

    /// <summary>Draft → Active. Không ảnh hưởng mẫu Active khác (nhiều loại hợp đồng cùng áp dụng).</summary>
    public void Activate(DateTime nowUtc)
    {
        EnsureDraft("áp dụng");
        Status = ContractTemplateStatus.Active;
        ActivatedAtUtc = nowUtc;
        EffectiveFromUtc = nowUtc;
    }

    public void Retire()
    {
        if (Status != ContractTemplateStatus.Active)
            throw new ConflictException($"Chỉ thôi áp dụng được mẫu đang Active (mẫu \"{Name}\" đang {Status}).");
        Status = ContractTemplateStatus.Retired;
    }

    public void EnsureDraft(string action)
    {
        if (Status != ContractTemplateStatus.Draft)
            throw new ConflictException($"Chỉ {action} được mẫu ở trạng thái Draft (mẫu \"{Name}\" đang {Status}).");
    }

    private void SetPlatform(PlatformParty platform)
    {
        PlatformName = platform.Name;
        PlatformAddress = platform.Address;
        PlatformTaxCode = platform.TaxCode;
        PlatformPhone = platform.Phone;
        PlatformEmail = platform.Email;
        PlatformRepresentativeName = platform.RepresentativeName;
        PlatformRepresentativePosition = platform.RepresentativePosition;
    }
}
