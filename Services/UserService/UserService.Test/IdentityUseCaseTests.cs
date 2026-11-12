using System.Text.Json;
using ParkingManagement.SharedKernel.Contracts;
using ParkingManagement.SharedKernel.Domain;
using ParkingManagement.SharedKernel.Enums;
using ParkingManagement.SharedKernel.Exceptions;
using UserService.Application.Features.Identity;
using UserService.Application.Interfaces;
using UserService.Application.Features.Users;
using UserService.Domain.Entities;

namespace UserService.Test;

public class IdentityUseCaseTests
{
    private static readonly DateTime Now = new(2026,10,7,10,0,0,DateTimeKind.Utc);
    private readonly Store store = new();
    private readonly Delivery delivery = new();
    private AccountUseCases Accounts => new(store, new Passwords(), new Cipher(), delivery, new Clock());

    [Fact]
    public async Task Register_creates_pending_user_six_digit_otp_and_outbox_together()
    {
        await Accounts.RegisterAsync(new("Driver", " Driver@Example.com ", "Password123"), default);
        var user = Assert.Single(store.Users);
        Assert.Equal("driver@example.com", user.Email);
        Assert.Equal(UserStatus.PendingVerification, user.Status);
        Assert.Equal("hash:Password123", user.PasswordHash);
        var otp = Assert.Single(store.Otps);
        Assert.True(IdentityRules.OtpFormat(Code()));
        Assert.Equal(Now.AddSeconds(300), otp.ExpiresAtUtc);
        Assert.Equal("hash:"+Code(),otp.CodeHash);
        Assert.Contains(store.Events,e=>e is UserRegistered);
        Assert.Equal(1, store.Commits);
    }
    [Fact]
    public async Task Owner_registration_creates_tenant_and_owner_event()
    {
        await Accounts.RegisterAsync(new("Owner","owner@example.com","Password123",UserRoleType.LotOwner,"Company"),default);
        Assert.NotNull(store.Users[0].OwnerProfile);
        Assert.Contains(store.Events,e=>e is OwnerProfileCreated);
    }
    [Theory]
    [InlineData(UserRoleType.Admin)]
    [InlineData(UserRoleType.Staff)]
    public async Task Public_registration_cannot_grant_privileged_roles(UserRoleType role)
    {
        await Assert.ThrowsAsync<ValidationException>(()=>Accounts.RegisterAsync(new("User","a@example.com","Password123",role),default));
        Assert.Empty(store.Users);
    }
    [Fact]
    public async Task Duplicate_registration_does_not_replace_password_or_send_another_otp()
    {
        await Accounts.RegisterAsync(new("User","a@example.com","Password123"),default);
        await Accounts.RegisterAsync(new("Other","a@example.com","OtherPassword123"),default);
        Assert.Single(store.Users); Assert.Single(store.Otps);
        Assert.Equal("hash:Password123",store.Users[0].PasswordHash);
    }
    [Fact]
    public async Task Missing_delivery_configuration_fails_before_creating_user()
    {
        delivery.Available=false;
        await Assert.ThrowsAsync<DependencyUnavailableException>(()=>Accounts.RegisterAsync(new("User","a@example.com","Password123"),default));
        Assert.Empty(store.Users);
    }
    [Fact]
    public async Task Verify_activates_user_and_consumes_otp_once()
    {
        await Accounts.RegisterAsync(new("User","a@example.com","Password123"),default);
        var code=Code();
        await Accounts.VerifyRegistrationAsync(new("a@example.com",code),default);
        Assert.Equal(UserStatus.Active,store.Users[0].Status); Assert.True(store.Users[0].EmailConfirmed);
        Assert.Equal(Now,store.Otps[0].ConsumedAtUtc);
        await Assert.ThrowsAsync<AuthenticationException>(()=>Accounts.VerifyRegistrationAsync(new("a@example.com",code),default));
    }
    [Theory]
    [InlineData(300)]
    [InlineData(301)]
    public async Task Otp_is_invalid_at_exact_expiry_and_after(int seconds)
    {
        await Accounts.RegisterAsync(new("User","a@example.com","Password123"),default);
        var accounts=new AccountUseCases(store,new Passwords(),new Cipher(),delivery,new Clock(Now.AddSeconds(seconds)));
        await Assert.ThrowsAsync<AuthenticationException>(()=>accounts.VerifyRegistrationAsync(new("a@example.com",Code()),default));
        Assert.Equal(UserStatus.PendingVerification,store.Users[0].Status);
    }
    [Fact]
    public async Task Three_wrong_otps_lock_for_fifteen_minutes_and_persist_failure()
    {
        await Accounts.RegisterAsync(new("User","a@example.com","Password123"),default);
        for(var i=0;i<3;i++) await Assert.ThrowsAsync<AuthenticationException>(()=>Accounts.VerifyRegistrationAsync(new("a@example.com","wrong"),default));
        Assert.Equal(Now.AddMinutes(15),store.Users[0].LockedUntilUtc);
        Assert.Equal(3,store.Otps[0].AttemptCount);
        Assert.Contains(store.Events,e=>e is UserLocked);
        Assert.Equal(1,store.SessionRevocations);
        Assert.Equal(4,store.Commits);
    }
    [Fact]
    public async Task Resend_invalidates_old_code_and_does_not_reset_failure_history()
    {
        await Accounts.RegisterAsync(new("User","a@example.com","Password123"),default);
        await Assert.ThrowsAsync<AuthenticationException>(()=>Accounts.VerifyRegistrationAsync(new("a@example.com","wrong"),default));
        var accounts=new AccountUseCases(store,new Passwords(),new Cipher(),delivery,new Clock(Now.AddSeconds(61)));
        await accounts.RequestOtpAsync(new("a@example.com"),default);
        Assert.Equal(2,store.Otps.Count); Assert.NotNull(store.Otps[0].ConsumedAtUtc);
        Assert.Single(store.Security,e=>e.EventType==SecurityEventType.OtpFailed);
    }
    [Fact]
    public async Task Resend_within_sixty_seconds_does_not_issue_new_code()
    {
        await Accounts.RegisterAsync(new("User","a@example.com","Password123"),default);
        await Accounts.RequestOtpAsync(new("a@example.com"),default); Assert.Single(store.Otps);
    }
    [Fact]
    public async Task Reset_password_consumes_code_changes_hash_revokes_sessions_and_cannot_replay()
    {
        var user = Active();
        await Accounts.RequestOtpAsync(new(user.Email!, OtpPurpose.ResetPassword), default);
        var code = Code();
        await Accounts.ResetPasswordAsync(new(user.Email!, code, "NewPassword123"), default);
        Assert.Equal("hash:NewPassword123", user.PasswordHash);
        Assert.Equal(1, store.SessionRevocations);
        Assert.Contains(store.Security, e => e.EventType == SecurityEventType.PasswordChanged);
        await Assert.ThrowsAsync<AuthenticationException>(() => Accounts.ResetPasswordAsync(new(user.Email!, code, "Another123"), default));
    }

