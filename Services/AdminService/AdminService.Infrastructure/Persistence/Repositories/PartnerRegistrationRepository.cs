using System.Globalization;
using AdminService.Application.Features.PartnerOnboarding;
using AdminService.Domain.Entities;
using AdminService.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using ParkingManagement.SharedKernel.Exceptions;

namespace AdminService.Infrastructure.Persistence.Repositories;

/// <summary>
/// US-053: lưu hồ sơ đăng ký đối tác của người dùng đã đăng nhập. Mã PA-{năm}-{4 số} tăng dần theo năm (không ngẫu nhiên);
/// hai hồ sơ nộp cùng lúc lấy trùng số thì unique index IX_PartnerApplications_Code chặn, lấy số kế tiếp và thử lại.
/// </summary>
public sealed class PartnerRegistrationRepository(AdminDbContext db) : IPartnerRegistrationRepository
{
    private const int MaxAttempts = 5;
    private const string CodeIndex = "IX_PartnerApplications_Code";

    private static readonly PartnerApplicationStatus[] Closed =
        [PartnerApplicationStatus.Activated, PartnerApplicationStatus.Rejected, PartnerApplicationStatus.Cancelled];

    public Task<bool> HasOpenApplicationAsync(int applicantUserId, string taxCode, string email, CancellationToken cancellationToken)
        => db.PartnerApplications.AsNoTracking()
            .AnyAsync(a => !Closed.Contains(a.Status)
                           && (a.ApplicantUserId == applicantUserId || a.TaxCode == taxCode || a.Email.ToLower() == email),
                cancellationToken);

    public async Task<IReadOnlyList<MyPartnerApplicationDto>> ListByApplicantAsync(int applicantUserId, CancellationToken cancellationToken)
        => await db.PartnerApplications.AsNoTracking()
            .Where(a => a.ApplicantUserId == applicantUserId)
            .OrderByDescending(a => a.CreatedAtUtc).ThenByDescending(a => a.Id)
            .Select(a => new MyPartnerApplicationDto(a.Code, a.Status.ToString(), a.BusinessName, a.TaxCode,
                db.PartnerApplicationDocuments.Count(d => d.PartnerApplicationId == a.Id),
                a.InfoRequestNote, a.RejectReason, a.CreatedAtUtc, a.UpdatedAtUtc))
            .ToListAsync(cancellationToken);

    public async Task<string> AddWithNewCodeAsync(PartnerApplication application, IReadOnlyList<PartnerApplicationDocument> documents,
        int year, CancellationToken cancellationToken, Func<PartnerApplication, AuditLog>? buildAuditLog = null)
    {
        db.PartnerApplications.Add(application);
        db.AddRange(documents);
        var prefix = $"PA-{year}-";
        for (var attempt = 1; ; attempt++)
        {
            application.Code = prefix + (await LastSequenceAsync(prefix, cancellationToken) + 1).ToString("0000", CultureInfo.InvariantCulture);
            // Hồ sơ + giấy tờ + audit log (cần Id hồ sơ) trong một transaction: audit lỗi thì hồ sơ cũng không được lưu.
            await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
            try
            {
                await db.SaveChangesAsync(cancellationToken);
                if (buildAuditLog is not null)
                {
                    db.AuditLogs.Add(buildAuditLog(application));
                    await db.SaveChangesAsync(cancellationToken);
                }
                await transaction.CommitAsync(cancellationToken);
                return application.Code;
            }
            catch (DbUpdateException ex) when (attempt < MaxAttempts
                                               && ex.InnerException is PostgresException
                                               {
                                                   SqlState: PostgresErrorCodes.UniqueViolation, ConstraintName: CodeIndex,
                                               })
            {
                // Người khác vừa lấy số này: hủy transaction, giữ entity trong change tracker (vẫn Added) và thử số kế tiếp.
                await transaction.RollbackAsync(cancellationToken);
            }
            catch (DbUpdateException ex) when (ex.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation })
            {
                await transaction.RollbackAsync(CancellationToken.None);
                Detach(application, documents);
                throw new ConflictException("Hệ thống đang bận, vui lòng gửi lại hồ sơ.");
            }
            catch
            {
                await transaction.RollbackAsync(CancellationToken.None);
                Detach(application, documents);
                throw;
            }
        }
    }

    private async Task<int> LastSequenceAsync(string prefix, CancellationToken cancellationToken)
    {
        var codes = await db.PartnerApplications.AsNoTracking()
            .Where(a => a.Code.StartsWith(prefix))
            .Select(a => a.Code)
            .ToListAsync(cancellationToken);
        return codes.Select(c => int.TryParse(c.AsSpan(prefix.Length), NumberStyles.None, CultureInfo.InvariantCulture, out var n) ? n : 0)
            .DefaultIfEmpty(0)
            .Max();
    }

    private void Detach(PartnerApplication application, IReadOnlyList<PartnerApplicationDocument> documents)
    {
        foreach (var document in documents) db.Entry(document).State = EntityState.Detached;
        db.Entry(application).State = EntityState.Detached;
    }
}
