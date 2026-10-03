using Microsoft.EntityFrameworkCore;
using ParkingManagement.ServiceDefaults.Persistence;
using SupportService.Domain.Entities;

namespace SupportService.Infrastructure.Persistence;

/// <summary>Database riêng của SupportService: PM_SupportDb. Khiếu nại, tranh chấp 3 bên, đánh giá bãi.</summary>
public sealed class SupportDbContext(DbContextOptions<SupportDbContext> options) : ServiceDbContext(options)
{
    public DbSet<Complaint> Complaints => Set<Complaint>();
    public DbSet<Review> Reviews => Set<Review>();
    public DbSet<ComplaintMessage> ComplaintMessages => Set<ComplaintMessage>();
    public DbSet<FaqArticle> FaqArticles => Set<FaqArticle>();
}