    [Fact]
    public async Task Registration_otp_cannot_be_used_for_password_reset()
    {
        var user = Active();
        await Accounts.IssueOtpAsync(user, user.Email!, OtpPurpose.Register, default);
        await Assert.ThrowsAsync<AuthenticationException>(() => Accounts.ResetPasswordAsync(new(user.Email!, Code(), "NewPassword123"), default));
        Assert.Equal(0, store.SessionRevocations);
    }

    [Fact]
    public async Task Reset_otp_locks_account_after_three_failures()
    {
        var user = Active();
        await Accounts.RequestOtpAsync(new(user.Email!, OtpPurpose.ResetPassword), default);
        for (var i = 0; i < 3; i++)
            await Assert.ThrowsAsync<AuthenticationException>(() => Accounts.ResetPasswordAsync(new(user.Email!, "wrong", "NewPassword123"), default));
        Assert.Equal(Now.AddMinutes(15), user.LockedUntilUtc);
        Assert.Equal("", user.PasswordHash);
    }

    [Fact]
    public async Task Forgot_password_does_not_issue_for_unknown_pending_or_unverified_contacts()
    {
        await Accounts.RequestOtpAsync(new("unknown@example.com", OtpPurpose.ResetPassword), default);
        var user = Active(); user.EmailConfirmed = false;
        await Accounts.RequestOtpAsync(new(user.Email!, OtpPurpose.ResetPassword), default);
        user.EmailConfirmed = true; user.Status = UserStatus.PendingVerification;
        await Accounts.RequestOtpAsync(new(user.Email!, OtpPurpose.ResetPassword), default);
        Assert.Empty(store.Otps);
    }

