using ParkingManagement.SharedKernel.Contracts;
using ParkingManagement.SharedKernel.Enums;
using ParkingManagement.SharedKernel.Exceptions;
using UserService.Application.Features.Users;

namespace UserService.Test;

public class UserUseCaseTests
{
    [Theory]
    [InlineData(0, 20)]
    [InlineData(1, 0)]
    [InlineData(1, 101)]
    public async Task Invalid_paging_is_rejected(int page, int pageSize)
        => await Assert.ThrowsAsync<ValidationException>(() => new ListUsersUseCase(new FakeQueries()).ExecuteAsync(null, page, pageSize));

    [Fact]
    public async Task Missing_user_returns_not_found()
        => await Assert.ThrowsAsync<NotFoundException>(() => new GetUserByIdUseCase(new FakeQueries()).ExecuteAsync(99));

    private sealed class FakeQueries : IUserQueries
    {
        public Task<UserDto?> GetByIdAsync(int id, CancellationToken cancellationToken) => Task.FromResult<UserDto?>(null);

        public Task<PagedResult<UserSummaryDto>> ListAsync(UserRoleType? role, int page, int pageSize, CancellationToken cancellationToken)
            => Task.FromResult(new PagedResult<UserSummaryDto>([], page, pageSize, 0));
    }
}
