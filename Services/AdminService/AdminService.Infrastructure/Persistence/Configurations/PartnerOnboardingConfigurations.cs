using AdminService.Domain.Entities;
using AdminService.Domain.Enums;
using AdminService.Domain.ValueObjects;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AdminService.Infrastructure.Persistence.Configurations;

/// <summary>US-116: hồ sơ đối tác. Mã hồ sơ duy nhất; RowVersion chống hai Admin thao tác cùng lúc.</summary>
internal sealed class PartnerApplicationConfiguration : IEntityTypeConfiguration<PartnerApplication>
{
    public void Configure(EntityTypeBuilder<PartnerApplication> e)
    {
        e.ToTable("PartnerApplications");
        e.Property(x => x.Code).HasMaxLength(20).IsRequired();
        e.Property(x => x.FullName).HasMaxLength(200).IsRequired();
        e.Property(x => x.Email).HasMaxLength(254).IsRequired();
        e.Property(x => x.Phone).HasMaxLength(20).IsRequired();
        e.Property(x => x.Position).HasMaxLength(100);
        e.Property(x => x.BusinessName).HasMaxLength(300).IsRequired();
        e.Property(x => x.TaxCode).HasMaxLength(20).IsRequired();
        e.Property(x => x.LegalRepresentativeName).HasMaxLength(200).IsRequired();
        e.Property(x => x.LegalRepresentativePosition).HasMaxLength(100);
        e.Property(x => x.BusinessLicenseUrl).HasMaxLength(512);
        e.Property(x => x.OperatingHours).HasMaxLength(100);
        e.Property(x => x.InfoRequestNote).HasMaxLength(1000);
        e.Property(x => x.RejectReason).HasMaxLength(1000);
        e.Property(x => x.CancelReason).HasMaxLength(1000);
        e.Property(x => x.PayoutBankBin).HasMaxLength(10);
        e.Property(x => x.PayoutBankName).HasMaxLength(100);
        e.Property(x => x.PayoutBankAccountNumber).HasMaxLength(30);
        e.Property(x => x.PayoutBankAccountName).HasMaxLength(200);
        e.Property(x => x.RowVersion).IsConcurrencyToken();
        e.Ignore(x => x.PayoutBank);
        e.Ignore(x => x.IsTerminal);
        e.HasIndex(x => x.Code).IsUnique();
        e.HasIndex(x => x.Status);
        e.HasIndex(x => x.TaxCode);
        e.HasIndex(x => x.CreatedOwnerProfileId);
    }
}

/// <summary>US-116: mẫu hợp đồng (loại hợp đồng có tên) – mỗi Version một dòng; nhiều mẫu Active cùng lúc; Bên A theo từng mẫu.</summary>
internal sealed class ContractTemplateConfiguration : IEntityTypeConfiguration<ContractTemplate>
{
    public void Configure(EntityTypeBuilder<ContractTemplate> e)
    {
        e.ToTable("ContractTemplates");
        e.Property(x => x.Name).HasMaxLength(200).IsRequired();
        // Văn bản thường có placeholder, không phải JSON → text (IsMaxText() của ServiceDefaults là jsonb).
        e.Property(x => x.Body).HasColumnType("text").IsRequired();
        e.Property(x => x.ChangeNote).HasMaxLength(1000);
        // Nội dung form (TemplateContent) dạng JSON thật → jsonb.
        e.Property(x => x.ContentJson).HasColumnType("jsonb");
        e.Property(x => x.SourceFileKey).HasMaxLength(300);
        e.Property(x => x.SourceFileName).HasMaxLength(260);
        e.Property(x => x.SourceFileSha256).HasMaxLength(64);
        e.Property(x => x.ImportNotes).HasColumnType("text");
        e.Property(x => x.PlatformName).HasMaxLength(300).IsRequired();
        e.Property(x => x.PlatformAddress).HasMaxLength(300).IsRequired();
        e.Property(x => x.PlatformTaxCode).HasMaxLength(300).IsRequired();
        e.Property(x => x.PlatformPhone).HasMaxLength(300).IsRequired();
        e.Property(x => x.PlatformEmail).HasMaxLength(300).IsRequired();
        e.Property(x => x.PlatformRepresentativeName).HasMaxLength(300).IsRequired();
        e.Property(x => x.PlatformRepresentativePosition).HasMaxLength(300).IsRequired();
        e.Property(x => x.RowVersion).IsConcurrencyToken();
        e.Ignore(x => x.IsActive);
        e.Ignore(x => x.Platform);
        e.Ignore(x => x.SourceFile);
        e.HasIndex(x => x.Version).IsUnique();
        e.HasIndex(x => x.Status);
    }
}

