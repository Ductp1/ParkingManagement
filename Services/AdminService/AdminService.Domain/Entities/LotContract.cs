using AdminService.Domain.Enums;
using AdminService.Domain.ValueObjects;
using ParkingManagement.SharedKernel.Domain;
using ParkingManagement.SharedKernel.Exceptions;

namespace AdminService.Domain.Entities;

/// <summary>Thông tin bãi đỗ chụp lại lúc lập hợp đồng (dữ liệu gốc thuộc ParkingService).</summary>
public sealed record LotSnapshot(int ParkingLotId, int KybApplicationId, int OwnerProfileId, string Name, string Address, string City,
    string? District, int TotalSlots, string OpeningHours);

/// <summary>
/// [AdminService] Hợp đồng hợp tác khai thác MỘT bãi đỗ (US-116, mục 14). Lập sau khi KYB của bãi được duyệt;
/// ký xong thì AdminService gọi ParkingService kích hoạt bãi. Bên B được chụp lại từ hồ sơ đối tác (PartnerApplication),
/// bãi được chụp lại từ ParkingService – hợp đồng in ra không đổi dù dữ liệu gốc thay đổi sau này.
/// Pending → (revise v+1)* → Signed; Pending → Cancelled.
/// </summary>
public class LotContract : BaseEntity
{
    /// <summary>Số hợp đồng, duy nhất, giữ nguyên qua mọi phiên bản: HD-SP-2026-0003 (theo Id bãi; lập lại sau khi hủy thêm hậu tố -2, -3…).</summary>
    public string ContractNumber { get; set; } = string.Empty;
    public LotContractStatus Status { get; private set; } = LotContractStatus.Pending;

    // Bãi đỗ (→ ParkingService, không FK)
    public int ParkingLotId { get; set; }
    public int KybApplicationId { get; set; }
    public string LotName { get; set; } = string.Empty;
    public string LotAddress { get; set; } = string.Empty;
    public string LotCity { get; set; } = string.Empty;
    public string? LotDistrict { get; set; }
    public int LotTotalSlots { get; set; }
    public string LotOpeningHours { get; set; } = string.Empty;

    // Bên B (chụp từ hồ sơ đối tác)
    public int OwnerProfileId { get; set; }
    public int PartnerApplicationId { get; set; }
    public string ApplicationCode { get; set; } = string.Empty;
    public string BusinessName { get; set; } = string.Empty;
    public string TaxCode { get; set; } = string.Empty;
    public string BusinessAddress { get; set; } = string.Empty;
    public string LegalRepresentativeName { get; set; } = string.Empty;
    public string? LegalRepresentativePosition { get; set; }
    public string Email { get; set; } = string.Empty;
    public string Phone { get; set; } = string.Empty;
    public string BankBin { get; set; } = string.Empty;
    public string BankName { get; set; } = string.Empty;
    public string BankAccountNumber { get; set; } = string.Empty;
    public string BankAccountName { get; set; } = string.Empty;

    // Ký / hủy
    public string? SignedFileKey { get; set; }
    public string? SignedFileContentType { get; set; }
    public string? SignedFileSha256 { get; set; }
    public DateTime? SignedAtUtc { get; set; }
    public string? CancelReason { get; set; }

    /// <summary>→ UserService (không FK).</summary>
    public int CreatedByAdminId { get; set; }

    /// <summary>Optimistic concurrency (ServiceDefaults đóng dấu mỗi lần lưu).</summary>
    public byte[] RowVersion { get; set; } = [];

    public ICollection<LotContractVersion> Versions { get; set; } = new List<LotContractVersion>();

    public LotContractVersion? CurrentVersion => Versions.FirstOrDefault(v => v.Status == LotContractVersionStatus.Current);
    public int NextVersion => Versions.Count == 0 ? 1 : Versions.Max(v => v.Version) + 1;