    [Fact]
    public async Task Reset_code_is_invalid_at_exact_expiry()
    {
        var user = Active();
        await Accounts.RequestOtpAsync(new(user.Email!, OtpPurpose.ResetPassword), default);
        var accounts = new AccountUseCases(store, new Passwords(), new Cipher(), delivery, new Clock(Now.AddSeconds(300)));
        await Assert.ThrowsAsync<AuthenticationException>(() => accounts.ResetPasswordAsync(new(user.Email!, Code(), "NewPassword123"), default));
    }

    [Fact]
    public async Task Profile_only_changes_current_users_display_fields()
    {
        var user = Active();
        var useCase = new ProfileUseCases(store, new Current(), new Clock());
        user.AvatarUrl = "/api/v1/users/avatars/existing.png";
        await useCase.UpdateAsync(new(" Updated "), default);
        Assert.Equal("Updated", user.FullName);
        Assert.Equal("/api/v1/users/avatars/existing.png", user.AvatarUrl);
        Assert.Equal("a@example.com", user.Email);
        user.LockedUntilUtc = Now.AddMinutes(15);
        await Assert.ThrowsAsync<AuthenticationException>(() => useCase.UpdateAsync(new("User"), default));
    }

    [Fact]
    public async Task Avatar_upload_and_remove_only_change_current_users_avatar()
    {
        var user = Active(); user.AvatarUrl = "old";
        var storage = new AvatarFiles();
        var avatars = new AvatarUseCases(store, new Current(), storage, new Clock());
        using var image = new MemoryStream([1]);
        var response = await avatars.UploadAsync(image, 1, default);
        Assert.Equal("new", response.AvatarUrl); Assert.Equal("new", user.AvatarUrl);
        Assert.Contains("old", storage.Deleted);
        Assert.Equal("a@example.com", user.Email);
        await avatars.RemoveAsync(default);
        Assert.Null(user.AvatarUrl); Assert.Contains("new", storage.Deleted);
    }

    [Fact]
    public async Task Locked_user_upload_cleans_new_file_and_keeps_old_avatar()
    {
        var user = Active(); user.AvatarUrl = "old"; user.LockedUntilUtc = Now.AddMinutes(1);
        var storage = new AvatarFiles();
        using var image = new MemoryStream([1]);
        await Assert.ThrowsAsync<AuthenticationException>(() => new AvatarUseCases(store, new Current(), storage, new Clock()).UploadAsync(image, 1, default));
        Assert.Equal("old", user.AvatarUrl);
        Assert.Contains("new", storage.Deleted); Assert.DoesNotContain("old", storage.Deleted);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(2097153)]
    public async Task Avatar_invalid_length_never_writes_file(long length)
    {
        Active(); var storage = new AvatarFiles(); using var image = new MemoryStream();
        await Assert.ThrowsAsync<ValidationException>(() => new AvatarUseCases(store, new Current(), storage, new Clock()).UploadAsync(image, length, default));
        Assert.Equal(0, storage.Saves);
    }
    private sealed class AvatarFiles : IAvatarStorage
    {
        public int Saves;
        public List<string?> Deleted = [];
        public Task<string> SaveAsync(Stream image, CancellationToken ct) { Saves++; return Task.FromResult("new"); }
        public Task DeleteAsync(string? url, CancellationToken ct) { Deleted.Add(url); return Task.CompletedTask; }
        public Task<byte[]?> ReadAsync(Guid id, CancellationToken ct) => Task.FromResult<byte[]?>(null);
    }

