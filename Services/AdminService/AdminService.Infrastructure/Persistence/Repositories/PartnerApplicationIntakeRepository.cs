using AdminService.Application.Features.PartnerOnboarding;
using AdminService.Domain.Entities;
using AdminService.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using ParkingManagement.SharedKernel.Exceptions;

namespace AdminService.Infrastructure.Persistence.Repositories;

/// <summary>US-116 T1: tạo hồ sơ đối tác mới (form public hoặc Admin nhập thay) – mã PA-{năm}-NNNN + audit log cùng 1 transaction.</summary>
public sealed class PartnerApplicationIntakeRepository(AdminDbContext db) : IPartnerApplicationIntakeRepository
{
    /// <summary>Unique index PartnerApplications.Code – 2 hồ sơ nộp cùng lúc có thể lấy cùng mã → thử lại với mã kế tiếp.</summary>
    private const int MaxCodeAttempts = 5;

    private static readonly PartnerApplicationStatus[] Closed =
        [PartnerApplicationStatus.Activated, PartnerApplicationStatus.Rejected, PartnerApplicationStatus.Cancelled];

    public async Task<OpenApplicationMatch?> FindOpenDuplicateAsync(string email, string phone, string taxCode,
        CancellationToken cancellationToken)
    {
        var lowerEmail = email.ToLowerInvariant();
        var match = await db.PartnerApplications.AsNoTracking()
            .Where(a => !Closed.Contains(a.Status)
                        && (a.Email.ToLower() == lowerEmail || a.Phone == phone || a.TaxCode == taxCode))
            .OrderBy(a => a.Id)
            .Select(a => new { a.Code, a.Email, a.Phone, a.TaxCode })
            .FirstOrDefaultAsync(cancellationToken);
        return match is null
            ? null
            : new OpenApplicationMatch(match.Code, match.Email.ToLower() == lowerEmail, match.Phone == phone, match.TaxCode == taxCode);
    }

    public async Task AddSubmittedAsync(PartnerApplication application, int year, Func<PartnerApplication, AuditLog> buildAuditLog,
        CancellationToken cancellationToken)
    {
        var prefix = PartnerApplicationCode.Prefix(year);
        for (var attempt = 1; ; attempt++)
        {
            var last = await db.PartnerApplications.AsNoTracking()
                .Where(a => a.Code.StartsWith(prefix))
                .OrderByDescending(a => a.Code)
                .Select(a => a.Code)
                .FirstOrDefaultAsync(cancellationToken);
            application.Code = PartnerApplicationCode.Next(year, last);

            await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
            try
            {
                db.PartnerApplications.Add(application);
                await db.SaveChangesAsync(cancellationToken);              // sinh Id cho audit log

                db.AuditLogs.Add(buildAuditLog(application));
                await db.SaveChangesAsync(cancellationToken);

                await transaction.CommitAsync(cancellationToken);
                return;
            }
            catch (DbUpdateException ex) when (ex.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation })
            {
                await transaction.RollbackAsync(cancellationToken);
                foreach (var entry in db.ChangeTracker.Entries().ToList()) entry.State = EntityState.Detached;
                if (attempt >= MaxCodeAttempts)
                    throw new ConflictException("Hệ thống đang nhận nhiều hồ sơ cùng lúc, vui lòng gửi lại sau ít phút.");
            }
        }
    }
}
