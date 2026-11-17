using ParkingManagement.SharedKernel.Contracts;
using ParkingManagement.SharedKernel.Domain;
using ParkingManagement.SharedKernel.Enums;
using ParkingManagement.SharedKernel.Exceptions;
using UserService.Application.Features.Identity;
using UserService.Application.Features.OwnerOnboarding;
using UserService.Domain.Entities;

namespace UserService.Test;

// US-053: Admin duyệt hồ sơ đối tác → chính tài khoản người nộp có thêm role LotOwner (giữ Driver) + OwnerProfile.
public class GrantLotOwnerRoleUseCaseTests
{
    private const int DriverId = 6;

    private static GrantLotOwnerRoleCommand Command(int? applicantUserId = DriverId, string businessName = "Hộ kinh doanh Bãi xe Bình An")
        => new(12, "Lê Văn Bình", "binh@baixe.vn", "0933444555", businessName, "0312345678", "99 Điện Biên Phủ, Bình Thạnh",
            "970436", "Vietcombank", "0123456789", "LE VAN BINH", applicantUserId);

    private static User Driver(UserStatus status = UserStatus.Active)
    {
        var user = new User { FullName = "Lê Văn Bình", Email = "driver2@smartparking.vn", Status = status };
        typeof(BaseEntity).GetProperty(nameof(BaseEntity.Id))!.SetValue(user, DriverId);
        user.Roles.Add(new UserRole { UserId = DriverId, Role = UserRoleType.Driver });
        return user;
    }

    private sealed class Store : IIdentityStore
    {
        public List<User> Users = [];
        public List<IntegrationEvent> Events = [];
        public int Commits;
        private int nextProfileId = 40;

        public Task<IIdentityTransaction> BeginAsync(CancellationToken ct) => Task.FromResult<IIdentityTransaction>(new Tx(this));
        public Task<User?> FindContactAsync(string contact, CancellationToken ct) => Task.FromResult<User?>(null);
        public Task<User?> FindUserAsync(int id, bool forUpdate, CancellationToken ct) => Task.FromResult(Users.FirstOrDefault(u => u.Id == id));
        public Task<OtpCode?> LatestOtpAsync(int userId, OtpPurpose purpose, CancellationToken ct) => Task.FromResult<OtpCode?>(null);
        public Task<IReadOnlyList<OtpCode>> PendingOtpsAsync(int userId, OtpPurpose purpose, CancellationToken ct)
            => Task.FromResult<IReadOnlyList<OtpCode>>([]);
        public Task<int> OtpFailuresAsync(int userId, DateTime since, CancellationToken ct) => Task.FromResult(0);
        public Task RevokeSessionsAsync(int userId, DateTime now, CancellationToken ct) => Task.CompletedTask;
        public void Add(object entity) { }
        public void Event(IntegrationEvent integrationEvent) => Events.Add(integrationEvent);

        /// <summary>Giả lập database cấp Id cho OwnerProfile mới.</summary>
        public Task SaveAsync(CancellationToken ct)
        {
            foreach (var profile in Users.Select(u => u.OwnerProfile).OfType<OwnerProfile>().Where(p => p.Id == 0))
                typeof(BaseEntity).GetProperty(nameof(BaseEntity.Id))!.SetValue(profile, nextProfileId++);
            return Task.CompletedTask;
        }

        private sealed class Tx(Store store) : IIdentityTransaction
        {
            public Task CommitAsync(CancellationToken ct) { store.Commits++; return Task.CompletedTask; }
            public ValueTask DisposeAsync() => ValueTask.CompletedTask;
        }
    }

