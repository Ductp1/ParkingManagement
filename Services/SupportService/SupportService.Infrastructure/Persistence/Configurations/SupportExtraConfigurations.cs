using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ParkingManagement.ServiceDefaults.Persistence;
using SupportService.Domain.Entities;

namespace SupportService.Infrastructure.Persistence.Configurations;

internal sealed class ComplaintMessageConfiguration : IEntityTypeConfiguration<ComplaintMessage>
{
    public void Configure(EntityTypeBuilder<ComplaintMessage> e)
    {
        e.ToTable("ComplaintMessages");
        e.Property(x => x.Message).HasMaxLength(4000).IsRequired();
        e.Property(x => x.AttachmentUrlsJson).IsMaxText();
        e.HasOne(x => x.Complaint).WithMany(c => c.Messages).HasForeignKey(x => x.ComplaintId);
        e.HasIndex(x => new { x.ComplaintId, x.CreatedAtUtc });
    }
}

internal sealed class FaqArticleConfiguration : IEntityTypeConfiguration<FaqArticle>
{
    public void Configure(EntityTypeBuilder<FaqArticle> e)
    {
        e.ToTable("FaqArticles");
        e.Property(x => x.Category).HasMaxLength(50).IsRequired();
        e.Property(x => x.Question).HasMaxLength(500).IsRequired();
        e.Property(x => x.Answer).HasMaxLength(4000).IsRequired();
        e.Property(x => x.Audience).HasMaxLength(20).IsRequired();
        e.HasIndex(x => new { x.IsPublished, x.Category, x.SortOrder });
    }
}