/// <summary>
/// US-116: hợp đồng bãi – số hợp đồng duy nhất; mỗi bãi tối đa một hợp đồng chưa hủy; snapshot bãi + Bên B lúc lập.
/// </summary>
internal sealed class LotContractConfiguration : IEntityTypeConfiguration<LotContract>
{
    public void Configure(EntityTypeBuilder<LotContract> e)
    {
        e.ToTable("LotContracts");
        e.Property(x => x.ContractNumber).HasMaxLength(30).IsRequired();
        e.Property(x => x.LotName).HasMaxLength(200).IsRequired();
        e.Property(x => x.LotAddress).HasMaxLength(300).IsRequired();
        e.Property(x => x.LotCity).HasMaxLength(100).IsRequired();
        e.Property(x => x.LotDistrict).HasMaxLength(100);
        e.Property(x => x.LotOpeningHours).HasMaxLength(50).IsRequired();
        e.Property(x => x.ApplicationCode).HasMaxLength(20).IsRequired();
        e.Property(x => x.BusinessName).HasMaxLength(300).IsRequired();
        e.Property(x => x.TaxCode).HasMaxLength(20).IsRequired();
        e.Property(x => x.BusinessAddress).HasMaxLength(500).IsRequired();
        e.Property(x => x.LegalRepresentativeName).HasMaxLength(200).IsRequired();
        e.Property(x => x.LegalRepresentativePosition).HasMaxLength(100);
        e.Property(x => x.Email).HasMaxLength(254).IsRequired();
        e.Property(x => x.Phone).HasMaxLength(20).IsRequired();
        e.Property(x => x.BankBin).HasMaxLength(10).IsRequired();
        e.Property(x => x.BankName).HasMaxLength(100).IsRequired();
        e.Property(x => x.BankAccountNumber).HasMaxLength(30).IsRequired();
        e.Property(x => x.BankAccountName).HasMaxLength(200).IsRequired();
        e.Property(x => x.SignedFileKey).HasMaxLength(300);
        e.Property(x => x.SignedFileContentType).HasMaxLength(100);
        e.Property(x => x.SignedFileSha256).HasMaxLength(64);
        e.Property(x => x.CancelReason).HasMaxLength(1000);
        e.Property(x => x.RowVersion).IsConcurrencyToken();
        e.Ignore(x => x.CurrentVersion);
        e.Ignore(x => x.NextVersion);
        e.HasIndex(x => x.ContractNumber).IsUnique();
        e.HasIndex(x => x.ParkingLotId).IsUnique()
            .HasFilter($"\"Status\" <> '{nameof(LotContractStatus.Cancelled)}'")
            .HasDatabaseName("IX_LotContracts_SingleOpenPerLot");
        e.HasIndex(x => x.OwnerProfileId);
        e.HasIndex(x => x.Status);
        e.HasOne<PartnerApplication>().WithMany().HasForeignKey(x => x.PartnerApplicationId).OnDelete(DeleteBehavior.Restrict);
        e.HasMany(x => x.Versions).WithOne(x => x.LotContract).HasForeignKey(x => x.LotContractId);
    }
}

/// <summary>US-116: phiên bản hợp đồng bãi – (hợp đồng, version) duy nhất, mỗi hợp đồng tối đa một phiên bản Current.</summary>
internal sealed class LotContractVersionConfiguration : IEntityTypeConfiguration<LotContractVersion>
{
    public void Configure(EntityTypeBuilder<LotContractVersion> e)
    {
        e.ToTable("LotContractVersions");
        e.Property(x => x.CommissionRate).HasPrecision(5, 2);
        e.Property(x => x.FileKey).HasMaxLength(300).IsRequired();
        e.Property(x => x.FileContentType).HasMaxLength(100).IsRequired();
        e.Property(x => x.FileSha256).HasMaxLength(64).IsRequired();
        e.Property(x => x.RevisionReason).HasMaxLength(1000);
        e.Property(x => x.SpecialTerms).HasMaxLength(ContractTerms.MaxSpecialTermsLength);
        e.HasIndex(x => x.TemplateId);
        e.HasIndex(x => new { x.LotContractId, x.Version }).IsUnique();
        e.HasIndex(x => x.LotContractId).IsUnique()
            .HasFilter($"\"Status\" = '{nameof(LotContractVersionStatus.Current)}'")
            .HasDatabaseName("IX_LotContractVersions_SingleCurrent");
    }
}