    /// <summary>L1: hợp đồng mới (Pending, chưa có phiên bản) – gọi <see cref="AddFirstVersion"/> ngay sau đó.</summary>
    public static LotContract Open(string contractNumber, LotSnapshot lot, PartnerApplication partner, int adminUserId)
    {
        if (adminUserId <= 0) throw new ArgumentOutOfRangeException(nameof(adminUserId), "Admin không hợp lệ.");
        var bank = partner.PayoutBank
                   ?? throw new ConflictException($"Hồ sơ đối tác {partner.Code} chưa có tài khoản nhận thanh toán, chưa thể lập hợp đồng.");
        if (string.IsNullOrWhiteSpace(partner.LegalRepresentativeName))
            throw new ConflictException($"Hồ sơ đối tác {partner.Code} chưa có người đại diện pháp luật, chưa thể lập hợp đồng.");

        return new LotContract
        {
            ContractNumber = contractNumber,
            Status = LotContractStatus.Pending,
            ParkingLotId = lot.ParkingLotId,
            KybApplicationId = lot.KybApplicationId,
            LotName = lot.Name,
            LotAddress = lot.Address,
            LotCity = lot.City,
            LotDistrict = lot.District,
            LotTotalSlots = lot.TotalSlots,
            LotOpeningHours = lot.OpeningHours,
            OwnerProfileId = lot.OwnerProfileId,
            PartnerApplicationId = partner.Id,
            ApplicationCode = partner.Code,
            BusinessName = partner.BusinessName,
            TaxCode = partner.TaxCode,
            BusinessAddress = partner.BusinessAddress,
            LegalRepresentativeName = partner.LegalRepresentativeName,
            LegalRepresentativePosition = partner.LegalRepresentativePosition,
            Email = partner.Email,
            Phone = partner.Phone,
            BankBin = bank.Bin,
            BankName = bank.BankName,
            BankAccountNumber = bank.AccountNumber,
            BankAccountName = bank.AccountName,
            CreatedByAdminId = adminUserId,
        };
    }

    /// <summary>L1: phiên bản v1 (Current).</summary>
    public LotContractVersion AddFirstVersion(int adminUserId, ContractTerms terms, int templateVersion, StoredFile file)
    {
        if (Versions.Count > 0) throw new ConflictException($"Hợp đồng {ContractNumber} đã có phiên bản, hãy dùng chức năng sửa hợp đồng.");
        return AddVersion(adminUserId, terms, templateVersion, file, revisionReason: null);
    }

    /// <summary>L2: phiên bản v+1 cùng số hợp đồng; bản đang chờ ký → Superseded; lý do bắt buộc.</summary>
    public LotContractVersion Revise(int adminUserId, ContractTerms terms, int templateVersion, StoredFile file, string reason)
    {
        EnsurePending("sửa");
        var text = Required(reason, "Lý do sửa hợp đồng");
        foreach (var current in Versions.Where(v => v.Status == LotContractVersionStatus.Current))
            current.Status = LotContractVersionStatus.Superseded;
        return AddVersion(adminUserId, terms, templateVersion, file, text);
    }

    /// <summary>Trước khi gọi ParkingService (L3): đang Pending, có phiên bản Current và Admin xác nhận đúng phiên bản đó.</summary>
    public LotContractVersion EnsureCanSign(int? confirmedVersion)
    {
        EnsurePending("ký");
        var current = CurrentVersion ?? throw new ConflictException($"Hợp đồng {ContractNumber} chưa có phiên bản chờ ký.");
        if (confirmedVersion is not { } version || version <= 0)
            throw new ValidationException("Vui lòng xác nhận phiên bản hợp đồng đã ký (confirmedVersion).");
        if (version != current.Version)
            throw new ConflictException(
                $"Phiên bản hợp đồng hiện tại là v{current.Version}, không phải v{version}. Vui lòng kiểm tra lại bản đã ký.");
        return current;
    }

