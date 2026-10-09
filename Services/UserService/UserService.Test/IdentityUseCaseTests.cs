using System.Text.Json;
using ParkingManagement.SharedKernel.Contracts;
using ParkingManagement.SharedKernel.Domain;
using ParkingManagement.SharedKernel.Enums;
using ParkingManagement.SharedKernel.Exceptions;
using UserService.Application.Features.Identity;
using UserService.Application.Interfaces;
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
    private static byte[] Png()=>[137,80,78,71,13,10,26,10,1];
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
        public List<IntegrationEvent> Events=[];public List<OutboxMessage> Messages=[];public List<StaffAssignment> Assignments=[];
        public List<DataSubjectRequest> Requests=[];public int Commits;public int SessionRevocations;private int next=10;
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
                case StaffAssignment assignment:Add(assignment.StaffUser);assignment.StaffUserId=assignment.StaffUser.Id;Assignments.Add(assignment);break;
                case DataSubjectRequest request:Requests.Add(request);break;
            }
        }
        public void Event(IntegrationEvent ev)=>Events.Add(ev);
        public Task SaveAsync(CancellationToken ct)=>Task.CompletedTask;
        private sealed class Tx(Store store):IIdentityTransaction
        {public Task CommitAsync(CancellationToken ct){store.Commits++;return Task.CompletedTask;} public ValueTask DisposeAsync()=>ValueTask.CompletedTask;}
    }
}
