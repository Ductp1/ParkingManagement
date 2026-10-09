using AdminService.Application.Features.Users;
using ParkingManagement.SharedKernel.Contracts;
using ParkingManagement.SharedKernel.Enums;
using ParkingManagement.SharedKernel.Exceptions;

namespace AdminService.Test;

// US-096: Admin tra cứu người dùng (unit test với fake IUserServiceClient, không cần UserService hay database).
public class AdminUserTests
{
    private static readonly AdminUserDto Admin =
        new(1, "Quản trị Smart Parking", "admin@smartparking.vn", "0900000001", "Active", "NotSubmitted", ["Admin"], null);
    private static readonly AdminUserDto OwnerVincom =
        new(2, "Công ty Vincom Parking", "owner.vincom@smartparking.vn", "0900000002", "Active", "NotSubmitted", ["LotOwner", "Driver"],
            new AdminUserOwnerProfileDto(1, "Công ty TNHH Vincom Parking"));
    private static readonly AdminUserDto OwnerTsn =
        new(3, "Công ty Bãi xe Tân Sơn Nhất", "owner.tsn@smartparking.vn", "0900000003", "Locked", "NotSubmitted", ["LotOwner"],
            new AdminUserOwnerProfileDto(2, "Công ty CP Bãi xe Tân Sơn Nhất"));

    [Fact]
    public async Task List_users_passes_role_and_paging_to_user_service()
    {
        var userService = new FakeUserService(Admin, OwnerVincom, OwnerTsn);

        var result = await new ListAdminUsersUseCase(userService).ExecuteAsync(UserRoleType.LotOwner, 2, 1);

        var call = Assert.Single(userService.ListCalls);
        Assert.Equal(UserRoleType.LotOwner, call.Role);
        Assert.Equal(2, call.Page);
        Assert.Equal(1, call.PageSize);
        Assert.Equal(2, result.Page);
        Assert.Equal(1, result.PageSize);
        Assert.Equal(2, result.TotalCount);                  // 2 chủ bãi, trang 2 chứa chủ bãi thứ hai
        var item = Assert.Single(result.Items);
        Assert.Equal(OwnerTsn.Id, item.Id);
        Assert.Equal("Locked", item.Status);
        Assert.Equal(["LotOwner"], item.Roles);
    }

    [Fact]
    public async Task List_users_without_role_returns_every_role()
    {
        var userService = new FakeUserService(Admin, OwnerVincom, OwnerTsn);

        var result = await new ListAdminUsersUseCase(userService).ExecuteAsync(null, 1, 20);

        Assert.Null(Assert.Single(userService.ListCalls).Role);
        Assert.Equal(3, result.Items.Count);
    }

    [Theory]
    [InlineData(1, 1)]
    [InlineData(1, 100)]
    public async Task List_users_accepts_page_size_boundaries(int page, int pageSize)
    {
        var userService = new FakeUserService(Admin);

        await new ListAdminUsersUseCase(userService).ExecuteAsync(null, page, pageSize);

        Assert.Single(userService.ListCalls);
    }

    [Theory]
    [InlineData(0, 20, "page phải ≥ 1.")]
    [InlineData(-1, 20, "page phải ≥ 1.")]
    [InlineData(1, 0, "pageSize phải trong khoảng 1–100.")]
    [InlineData(1, 101, "pageSize phải trong khoảng 1–100.")]
    public async Task List_users_with_invalid_paging_is_rejected(int page, int pageSize, string expectedMessage)
    {
        var userService = new FakeUserService(Admin);

        var ex = await Assert.ThrowsAsync<ValidationException>(
            () => new ListAdminUsersUseCase(userService).ExecuteAsync(null, page, pageSize));

        Assert.Equal(expectedMessage, ex.Message);
        Assert.Empty(userService.ListCalls);                 // không gọi UserService khi đầu vào sai
    }

