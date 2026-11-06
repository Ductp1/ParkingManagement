using System.Collections.Concurrent;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text.Json;
using MassTransit;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using UserService.Infrastructure.Persistence;

namespace ParkingManagement.IntegrationTests;

// Chỉ bật với DB test riêng; không chạm pm_user đang dùng.
public sealed class Tv1PostgresFactAttribute : FactAttribute
{
    public Tv1PostgresFactAttribute()
    {
        if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("TV1_TEST_DB")))
            Skip="Cần TV1_TEST_DB trỏ đến PostgreSQL test riêng.";
    }
}

[CollectionDefinition("TV1 HTTP",DisableParallelization=true)]
public sealed class Tv1Collection : ICollectionFixture<Tv1ApiFixture>;

[Collection("TV1 HTTP")]
public sealed class UserServiceApiTests(Tv1ApiFixture fixture)
{
    [Tv1PostgresFact]
    public async Task Registration_login_rotation_and_logout_revoke_access_session()
    {
        var session=await fixture.RegisterAsync();
        using var client=fixture.Client();client.DefaultRequestHeaders.Authorization=new("Bearer",session.AccessToken);
        Assert.Equal(HttpStatusCode.OK,(await client.GetAsync("/api/v1/users/me")).StatusCode);
        var response=await client.PostAsJsonAsync("/api/v1/auth/refresh",new{session.RefreshToken});
        Assert.Equal(HttpStatusCode.OK,response.StatusCode);
        Assert.True(response.Headers.CacheControl?.NoStore);
        var replacement=(await response.Content.ReadFromJsonAsync<Tokens>())!;
        Assert.NotEqual(session.RefreshToken,replacement.RefreshToken);
        Assert.Equal(HttpStatusCode.Unauthorized,(await client.GetAsync("/api/v1/users/me")).StatusCode);
        client.DefaultRequestHeaders.Authorization=new("Bearer",replacement.AccessToken);
        Assert.Equal(HttpStatusCode.OK,(await client.GetAsync("/api/v1/users/me")).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent,(await client.PostAsJsonAsync("/api/v1/auth/logout",new{replacement.RefreshToken})).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized,(await client.GetAsync("/api/v1/users/me")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized,(await client.PostAsJsonAsync("/api/v1/auth/refresh",new{replacement.RefreshToken})).StatusCode);
    }

    [Tv1PostgresFact]
    public async Task Third_wrong_otp_persists_lock_and_security_events()
    {
        using var client=fixture.Client();var contact=fixture.Contact();
        Assert.Equal(HttpStatusCode.Accepted,(await client.PostAsJsonAsync("/api/v1/auth/register",new{FullName="Test",Contact=contact,Password="Password123"})).StatusCode);
        var code=await fixture.CodeAsync(contact,"Register");var wrong=code=="000000"?"999999":"000000";
        for(var i=0;i<3;i++)Assert.Equal(HttpStatusCode.Unauthorized,(await client.PostAsJsonAsync("/api/v1/auth/otp/verify",new{Contact=contact,Code=wrong})).StatusCode);
        await using var scope=fixture.Services.CreateAsyncScope();var db=scope.ServiceProvider.GetRequiredService<UserDbContext>();
        var user=await db.Users.SingleAsync(u=>u.Email==contact);
        Assert.True(user.LockedUntilUtc>DateTime.UtcNow.AddMinutes(14));
        Assert.Equal(3,await db.SecurityEvents.CountAsync(e=>e.UserId==user.Id&&e.EventType==ParkingManagement.SharedKernel.Enums.SecurityEventType.OtpFailed));
    }

    [Tv1PostgresFact]
    public async Task Concurrent_refresh_has_one_winner_and_replay_revokes_the_chain()
    {
        var session=await fixture.RegisterAsync();using var client=fixture.Client();
        var responses=await Task.WhenAll(client.PostAsJsonAsync("/api/v1/auth/refresh",new{session.RefreshToken}),
            client.PostAsJsonAsync("/api/v1/auth/refresh",new{session.RefreshToken}));
        Assert.Single(responses,r=>r.StatusCode==HttpStatusCode.OK);
        Assert.Single(responses,r=>r.StatusCode==HttpStatusCode.Unauthorized);
        var winner=(await responses.Single(r=>r.StatusCode==HttpStatusCode.OK).Content.ReadFromJsonAsync<Tokens>())!;
        Assert.Equal(HttpStatusCode.Unauthorized,(await client.PostAsJsonAsync("/api/v1/auth/refresh",new{winner.RefreshToken})).StatusCode);
    }

    [Tv1PostgresFact]
    public async Task Data_requests_require_password_and_only_export_the_requesters_profile()
    {
        const string path = "/api/v1/users/me/data-requests";
        var session = await fixture.RegisterAsync();
        var other = await fixture.RegisterAsync();
        using var client = fixture.Client();
        using var otherClient = fixture.Client();
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync(path)).StatusCode);
        client.DefaultRequestHeaders.Authorization = new("Bearer", session.AccessToken);
        otherClient.DefaultRequestHeaders.Authorization = new("Bearer", other.AccessToken);
        var request = new { Type = "Export", Password = "Password123" };
        Assert.Equal(HttpStatusCode.Forbidden, (await client.PostAsJsonAsync(path, new { request.Type, Password = "Wrong123" })).StatusCode);
        using var response = await client.PostAsJsonAsync(path, request);
        Assert.Equal(HttpStatusCode.Accepted, response.StatusCode);
        var item = await response.Content.ReadFromJsonAsync<UserService.Application.Features.Privacy.DataRequestItem>();
        using var duplicate = await client.PostAsJsonAsync(path, request);
        Assert.Equal(item!.Id, (await duplicate.Content.ReadFromJsonAsync<UserService.Application.Features.Privacy.DataRequestItem>())!.Id);
        Assert.Empty((await otherClient.GetFromJsonAsync<UserService.Application.Features.Privacy.DataRequestItem[]>(path))!);
        var download = path + "/" + item.Id + "/download";
        Assert.Equal(HttpStatusCode.NotFound, (await otherClient.PostAsJsonAsync(download, new { request.Password })).StatusCode);
        using var export = await client.PostAsJsonAsync(download, new { request.Password });
        Assert.Equal(HttpStatusCode.OK, export.StatusCode);
        Assert.True(export.Headers.CacheControl?.NoStore);
        using var data = JsonDocument.Parse(await export.Content.ReadAsStringAsync());
        Assert.Equal(session.Contact, data.RootElement.GetProperty("Email").GetString());
        Assert.False(data.RootElement.TryGetProperty("PasswordHash", out _));
        Assert.Equal("Completed", Assert.Single((await client.GetFromJsonAsync<UserService.Application.Features.Privacy.DataRequestItem[]>(path))!).Status);