    private sealed class Current : ICurrentUser
    {
        public int? UserId => 1;
        public string? Email => "a@example.com";
        public IReadOnlyList<string> Roles => ["Driver"];
        public int? OwnerProfileId => null;
        public bool IsAuthenticated => true;
    }
    private User Active()
    {
        var user=new User { FullName="User",Email="a@example.com",Status=UserStatus.Active,EmailConfirmed=true };
        Id(user,1); store.Users.Add(user); return user;
    }
    private string Code()
    {
        using var json=JsonDocument.Parse(store.Messages.Last(m=>m.EventType=="OtpDeliveryRequested").PayloadJson);
        return new Cipher().Decrypt(json.RootElement.GetProperty("EncryptedCode").GetString()!,"otp-delivery");
    }
    private static void Id(BaseEntity entity,int id)=>typeof(BaseEntity).GetProperty(nameof(BaseEntity.Id))!.SetValue(entity,id);
    private sealed class Clock(DateTime? now=null):TimeProvider { public override DateTimeOffset GetUtcNow()=>new(now??Now); }
    private sealed class Passwords:IPasswordHasher { public string Hash(string value)=>"hash:"+value; public bool Verify(string value,string hash)=>hash==Hash(value); }
    private sealed class Cipher:ISecretCipher
    {
        public string Encrypt(string value,string purpose)=>Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(purpose+"|"+value));
        public string Decrypt(string value,string purpose)=>System.Text.Encoding.UTF8.GetString(Convert.FromBase64String(value))[(purpose.Length+1)..];
    }
    private sealed class Delivery:IOtpDeliveryConfiguration { public bool Available=true; public void EnsureAvailable() { if(!Available) throw new DependencyUnavailableException("missing"); } }
    private sealed class Store:IIdentityStore
    {
        public List<User> Users=[]; public List<OtpCode> Otps=[];public List<SecurityEvent> Security=[];
        public List<IntegrationEvent> Events=[];public List<OutboxMessage> Messages=[];
        public int Commits;public int SessionRevocations;private int next=10;
        public Task<IIdentityTransaction> BeginAsync(CancellationToken ct)=>Task.FromResult<IIdentityTransaction>(new Tx(this));
        public Task<User?> FindContactAsync(string contact,CancellationToken ct)=>Task.FromResult(Users.FirstOrDefault(u=>u.Email==contact||u.PhoneNumber==contact));
        public Task<User?> FindUserAsync(int id,bool update,CancellationToken ct)=>Task.FromResult(Users.FirstOrDefault(u=>u.Id==id&&!u.IsDeleted));
        public Task<OtpCode?> LatestOtpAsync(int id,OtpPurpose purpose,CancellationToken ct)=>Task.FromResult(Otps.LastOrDefault(o=>o.UserId==id&&o.Purpose==purpose));
        public Task<IReadOnlyList<OtpCode>> PendingOtpsAsync(int id,OtpPurpose purpose,CancellationToken ct)=>Task.FromResult<IReadOnlyList<OtpCode>>(Otps.Where(o=>o.UserId==id&&o.Purpose==purpose&&o.ConsumedAtUtc==null).ToList());
        public Task<int> OtpFailuresAsync(int id,DateTime since,CancellationToken ct)=>Task.FromResult(Security.Count(e=>e.UserId==id&&e.EventType==SecurityEventType.OtpFailed));
        public Task RevokeSessionsAsync(int id,DateTime now,CancellationToken ct){SessionRevocations++;return Task.CompletedTask;}
        public void Add(object value)
        {
            if(value is BaseEntity entity&&entity.Id==0)Id(entity,next++);
            switch(value)
            {
                case User user: Users.Add(user);if(user.OwnerProfile is {} owner){Id(owner,next++);owner.User=user;owner.UserId=user.Id;}break;
                case OtpCode otp:Otps.Add(otp);break;
                case SecurityEvent ev:Security.Add(ev);break;
                case OutboxMessage m:Messages.Add(m);break;
            }
        }
        public void Event(IntegrationEvent ev)=>Events.Add(ev);
        public Task SaveAsync(CancellationToken ct)=>Task.CompletedTask;
        private sealed class Tx(Store store):IIdentityTransaction
        {public Task CommitAsync(CancellationToken ct){store.Commits++;return Task.CompletedTask;} public ValueTask DisposeAsync()=>ValueTask.CompletedTask;}
    }
}
