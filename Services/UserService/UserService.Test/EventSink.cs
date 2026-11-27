using ParkingManagement.SharedKernel.Contracts;
using ParkingManagement.SharedKernel.Enums;
using UserService.Application.Features.Identity;
using UserService.Domain.Entities;

namespace UserService.Test;

/// <summary>IIdentityStore cho use case chỉ phát sự kiện (khóa / chấm dứt chủ bãi).</summary>
internal sealed class EventSink : IIdentityStore
{
    public List<IntegrationEvent> Events = [];
    public void Event(IntegrationEvent integrationEvent) => Events.Add(integrationEvent);
    public Task<IIdentityTransaction> BeginAsync(CancellationToken ct) => throw new NotSupportedException();
    public Task<User?> FindContactAsync(string contact, CancellationToken ct) => throw new NotSupportedException();
    public Task<User?> FindUserAsync(int id, bool forUpdate, CancellationToken ct) => throw new NotSupportedException();
    public Task<OtpCode?> LatestOtpAsync(int userId, OtpPurpose purpose, CancellationToken ct) => throw new NotSupportedException();
    public Task<IReadOnlyList<OtpCode>> PendingOtpsAsync(int userId, OtpPurpose purpose, CancellationToken ct) => throw new NotSupportedException();
    public Task<int> OtpFailuresAsync(int userId, DateTime since, CancellationToken ct) => throw new NotSupportedException();
    public Task RevokeSessionsAsync(int userId, DateTime now, CancellationToken ct) => throw new NotSupportedException();
    public void Add(object entity) => throw new NotSupportedException();
    public Task SaveAsync(CancellationToken ct) => throw new NotSupportedException();
}