        using var deletion = await client.PostAsJsonAsync(path, new { Type = "Deletion", request.Password });
        Assert.Equal(HttpStatusCode.Accepted, deletion.StatusCode);
        Assert.Equal("Submitted", (await deletion.Content.ReadFromJsonAsync<UserService.Application.Features.Privacy.DataRequestItem>())!.Status);
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/api/v1/users/me")).StatusCode);
    }

    [Tv1PostgresFact]
    public async Task Owners_only_manage_staff_in_their_tenant_and_revoke_active_sessions()
    {
        const string path = "/api/v1/users/me/staff";
        var owner = await fixture.RegisterAsync(owner: true);
        var other = await fixture.RegisterAsync(owner: true);
        var driver = await fixture.RegisterAsync();
        int lotId;
        await using (var scope = fixture.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<UserDbContext>();
            lotId = await db.OwnerProfiles.Where(p => p.User.Email == owner.Contact).Select(p => p.Id).SingleAsync();
        }

        using var client = fixture.Client();
        using var otherClient = fixture.Client();
        client.DefaultRequestHeaders.Authorization = new("Bearer", driver.AccessToken);
        Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync(path)).StatusCode);
        client.DefaultRequestHeaders.Authorization = new("Bearer", owner.AccessToken);
        otherClient.DefaultRequestHeaders.Authorization = new("Bearer", other.AccessToken);
        var contact = fixture.Contact();
        var input = new { FullName = "Staff", Contact = contact, ParkingLotId = lotId };
        Assert.Equal(HttpStatusCode.Forbidden, (await otherClient.PostAsJsonAsync(path, input)).StatusCode);
        using var created = await client.PostAsJsonAsync(path, input);
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        var staff = (await created.Content.ReadFromJsonAsync<UserService.Application.Features.Staff.StaffItem>())!;
        Assert.Equal(staff.Id, Assert.Single((await client.GetFromJsonAsync<UserService.Application.Features.Staff.StaffItem[]>(path))!).Id);
        Assert.Empty((await otherClient.GetFromJsonAsync<UserService.Application.Features.Staff.StaffItem[]>(path))!);
        Assert.Equal(HttpStatusCode.NotFound, (await otherClient.DeleteAsync(path + "/" + staff.Id)).StatusCode);

        // Cùng Email/SĐT → gán thêm bãi thứ hai của chính chủ bãi, không tạo tài khoản mới.
        var lots = (await client.GetFromJsonAsync<UserService.Application.Features.Staff.OwnerLot[]>(path + "/lots"))!;
        Assert.Equal(2, lots.Length);
        var secondLotId = lotId + Tv1ApiFixture.SecondLotOffset;
        using var second = await client.PostAsJsonAsync(path, new { FullName = "Staff", Contact = contact, ParkingLotId = secondLotId });
        Assert.Equal(HttpStatusCode.Created, second.StatusCode);
        var secondStaff = (await second.Content.ReadFromJsonAsync<UserService.Application.Features.Staff.StaffItem>())!;
        Assert.Equal(staff.UserId, secondStaff.UserId);
        Assert.Equal(2, (await client.GetFromJsonAsync<UserService.Application.Features.Staff.StaffItem[]>(path))!.Length);
        Assert.Equal(HttpStatusCode.Conflict, (await client.PostAsJsonAsync(path, input)).StatusCode);
        // Chủ bãi khác không được "nhận" nhân viên của tenant này; cũng không được biến tài khoản tài xế thành Staff.
        var otherLotId = await OwnerProfileIdAsync(other.Contact);
        Assert.Equal(HttpStatusCode.Conflict, (await otherClient.PostAsJsonAsync(path,
            new { FullName = "Staff", Contact = contact, ParkingLotId = otherLotId })).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await client.PostAsJsonAsync(path,
            new { FullName = "Driver", Contact = driver.Contact, ParkingLotId = lotId })).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await client.DeleteAsync(path + "/" + secondStaff.Id)).StatusCode);

        // Tài khoản nhân viên mới được kích hoạt ngay; mật khẩu tạm đi qua Outbox tới kênh gửi email (adapter test).
        using var staffClient = fixture.Client();
        var password = await fixture.CodeAsync(contact, "StaffCredentials");
        using var login = await staffClient.PostAsJsonAsync("/api/v1/auth/login",
            new { EmailOrPhone = contact, Password = password, CaptchaToken = "integration-valid" });
        Assert.Equal(HttpStatusCode.OK, login.StatusCode);
        var session = (await login.Content.ReadFromJsonAsync<Tokens>())!;
        staffClient.DefaultRequestHeaders.Authorization = new("Bearer", session.AccessToken);
        Assert.Equal(HttpStatusCode.OK, (await staffClient.GetAsync("/api/v1/auth/session")).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await client.DeleteAsync(path + "/" + staff.Id)).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await client.DeleteAsync(path + "/" + staff.Id)).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await staffClient.GetAsync("/api/v1/auth/session")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await staffClient.PostAsJsonAsync("/api/v1/auth/refresh", new { session.RefreshToken })).StatusCode);
    }

    [Tv1PostgresFact]
    public async Task Admin_approves_deletion_which_anonymizes_and_signs_out_the_user()
    {
        const string path = "/api/v1/users/data-requests";
        var requester = await fixture.RegisterAsync();
        var admin = await fixture.RegisterAsync();
        await using (var scope = fixture.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<UserDbContext>();
            var adminId = await db.Users.Where(u => u.Email == admin.Contact).Select(u => u.Id).SingleAsync();
            db.UserRoles.Add(new UserService.Domain.Entities.UserRole { UserId = adminId, Role = ParkingManagement.SharedKernel.Enums.UserRoleType.Admin });
            await db.SaveChangesAsync();
        }
        using var userClient = fixture.Client();
        userClient.DefaultRequestHeaders.Authorization = new("Bearer", requester.AccessToken);
        using var adminClient = fixture.Client();
        adminClient.DefaultRequestHeaders.Authorization = new("Bearer", admin.AccessToken);

        using var submitted = await userClient.PostAsJsonAsync("/api/v1/users/me/data-requests", new { Type = "Deletion", Password = "Password123" });
        Assert.Equal(HttpStatusCode.Accepted, submitted.StatusCode);
        var request = (await submitted.Content.ReadFromJsonAsync<UserService.Application.Features.Privacy.DataRequestItem>())!;

        Assert.Equal(HttpStatusCode.Forbidden, (await userClient.GetAsync(path)).StatusCode);
        var pending = (await adminClient.GetFromJsonAsync<UserService.Application.Features.Privacy.AdminDataRequestItem[]>(
            path + "?status=Submitted&type=Deletion"))!;
        Assert.Contains(pending, item => item.Id == request.Id);
        Assert.Equal(HttpStatusCode.BadRequest, (await adminClient.PostAsJsonAsync(path + "/" + request.Id + "/reject", new { Note = " " })).StatusCode);

        using var approved = await adminClient.PostAsJsonAsync(path + "/" + request.Id + "/approve", new { Note = "OK" });
        Assert.Equal(HttpStatusCode.OK, approved.StatusCode);
        Assert.Equal("Completed", (await approved.Content.ReadFromJsonAsync<UserService.Application.Features.Privacy.AdminDataRequestItem>())!.Status);
        Assert.Equal(HttpStatusCode.Conflict, (await adminClient.PostAsJsonAsync(path + "/" + request.Id + "/approve", new { Note = "OK" })).StatusCode);

        Assert.Equal(HttpStatusCode.Unauthorized, (await userClient.GetAsync("/api/v1/users/me")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await userClient.PostAsJsonAsync("/api/v1/auth/refresh", new { requester.RefreshToken })).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await userClient.PostAsJsonAsync("/api/v1/auth/login",
            new { EmailOrPhone = requester.Contact, Password = "Password123", CaptchaToken = "integration-valid" })).StatusCode);

        await using (var scope = fixture.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<UserDbContext>();
            var erasedId = await db.DataSubjectRequests.Where(r => r.Id == request.Id).Select(r => r.UserId).SingleAsync();
            var erased = await db.Users.IgnoreQueryFilters().SingleAsync(u => u.Id == erasedId);
            Assert.True(erased.IsDeleted);
            Assert.Equal(UserService.Application.Features.Privacy.DataRequestUseCases.ErasedName, erased.FullName);
            Assert.EndsWith("@deleted.invalid", erased.Email);
            Assert.Null(erased.PhoneNumber);
            Assert.False(await db.Users.IgnoreQueryFilters().AnyAsync(u => u.Email == requester.Contact));
        }
        // Email cũ được giải phóng: có thể đăng ký lại như tài khoản mới.
        using var again = fixture.Client();
        Assert.Equal(HttpStatusCode.Accepted, (await again.PostAsJsonAsync("/api/v1/auth/register",
            new { FullName = "Again", Contact = requester.Contact, Password = "Password123" })).StatusCode);
    }

    private async Task<int> OwnerProfileIdAsync(string contact)
    {
        await using var scope = fixture.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<UserDbContext>();
        return await db.OwnerProfiles.Where(p => p.User.Email == contact).Select(p => p.Id).SingleAsync();
    }

    public sealed record Tokens(string AccessToken,string RefreshToken);

    [Tv1PostgresFact]
    public async Task Reset_password_revokes_old_access_and_refresh_and_rejects_replay()
    {
        var session = await fixture.RegisterAsync();
        using var client = fixture.Client();
        Assert.Equal(HttpStatusCode.Accepted, (await client.PostAsJsonAsync("/api/v1/auth/password/forgot", new { session.Contact })).StatusCode);
        var code = await fixture.CodeAsync(session.Contact, "ResetPassword");
        var reset = new { session.Contact, Code = code, NewPassword = "NewPassword123" };
        Assert.Equal(HttpStatusCode.NoContent, (await client.PostAsJsonAsync("/api/v1/auth/password/reset", reset)).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.PostAsJsonAsync("/api/v1/auth/password/reset", reset)).StatusCode);
        client.DefaultRequestHeaders.Authorization = new("Bearer", session.AccessToken);
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/v1/users/me")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.PostAsJsonAsync("/api/v1/auth/refresh", new { session.RefreshToken })).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.PostAsJsonAsync("/api/v1/auth/login", new { EmailOrPhone = session.Contact, Password = "Password123", CaptchaToken = "integration-valid" })).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await client.PostAsJsonAsync("/api/v1/auth/login", new { EmailOrPhone = session.Contact, Password = "NewPassword123", CaptchaToken = "integration-valid" })).StatusCode);
    }

    [Tv1PostgresFact]
    public async Task Update_me_ignores_privileged_fields()
    {
        var session = await fixture.RegisterAsync();
        using var client = fixture.Client();
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.PutAsJsonAsync("/api/v1/users/me", new { FullName = "Anonymous" })).StatusCode);
        client.DefaultRequestHeaders.Authorization = new("Bearer", session.AccessToken);
        Assert.Equal(HttpStatusCode.NoContent, (await client.PutAsJsonAsync("/api/v1/users/me", new { FullName = "Updated", AvatarUrl = "https://example.com/a.png", Roles = new[] { "Admin" }, Email = "attacker@example.com", Status = "Banned" })).StatusCode);
        using var profile = JsonDocument.Parse(await client.GetStringAsync("/api/v1/users/me"));
        Assert.Equal("Updated", profile.RootElement.GetProperty("fullName").GetString());
        Assert.Equal(session.Contact, profile.RootElement.GetProperty("email").GetString());
        Assert.Equal("Driver", profile.RootElement.GetProperty("roles")[0].GetString());
        Assert.Equal(JsonValueKind.Null, profile.RootElement.GetProperty("avatarUrl").ValueKind);
    }

    [Tv1PostgresFact]
    public async Task Kyc_submission_is_masked_private_and_reviewable_by_admin()
    {
        const string path = "/api/v1/users/me/kyc";
        var driver = await fixture.RegisterAsync();
        var admin = await fixture.RegisterAsync();
        await using (var scope = fixture.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<UserDbContext>();
            var adminId = await db.Users.Where(u => u.Email == admin.Contact).Select(u => u.Id).SingleAsync();
            db.UserRoles.Add(new UserService.Domain.Entities.UserRole { UserId = adminId, Role = ParkingManagement.SharedKernel.Enums.UserRoleType.Admin });
            await db.SaveChangesAsync();
        }
        using var client = fixture.Client();
        client.DefaultRequestHeaders.Authorization = new("Bearer", driver.AccessToken);
        using var image = new SkiaSharp.SKBitmap(1200, 760);
        using var encoded = image.Encode(SkiaSharp.SKEncodedImageFormat.Png, 100);
        MultipartFormDataContent Form(string number)
        {
            var form = new MultipartFormDataContent { { new StringContent("CitizenId"), "documentType" }, { new StringContent(number), "documentNumber" } };
            form.Add(new ByteArrayContent(encoded.ToArray()), "frontImage", "front.png");
            form.Add(new ByteArrayContent(encoded.ToArray()), "backImage", "back.png");
            return form;
        }
        Assert.Equal("NotSubmitted", (await client.GetFromJsonAsync<UserService.Application.Features.Kyc.KycStatusDto>(path))!.Status);
        using (var bad = Form("12345")) Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsync(path, bad)).StatusCode);
        using (var form = Form("079201008921"))
        {
            using var submitted = await client.PostAsync(path, form);
            Assert.Equal(HttpStatusCode.Accepted, submitted.StatusCode);
            var status = (await submitted.Content.ReadFromJsonAsync<UserService.Application.Features.Kyc.KycStatusDto>())!;
            Assert.Equal(("Pending", "CitizenId", "••••••••8921"), (status.Status, status.DocumentType, status.MaskedNumber));
        }
        using (var again = Form("079201008921")) Assert.Equal(HttpStatusCode.Conflict, (await client.PostAsync(path, again)).StatusCode);

        using var front = await client.GetAsync(path + "/images/front");
        Assert.Equal(HttpStatusCode.OK, front.StatusCode);
        Assert.Equal("image/jpeg", front.Content.Headers.ContentType!.MediaType);
        Assert.True(front.Headers.CacheControl?.NoStore);
        using (var anonymous = fixture.Client())
            Assert.Equal(HttpStatusCode.Unauthorized, (await anonymous.GetAsync(path + "/images/front")).StatusCode);
        await using (var scope = fixture.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<UserDbContext>();
            var user = await db.Users.SingleAsync(u => u.Email == driver.Contact);
            Assert.DoesNotContain("079201008921", user.KycNumberEncrypted);
            Assert.StartsWith("kyc:", user.KycFrontImageUrl);
        }

        var view = await client.GetFromJsonAsync<UserService.Application.Features.Privacy.PersonalDataView>("/api/v1/users/me/personal-data");
        Assert.Equal(("••••••••8921", true), (view!.KycMaskedNumber, view.KycImagesStored));
        Assert.Contains(view.RecentSecurityEvents, e => e.Type == "KycSubmitted");

        Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync("/api/v1/users/kyc")).StatusCode);
        using var adminClient = fixture.Client();
        adminClient.DefaultRequestHeaders.Authorization = new("Bearer", admin.AccessToken);
        var queue = (await adminClient.GetFromJsonAsync<UserService.Application.Features.Kyc.AdminKycItem[]>("/api/v1/users/kyc"))!;
        var driverId = await UserIdAsync(driver.Contact);
        var mine = Assert.Single(queue, q => q.UserId == driverId);
        Assert.Equal("••••••••8921", mine.MaskedNumber);
        Assert.Equal(HttpStatusCode.OK, (await adminClient.GetAsync("/api/v1/users/kyc/" + driverId + "/images/back")).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await adminClient.PostAsync("/api/v1/users/kyc/" + driverId + "/approve", null)).StatusCode);
        Assert.Equal("Verified", (await client.GetFromJsonAsync<UserService.Application.Features.Kyc.KycStatusDto>(path))!.Status);
        using var me = JsonDocument.Parse(await client.GetStringAsync("/api/v1/users/me"));
        Assert.Equal("Verified", me.RootElement.GetProperty("kycStatus").GetString());
    }

    private async Task<int> UserIdAsync(string contact)
    {
        await using var scope = fixture.Services.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<UserDbContext>().Users.Where(u => u.Email == contact).Select(u => u.Id).SingleAsync();
    }

    [Tv1PostgresFact]
    public async Task History_reads_bookings_for_token_user_only_and_attaches_payments()
    {
        var driver = await fixture.RegisterAsync();
        using var client = fixture.Client();
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/v1/users/me/history")).StatusCode);
        client.DefaultRequestHeaders.Authorization = new("Bearer", driver.AccessToken);
        var userId = await UserIdAsync(driver.Contact);

        // userId giả mạo trên query bị bỏ qua: máy chủ luôn dùng userId trong token.
        var page = (await client.GetFromJsonAsync<UserService.Application.Features.History.ParkingHistoryPage>("/api/v1/users/me/history?userId=1"))!;
        Assert.Contains(fixture.BookingCalls, c => c.Query == "?userId=" + userId && c.Auth == "Bearer " + driver.AccessToken);
        Assert.Equal([502, 501], page.Items.Select(i => i.BookingId));
        Assert.Equal((2, 1, 45000m), (page.Summary.TotalBookings, page.Summary.CompletedBookings, page.Summary.TotalSpent));
        Assert.Equal("HD-1", Assert.Single(page.Items[1].Payments).InvoiceNumber);
        var completed = (await client.GetFromJsonAsync<UserService.Application.Features.History.ParkingHistoryPage>("/api/v1/users/me/history?filter=completed"))!;
        Assert.Equal(501, Assert.Single(completed.Items).BookingId);
        Assert.Equal(HttpStatusCode.BadRequest, (await client.GetAsync("/api/v1/users/me/history?filter=bogus")).StatusCode);
    }

    [Tv1PostgresFact]
    public async Task Correction_request_carries_reason_back_to_the_user()
    {
        var driver = await fixture.RegisterAsync();
        using var client = fixture.Client();
        client.DefaultRequestHeaders.Authorization = new("Bearer", driver.AccessToken);
        const string path = "/api/v1/users/me/data-requests";
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsJsonAsync(path, new { Type = "Correction", Password = "Password123" })).StatusCode);
        using var sent = await client.PostAsJsonAsync(path, new { Type = "Correction", Password = "Password123", Reason = "SĐT mới 0912345678" });
        Assert.Equal(HttpStatusCode.Accepted, sent.StatusCode);
        var item = Assert.Single((await client.GetFromJsonAsync<UserService.Application.Features.Privacy.DataRequestItem[]>(path))!);
        Assert.Equal(("Correction", "SĐT mới 0912345678"), (item.Type, item.Reason));
    }

    [Tv1PostgresFact]
    public async Task Avatar_upload_serves_normalized_image_replaces_and_removes_owned_files()
    {
        var session = await fixture.RegisterAsync();
        using var client = fixture.Client();
        using var image = new SkiaSharp.SKBitmap(800, 400);
        using var encoded = image.Encode(SkiaSharp.SKEncodedImageFormat.Png, 100);
        var bytes = encoded.ToArray();
        MultipartFormDataContent Form(byte[] content)
        {
            var form = new MultipartFormDataContent();
            form.Add(new ByteArrayContent(content), "file", "../../untrusted.png");
            return form;
        }
        using (var form = Form(bytes))
            Assert.Equal(HttpStatusCode.Unauthorized, (await client.PostAsync("/api/v1/users/me/avatar", form)).StatusCode);
        client.DefaultRequestHeaders.Authorization = new("Bearer", session.AccessToken);
        using (var invalid = Form("not an image"u8.ToArray()))
            Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsync("/api/v1/users/me/avatar", invalid)).StatusCode);
        string? first = null;
        string? second = null;
        for (var i = 0; i < 2; i++)
        {
            using var form = Form(bytes);
            var uploaded = await client.PostAsync("/api/v1/users/me/avatar", form);
            Assert.Equal(HttpStatusCode.OK, uploaded.StatusCode);
            using var result = JsonDocument.Parse(await uploaded.Content.ReadAsStringAsync());
            var url = result.RootElement.GetProperty("avatarUrl").GetString()!;
            if (i == 0) first = url; else second = url;
            using var anonymous = fixture.Client();
            var response = await anonymous.GetAsync(url);
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            Assert.Equal("image/png", response.Content.Headers.ContentType!.MediaType);
            using var normalized = SkiaSharp.SKBitmap.Decode(await response.Content.ReadAsByteArrayAsync());
            Assert.Equal(512, normalized.Width); Assert.Equal(256, normalized.Height);
        }
        Assert.NotEqual(first, second);
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync(first)).StatusCode);
        // Updating name must never clear or overwrite an uploaded avatar.
        await client.PutAsJsonAsync("/api/v1/users/me", new { FullName = "Avatar user", AvatarUrl = "https://attacker.test/a.svg" });
        using var me = JsonDocument.Parse(await client.GetStringAsync("/api/v1/users/me"));
        Assert.Equal(second, me.RootElement.GetProperty("avatarUrl").GetString());
        Assert.Equal(HttpStatusCode.NoContent, (await client.DeleteAsync("/api/v1/users/me/avatar")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync(second)).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await client.DeleteAsync("/api/v1/users/me/avatar")).StatusCode);
    }


}