    [Fact]
    public async Task List_users_with_undefined_role_is_rejected()
    {
        var userService = new FakeUserService(Admin);

        var ex = await Assert.ThrowsAsync<ValidationException>(
            () => new ListAdminUsersUseCase(userService).ExecuteAsync((UserRoleType)99, 1, 20));

        Assert.Equal("Vai trò không hợp lệ.", ex.Message);
        Assert.Empty(userService.ListCalls);
    }

    [Fact]
    public async Task List_users_does_not_hide_user_service_failure()
        => await Assert.ThrowsAsync<UserServiceUnavailableException>(
            () => new ListAdminUsersUseCase(new FakeUserService { Failure = new UserServiceUnavailableException("down") })
                .ExecuteAsync(null, 1, 20));

    [Fact]
    public async Task Get_user_by_id_returns_user_with_owner_profile()
    {
        var userService = new FakeUserService(Admin, OwnerVincom);

        var user = await new GetAdminUserByIdUseCase(userService).ExecuteAsync(OwnerVincom.Id);

        Assert.Equal(OwnerVincom, user);
        Assert.Equal("Công ty TNHH Vincom Parking", user.OwnerProfile!.BusinessName);
        Assert.Equal([OwnerVincom.Id], userService.FindCalls);
    }

    [Fact]
    public async Task Get_user_without_owner_profile_has_null_owner_profile()
    {
        var user = await new GetAdminUserByIdUseCase(new FakeUserService(Admin)).ExecuteAsync(Admin.Id);

        Assert.Null(user.OwnerProfile);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-5)]
    public async Task Get_user_with_non_positive_id_is_rejected(int userId)
    {
        var userService = new FakeUserService(Admin);

        var ex = await Assert.ThrowsAsync<ValidationException>(
            () => new GetAdminUserByIdUseCase(userService).ExecuteAsync(userId));

        Assert.Equal("Id người dùng phải là số nguyên dương.", ex.Message);
        Assert.Empty(userService.FindCalls);
    }

    [Fact]
    public async Task Get_non_existing_user_is_not_found()
    {
        var ex = await Assert.ThrowsAsync<NotFoundException>(
            () => new GetAdminUserByIdUseCase(new FakeUserService(Admin)).ExecuteAsync(999));

        Assert.Equal("Người dùng với Id = '999' không tồn tại hoặc chưa được công khai.", ex.Message);
    }

    [Fact]
    public async Task Get_user_does_not_hide_user_service_failure()
        => await Assert.ThrowsAsync<UserServiceUnavailableException>(
            () => new GetAdminUserByIdUseCase(new FakeUserService { Failure = new UserServiceUnavailableException("down") })
                .ExecuteAsync(2));

    private sealed class FakeUserService(params AdminUserDto[] users) : IUserServiceClient
    {
        public List<(UserRoleType? Role, int Page, int PageSize)> ListCalls { get; } = [];
        public List<int> FindCalls { get; } = [];
        public Exception? Failure { get; init; }

        public Task<PagedResult<AdminUserSummaryDto>> ListUsersAsync(UserRoleType? role, int page, int pageSize, CancellationToken cancellationToken)
        {
            ListCalls.Add((role, page, pageSize));
            if (Failure is not null) throw Failure;

            var matched = users.Where(u => role is null || u.Roles.Contains(role.Value.ToString())).ToList();
            var items = matched.Skip((page - 1) * pageSize).Take(pageSize)
                .Select(u => new AdminUserSummaryDto(u.Id, u.FullName, u.Email, u.Status, u.Roles))
                .ToList();
            return Task.FromResult(new PagedResult<AdminUserSummaryDto>(items, page, pageSize, matched.Count));
        }

        public Task<AdminUserDto?> FindUserByIdAsync(int userId, CancellationToken cancellationToken)
        {
            FindCalls.Add(userId);
            if (Failure is not null) throw Failure;
            return Task.FromResult(users.FirstOrDefault(u => u.Id == userId));
        }
    }
}
