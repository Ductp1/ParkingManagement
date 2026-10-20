using AdminService.Domain.Entities;
using AdminService.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ParkingManagement.ServiceDefaults.Persistence;

namespace AdminService.Infrastructure.Persistence.Configurations;

/// <summary>US-116: hồ sơ đối tác. Mã hồ sơ và số hợp đồng duy nhất; RowVersion chống hai Admin thao tác cùng lúc.</summary>
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
        e.Property(x => x.ContractNumber).HasMaxLength(30);
        e.Property(x => x.RowVersion).IsConcurrencyToken();
        e.HasIndex(x => x.Code).IsUnique();
        e.HasIndex(x => x.ContractNumber).IsUnique().HasFilter("\"ContractNumber\" IS NOT NULL");
        e.HasIndex(x => x.Status);
        e.HasIndex(x => x.TaxCode);
        e.HasMany(x => x.ContractDrafts).WithOne(x => x.PartnerApplication).HasForeignKey(x => x.PartnerApplicationId);
    }
}

/// <summary>US-116: mẫu hợp đồng – mỗi Version một dòng, tối đa một mẫu đang dùng.</summary>
internal sealed class ContractTemplateConfiguration : IEntityTypeConfiguration<ContractTemplate>
{
    public void Configure(EntityTypeBuilder<ContractTemplate> e)
    {
        e.ToTable("ContractTemplates");
        e.Property(x => x.Name).HasMaxLength(200).IsRequired();
        // Văn bản thường có placeholder, không phải JSON → text (IsMaxText() của ServiceDefaults là jsonb).
        e.Property(x => x.Body).HasColumnType("text").IsRequired();
        e.HasIndex(x => x.Version).IsUnique();
        e.HasIndex(x => x.IsActive).IsUnique().HasFilter("\"IsActive\" = TRUE").HasDatabaseName("IX_ContractTemplates_SingleActive");
    }
}

/// <summary>US-116: phiên bản hợp đồng – (hồ sơ, version) duy nhất, mỗi hồ sơ tối đa một phiên bản Current.</summary>
internal sealed class ContractDraftConfiguration : IEntityTypeConfiguration<ContractDraft>
{
    public void Configure(EntityTypeBuilder<ContractDraft> e)
    {
        e.ToTable("ContractDrafts");
        e.Property(x => x.ContractNumber).HasMaxLength(30).IsRequired();
        e.Property(x => x.CommissionRate).HasPrecision(5, 2);
        e.Property(x => x.BankBin).HasMaxLength(10).IsRequired();
        e.Property(x => x.BankName).HasMaxLength(100).IsRequired();
        e.Property(x => x.BankAccountNumber).HasMaxLength(30).IsRequired();
        e.Property(x => x.BankAccountName).HasMaxLength(200).IsRequired();
        e.Property(x => x.FileKey).HasMaxLength(300).IsRequired();
        e.Property(x => x.FileContentType).HasMaxLength(100).IsRequired();
        e.Property(x => x.FileSha256).HasMaxLength(64).IsRequired();
        e.Property(x => x.RevisionReason).HasMaxLength(1000);
        e.Property(x => x.SentToEmail).HasMaxLength(254).IsRequired();
        e.HasIndex(x => new { x.PartnerApplicationId, x.Version }).IsUnique();
        e.HasIndex(x => x.PartnerApplicationId).IsUnique()
            .HasFilter($"\"Status\" = '{nameof(ContractDraftStatus.Current)}'")
            .HasDatabaseName("IX_ContractDrafts_SingleCurrent");
    }
}

/// <summary>US-116: hợp đồng đã ký – số hợp đồng duy nhất, tra theo chủ bãi.</summary>
internal sealed class OwnerContractConfiguration : IEntityTypeConfiguration<OwnerContract>
{
    public void Configure(EntityTypeBuilder<OwnerContract> e)
    {
        e.ToTable("OwnerContracts");
        e.Property(x => x.ContractNumber).HasMaxLength(30).IsRequired();
        e.Property(x => x.SignedFileKey).HasMaxLength(300).IsRequired();
        e.Property(x => x.SignedFileContentType).HasMaxLength(100).IsRequired();
        e.Property(x => x.SignedFileSha256).HasMaxLength(64).IsRequired();
        e.Property(x => x.CommissionRate).HasPrecision(5, 2);
        e.HasIndex(x => x.ContractNumber).IsUnique();
        e.HasIndex(x => x.OwnerProfileId);
        e.HasIndex(x => x.PartnerApplicationId).IsUnique();
        e.HasOne(x => x.PartnerApplication).WithMany().HasForeignKey(x => x.PartnerApplicationId);
        e.HasOne(x => x.ContractDraft).WithMany().HasForeignKey(x => x.ContractDraftId);
    }
}