public sealed class Tv1ApiFixture : IDisposable
{
    public const int SecondLotOffset = 1_000_000;
    private readonly string root=Path.Combine(Path.GetTempPath(),"pm-tv1-api-"+Guid.NewGuid().ToString("N"));
    private readonly Dictionary<string,string?> original=[];
    private readonly DeliveryHandler delivery=new();
    private readonly ApiFactory factory;
    public IServiceProvider Services=>factory.Services;
    public Tv1ApiFixture()
    {
        Directory.CreateDirectory(root);using var rsa=RSA.Create(2048);
        var privatePath=Path.Combine(root,"private.key");var publicPath=Path.Combine(root,"public.key");
        File.WriteAllText(privatePath,rsa.ExportPkcs8PrivateKeyPem());File.WriteAllText(publicPath,rsa.ExportSubjectPublicKeyInfoPem());
        Set("Jwt__PublicKeyPath",publicPath);Set("Jwt__PrivateKeyPath",privatePath);
        Set("Security__EncryptionKey",Convert.ToBase64String(RandomNumberGenerator.GetBytes(32)));
        Set("Security__PrivateFilesPath",Path.Combine(root,"files"));Set("Database__SeedDemoData","false");
        Set("Security__AvatarPath", Path.Combine(root, "avatars"));
        Set("Security__AuthRequestsPerMinute","200");
        Set("Captcha__SecretKey","integration-test-secret");
        Set("Database__ApplyMigrationsOnStartup","true");Set("EventBus__Enabled","true");
        // A real RabbitMQ bus must never be constructed by this HTTP fixture (including /health).
        Set("EventBus__RabbitMq__Password", " ");
        Set("Integrations__ParkingServiceUrl", "https://parking.test/");
        Set("Integrations__BookingServiceUrl", "https://booking.test/");
        Set("Integrations__PaymentServiceUrl", "https://payment.test/");
        Set("Security__KycPath", Path.Combine(root, "kyc"));
        Set("Otp__Transport","Http");Set("Otp__DeliveryUrl","https://otp.test/delivery");
        if(Environment.GetEnvironmentVariable("TV1_TEST_DB") is {Length:>0} db)Set("ConnectionStrings__ServiceDb",db);
        factory=new ApiFactory(delivery);
    }
    private void Set(string name,string value){original[name]=Environment.GetEnvironmentVariable(name);Environment.SetEnvironmentVariable(name,value);}
    public HttpClient Client()=>factory.CreateClient();
    public ConcurrentBag<(string Query, string? Auth)> BookingCalls=>delivery.BookingCalls;
    public string Contact()=>"tv1-"+Guid.NewGuid().ToString("N")+"@example.com";
    public async Task<(string Contact,string AccessToken,string RefreshToken)> RegisterAsync(bool owner = false)
    {
        using var client=Client();var contact=Contact();
        Assert.Equal(HttpStatusCode.Accepted,(await client.PostAsJsonAsync("/api/v1/auth/register",new { FullName = "Test", Contact = contact, Password = "Password123", Role = owner ? "LotOwner" : "Driver", BusinessName = owner ? "Test Parking" : null })).StatusCode);
        var code=await CodeAsync(contact,"Register");
        Assert.Equal(HttpStatusCode.NoContent,(await client.PostAsJsonAsync("/api/v1/auth/otp/verify",new{Contact=contact,Code=code})).StatusCode);
        var response=await client.PostAsJsonAsync("/api/v1/auth/login",new{EmailOrPhone=contact,Password="Password123",CaptchaToken="integration-valid"});
        Assert.Equal(HttpStatusCode.OK,response.StatusCode);
        var tokens=(await response.Content.ReadFromJsonAsync<UserServiceApiTests.Tokens>())!;
        return(contact,tokens.AccessToken,tokens.RefreshToken);
    }
    public async Task<string> CodeAsync(string contact,string purpose)
    {
        for(var i=0;i<30;i++){if(delivery.Codes.TryGetValue(contact+":"+purpose,out var code))return code;await Task.Delay(1000);}
        throw new TimeoutException("Outbox chưa giao OTP cho test adapter.");
    }
    public void Dispose(){factory.Dispose();foreach(var item in original)Environment.SetEnvironmentVariable(item.Key,item.Value);if(Directory.Exists(root))Directory.Delete(root,true);}
    private sealed class ApiFactory(DeliveryHandler handler):WebApplicationFactory<UserService.API.Controllers.AuthController>
    {
        protected override void ConfigureWebHost(IWebHostBuilder builder)=>builder.ConfigureLogging(logging=>logging.ClearProviders().AddConsole()).ConfigureServices(services=>
        {
            services.AddScoped<UserService.Application.Interfaces.ICaptchaVerifier, TestCaptcha>();
            services.AddDataProtection().UseEphemeralDataProtectionProvider();
            services.AddHttpClient("IdentityIntegrations").ConfigurePrimaryHttpMessageHandler(()=>handler);
            services.AddSingleton<ParkingManagement.ServiceDefaults.Messaging.IRabbitMqPublisher, TestEventPublisher>();
            // Replace both transport and health-check dependencies, not only the hosted service.
            services.AddMassTransitTestHarness();
        });
    }
    private sealed class TestCaptcha : UserService.Application.Interfaces.ICaptchaVerifier
    {
        public Task VerifyAsync(string? token, CancellationToken ct = default)
        {
            if (token != "integration-valid") throw new UserService.Application.Interfaces.CaptchaException(false);
            return Task.CompletedTask;
        }
    }
    private sealed class TestEventPublisher : ParkingManagement.ServiceDefaults.Messaging.IRabbitMqPublisher
    {
        public Task PublishAsync(ParkingManagement.ServiceDefaults.Messaging.IntegrationEventEnvelope envelope, CancellationToken ct)
            => Task.CompletedTask;
    }
    private sealed class DeliveryHandler:HttpMessageHandler
    {
        public ConcurrentDictionary<string,string> Codes {get;}=new();
        public ConcurrentBag<(string Query, string? Auth)> BookingCalls {get;}=new();
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,CancellationToken ct)
        {
            if (request.RequestUri is { Host: "booking.test" } booking)
            {
                BookingCalls.Add((booking.Query, request.Headers.Authorization?.ToString()));
                var start = new DateTime(2026, 10, 20, 8, 0, 0, DateTimeKind.Utc);
                return new HttpResponseMessage(HttpStatusCode.OK) { Content = JsonContent.Create(new object[]
                {
                    new { Id = 501, Code = "BK-0501", Status = "Completed", ParkingLotName = "Bãi Lê Lợi", SlotCode = "A-04", PlateNumber = "51K-889.32",
                        StartAtUtc = start, EndAtUtc = start.AddHours(3), TotalAmount = 45000m },
                    new { Id = 502, Code = "BK-0502", Status = "Cancelled", ParkingLotName = "Bãi Nguyễn Huệ", SlotCode = (string?)null, PlateNumber = "51K-889.32",
                        StartAtUtc = start.AddDays(5), EndAtUtc = start.AddDays(5).AddHours(1), TotalAmount = 0m },
                }) };
            }
            if (request.RequestUri is { Host: "payment.test" } payment)
                return new HttpResponseMessage(HttpStatusCode.OK) { Content = JsonContent.Create(payment.Query.Contains("bookingId=501")
                    ? new object[] { new { Id = 1, Code = "PM-1", BookingId = 501, BookingCode = "BK-0501", Method = "Vnpay", Status = "Succeeded",
                        Amount = 45000m, RefundedAmount = 0m, ProviderTransactionId = "x", PaidAtUtc = DateTime.UtcNow, InvoiceNumber = "HD-1" } }
                    : Array.Empty<object>()) };
            if (request.RequestUri is { Host: "parking.test" } uri &&
                int.TryParse(uri.Segments.Last(), out var ownerId))
                // Mỗi chủ bãi có 2 bãi giả lập: Id = ownerProfileId và ownerProfileId + SecondLotOffset.
                return new HttpResponseMessage(HttpStatusCode.OK) { Content = JsonContent.Create(new[]
                    { new { Id = ownerId, Name = "Lot A" }, new { Id = ownerId + SecondLotOffset, Name = "Lot B" } }) };

            if(request.RequestUri!.Host=="otp.test")
            {
                using var data=JsonDocument.Parse(await request.Content!.ReadAsStringAsync(ct));var json=data.RootElement;
                Codes[json.GetProperty("destination").GetString()+":"+json.GetProperty("purpose").GetString()]=json.GetProperty("code").GetString()!;
            }
            return new HttpResponseMessage(HttpStatusCode.OK);
        }
    }
}
