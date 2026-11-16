using System.Globalization;
using AdminService.Application.Features.PartnerOnboarding;
using AdminService.Domain.Entities;
using AdminService.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using ParkingManagement.SharedKernel.Exceptions;

namespace AdminService.Infrastructure.Persistence.Repositories;

/// <summary>
/// US-053: lưu hồ sơ đăng ký đối tác từ form public. Mã PA-{năm}-{4 số} tăng dần theo năm (không ngẫu nhiên);
/// hai hồ sơ nộp cùng lúc lấy trùng số thì unique index IX_PartnerApplications_Code chặn, lấy số kế tiếp và thử lại.
/// </summary>
public sealed class PartnerRegistrationRepository(AdminDbContext db) : IPartnerRegistrationRepository
{
    private const int MaxAttempts = 5;
    private const string CodeIndex = "IX_PartnerApplications_Code";

    private static readonly PartnerApplicationStatus[] Closed =
        [PartnerApplicationStatus.Activated, PartnerApplicationStatus.Rejected, PartnerApplicationStatus.Cancelled];

    public Task<bool> HasOpenApplicationAsync(string taxCode, string email, CancellationToken cancellationToken)
        => db.PartnerApplications.AsNoTracking()
            .AnyAsync(a => !Closed.Contains(a.Status) && (a.TaxCode == taxCode || a.Email.ToLower() == email), cancellationToken);

    public async Task<string> AddWithNewCodeAsync(PartnerApplication application, IReadOnlyList<PartnerApplicationDocument> documents,
        int year, CancellationToken cancellationToken)
    {
        db.PartnerApplications.Add(application);
        db.AddRange(documents);
        var prefix = $"PA-{year}-";
        for (var attempt = 1; ; attempt++)
        {
            application.Code = prefix + (await LastSequenceAsync(prefix, cancellationToken) + 1).ToString("0000", CultureInfo.InvariantCulture);
            try
            {
                await db.SaveChangesAsync(cancellationToken);
                return application.Code;
            }
            catch (DbUpdateException ex) when (attempt < MaxAttempts
                                               && ex.InnerException is PostgresException
                                               {
                                                   SqlState: PostgresErrorCodes.UniqueViolation, ConstraintName: CodeIndex,
                                               })
            {
                // Người khác vừa lấy số này: giữ entity trong change tracker (vẫn Added) và thử số kế tiếp.
            }
            catch (DbUpdateException ex) when (ex.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation })
            {
                Detach(application, documents);
                throw new ConflictException("Hệ thống đang bận, vui lòng gửi lại hồ sơ.");
            }
            catch
            {
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