    /// <summary>L3: Pending → Signed sau khi ParkingService đã kích hoạt bãi; phiên bản Current → Signed.</summary>
    public void Sign(int adminUserId, int confirmedVersion, StoredFile signedScan, DateTime nowUtc)
    {
        if (adminUserId <= 0) throw new ArgumentOutOfRangeException(nameof(adminUserId), "Admin không hợp lệ.");
        var current = EnsureCanSign(confirmedVersion);
        current.Status = LotContractVersionStatus.Signed;
        SignedFileKey = signedScan.Key;
        SignedFileContentType = signedScan.ContentType;
        SignedFileSha256 = signedScan.Sha256;
        SignedAtUtc = nowUtc;
        Status = LotContractStatus.Signed;
    }

    /// <summary>L4: Pending → Cancelled (bãi vẫn chờ duyệt); phiên bản đang chờ ký → Superseded.</summary>
    public void Cancel(int adminUserId, string reason)
    {
        if (adminUserId <= 0) throw new ArgumentOutOfRangeException(nameof(adminUserId), "Admin không hợp lệ.");
        EnsurePending("hủy");
        CancelReason = Required(reason, "Lý do hủy");
        foreach (var current in Versions.Where(v => v.Status == LotContractVersionStatus.Current))
            current.Status = LotContractVersionStatus.Superseded;
        Status = LotContractStatus.Cancelled;
    }

    private LotContractVersion AddVersion(int adminUserId, ContractTerms terms, int templateVersion, StoredFile file, string? revisionReason)
    {
        if (adminUserId <= 0) throw new ArgumentOutOfRangeException(nameof(adminUserId), "Admin không hợp lệ.");
        var version = new LotContractVersion
        {
            LotContract = this,
            Version = NextVersion,
            TemplateVersion = templateVersion,
            CommissionRate = terms.CommissionRate,
            EffectiveFrom = terms.EffectiveFrom,
            EffectiveTo = terms.EffectiveTo,
            TermMonths = terms.TermMonths,
            FileKey = file.Key,
            FileContentType = file.ContentType,
            FileSha256 = file.Sha256,
            Status = LotContractVersionStatus.Current,
            RevisionReason = revisionReason,
            CreatedByAdminId = adminUserId,
        };
        Versions.Add(version);
        return version;
    }

    private void EnsurePending(string action)
    {
        if (Status != LotContractStatus.Pending)
            throw new ConflictException($"Không thể {action} hợp đồng {ContractNumber} đang ở trạng thái {Status}.");
    }

    private static string Required(string? value, string field)
    {
        if (string.IsNullOrWhiteSpace(value)) throw new ValidationException($"{field} không được để trống.");
        var text = value.Trim();
        if (text.Length > 1000) throw new ValidationException($"{field} tối đa 1000 ký tự.");
        return text;
    }
}

/// <summary>[AdminService] Một phiên bản hợp đồng bãi (v1 khi lập, v+1 khi sửa); file HTML lưu qua IFileStorage kèm SHA-256.</summary>
public class LotContractVersion : BaseEntity
{
    public int LotContractId { get; set; }
    public int Version { get; set; }
    public int TemplateVersion { get; set; }

    /// <summary>Phí nền tảng (%), 0–100.</summary>
    public decimal CommissionRate { get; set; }
    public DateOnly EffectiveFrom { get; set; }
    public DateOnly EffectiveTo { get; set; }
    public int TermMonths { get; set; }

    public string FileKey { get; set; } = string.Empty;
    public string FileContentType { get; set; } = string.Empty;
    public string FileSha256 { get; set; } = string.Empty;

    public LotContractVersionStatus Status { get; set; } = LotContractVersionStatus.Current;
    public string? RevisionReason { get; set; }
    /// <summary>→ UserService (không FK).</summary>
    public int CreatedByAdminId { get; set; }

    public LotContract LotContract { get; set; } = null!;
}