    [Fact] // AC: duyệt xong → tài khoản có 2 role Driver + LotOwner và OwnerProfile đầy đủ thông tin pháp nhân / ngân hàng.
    public async Task Approved_applicant_keeps_driver_role_and_becomes_lot_owner()
    {
        var store = new Store { Users = [Driver()] };

        var result = await new GrantLotOwnerRoleUseCase(store).ExecuteAsync(Command());

        var user = store.Users[0];
        Assert.Equal(new GrantedLotOwnerDto(DriverId, 40), result);
        Assert.Equal([UserRoleType.Driver, UserRoleType.LotOwner], user.Roles.Select(r => r.Role).Order().ToArray());
        var profile = Assert.IsType<OwnerProfile>(user.OwnerProfile);
        Assert.Equal("Hộ kinh doanh Bãi xe Bình An", profile.BusinessName);
        Assert.Equal("0312345678", profile.TaxCode);
        Assert.Equal("0123456789", profile.BankAccountNumber);
        Assert.Equal(OwnerStatus.Active, profile.Status);
        Assert.False(profile.IsLocked);
        var created = Assert.IsType<OwnerProfileCreated>(Assert.Single(store.Events));
        Assert.Equal(40, created.OwnerProfileId);
        Assert.Equal(1, store.Commits);
    }

    [Fact] // Gọi lại (AdminService thử lại sau lỗi) → trả Id cũ, không tạo hồ sơ / role / sự kiện mới.
    public async Task Repeated_grant_is_idempotent()
    {
        var store = new Store { Users = [Driver()] };
        var useCase = new GrantLotOwnerRoleUseCase(store);

        var first = await useCase.ExecuteAsync(Command());
        var second = await useCase.ExecuteAsync(Command());

        Assert.Equal(first, second);
        Assert.Equal(2, store.Users[0].Roles.Count);
        Assert.Single(store.Events);
    }

    [Theory] // Hồ sơ cũ không gắn tài khoản / tài khoản không tồn tại → 409 (không 404 vì AdminService hiểu 404 là thiếu endpoint).
    [InlineData(null)]
    [InlineData(999)]
    public async Task Missing_applicant_is_conflict(int? applicantUserId)
    {
        var store = new Store { Users = [Driver()] };
        await Assert.ThrowsAsync<ConflictException>(() => new GrantLotOwnerRoleUseCase(store).ExecuteAsync(Command(applicantUserId)));
        Assert.Null(store.Users[0].OwnerProfile);
    }

    [Theory] // Tài khoản bị khóa / chưa xác minh không được cấp quyền.
    [InlineData(UserStatus.Locked)]
    [InlineData(UserStatus.PendingVerification)]
    public async Task Inactive_applicant_is_conflict(UserStatus status)
    {
        var store = new Store { Users = [Driver(status)] };
        var ex = await Assert.ThrowsAsync<ConflictException>(() => new GrantLotOwnerRoleUseCase(store).ExecuteAsync(Command()));
        Assert.Contains("đang hoạt động", ex.Message);
        Assert.DoesNotContain(store.Users[0].Roles, r => r.Role == UserRoleType.LotOwner);
    }

    [Fact] // Chủ bãi cũ đã ngừng hợp tác (Terminated, khóa) → 409, không dùng lại hồ sơ cũ một cách âm thầm.
    public async Task Former_terminated_owner_is_conflict()
    {
        var user = Driver();
        user.OwnerProfile = new OwnerProfile { UserId = DriverId, BusinessName = "Bãi cũ", Status = OwnerStatus.Terminated, IsLocked = true };
        var store = new Store { Users = [user] };

        var ex = await Assert.ThrowsAsync<ConflictException>(() => new GrantLotOwnerRoleUseCase(store).ExecuteAsync(Command()));
        Assert.Contains("đã từng là chủ bãi", ex.Message);
        Assert.Empty(store.Events);
    }

    [Fact] // Tên doanh nghiệp vượt giới hạn hồ sơ chủ bãi → 409 kèm lý do (Admin thấy được thông điệp).
    public async Task Too_long_business_name_is_conflict_with_reason()
    {
        var store = new Store { Users = [Driver()] };
        var ex = await Assert.ThrowsAsync<ConflictException>(
            () => new GrantLotOwnerRoleUseCase(store).ExecuteAsync(Command(businessName: new string('A', 201))));
        Assert.Contains("200 ký tự", ex.Message);
    }
}
