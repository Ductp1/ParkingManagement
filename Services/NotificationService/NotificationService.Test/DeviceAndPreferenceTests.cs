using Microsoft.Extensions.Time.Testing;
using NotificationService.Application.Features.DeviceTokens;
using NotificationService.Application.Features.Preferences;
using NotificationService.Domain.Entities;
using NSubstitute;
using ParkingManagement.SharedKernel.Enums;
using ParkingManagement.SharedKernel.Exceptions;

namespace NotificationService.Test;

/// <summary>
/// Unit Tests cho Device Token (S1-T602) và Notification Preferences (S4)
/// – theo Test Plan v3 (TESTING_GUIDE.md). Bản PERSIST THẬT: repository thay bằng
/// NSubstitute → verify use case ghi/đọc đúng mà không cần DB.
/// </summary>
public class DeviceAndPreferenceTests
{
    private readonly IDeviceTokenRepository _deviceTokens = Substitute.For<IDeviceTokenRepository>();
    private readonly INotificationPreferenceRepository _preferences = Substitute.For<INotificationPreferenceRepository>();
    private readonly FakeTimeProvider _clock = new();

    // ===== RegisterDeviceTokenUseCase =====

    [Fact]
    public async Task RegisterDevice_new_token_adds_active_row()
    {
        // Arrange: token chưa tồn tại
        _deviceTokens.GetByUserAndTokenAsync(5, "fcm-token-abc", Arg.Any<CancellationToken>())
            .Returns((DeviceToken?)null);
        DeviceToken? added = null;
        await _deviceTokens.AddAsync(Arg.Do<DeviceToken>(t => added = t), Arg.Any<CancellationToken>());
        var useCase = new RegisterDeviceTokenUseCase(_deviceTokens, _clock);

        // Act
        await useCase.ExecuteAsync(new RegisterDeviceTokenRequest(5, "fcm-token-abc", "Samsung A55", "Android"));

        // Assert: row mới, active, đúng platform
        Assert.NotNull(added);
        Assert.Equal(5, added!.UserId);
        Assert.Equal("fcm-token-abc", added.Token);
        Assert.Equal("Android", added.Platform);
        Assert.False(added.IsRevoked);
    }

    [Fact]
    public async Task RegisterDevice_existing_revoked_token_reactivates_instead_of_duplicate()
    {
        // Arrange: token đã có nhưng đã revoked (vd re-install app)
        var existing = new DeviceToken { UserId = 5, Token = "fcm-token-abc", Platform = "Android", IsRevoked = true };
        _deviceTokens.GetByUserAndTokenAsync(5, "fcm-token-abc", Arg.Any<CancellationToken>()).Returns(existing);
        var useCase = new RegisterDeviceTokenUseCase(_deviceTokens, _clock);

        // Act
        await useCase.ExecuteAsync(new RegisterDeviceTokenRequest(5, "fcm-token-abc", "Samsung A55", "Android"));

        // Assert: UPDATE (không AddAsync) + bật lại token
        await _deviceTokens.Received(1).UpdateAsync(existing, Arg.Any<CancellationToken>());
        await _deviceTokens.DidNotReceiveWithAnyArgs().AddAsync(null!);
        Assert.False(existing.IsRevoked);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-2)]
    public async Task RegisterDevice_invalid_user_id_throws_argument_exception(int userId)
    {
        var useCase = new RegisterDeviceTokenUseCase(_deviceTokens, _clock);

        await Assert.ThrowsAsync<ArgumentException>(
            () => useCase.ExecuteAsync(new RegisterDeviceTokenRequest(userId, "token", "device-01")));
        await _deviceTokens.DidNotReceiveWithAnyArgs().AddAsync(null!);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task RegisterDevice_blank_token_throws_argument_exception(string token)
    {
        var useCase = new RegisterDeviceTokenUseCase(_deviceTokens, _clock);

        await Assert.ThrowsAsync<ArgumentException>(
            () => useCase.ExecuteAsync(new RegisterDeviceTokenRequest(5, token, "device-01")));
        await _deviceTokens.DidNotReceiveWithAnyArgs().AddAsync(null!);
    }

    [Fact]
    public async Task RegisterDevice_null_request_throws_argument_null_exception()
    {
        var useCase = new RegisterDeviceTokenUseCase(_deviceTokens, _clock);
        await Assert.ThrowsAsync<ArgumentNullException>(() => useCase.ExecuteAsync(null!));
    }

    // ===== UnregisterDeviceTokenUseCase =====

    [Fact]
    public async Task UnregisterDevice_known_token_marks_revoked()
    {
        _deviceTokens.RevokeByUserAndTokenAsync(5, "fcm-token-abc", Arg.Any<CancellationToken>()).Returns(1);
        var useCase = new UnregisterDeviceTokenUseCase(_deviceTokens);

        await useCase.ExecuteAsync(5, "fcm-token-abc");

        await _deviceTokens.Received(1).RevokeByUserAndTokenAsync(5, "fcm-token-abc", Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task UnregisterDevice_unknown_token_throws_not_found()
    {
        _deviceTokens.RevokeByUserAndTokenAsync(Arg.Any<int>(), Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(0);
        var useCase = new UnregisterDeviceTokenUseCase(_deviceTokens);

        await Assert.ThrowsAsync<NotFoundException>(() => useCase.ExecuteAsync(5, "unknown-token"));
    }

    [Fact]
    public async Task UnregisterDevice_invalid_user_id_throws_argument_exception()
    {
        var useCase = new UnregisterDeviceTokenUseCase(_deviceTokens);
        await Assert.ThrowsAsync<ArgumentException>(() => useCase.ExecuteAsync(0, "device-01"));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task UnregisterDevice_blank_device_id_throws_argument_exception(string deviceId)
    {
        var useCase = new UnregisterDeviceTokenUseCase(_deviceTokens);
        await Assert.ThrowsAsync<ArgumentException>(() => useCase.ExecuteAsync(5, deviceId));
    }

    // ===== GetNotificationPreferencesUseCase (S4) =====

    [Fact]
    public async Task GetPreferences_maps_persisted_rows_and_fills_defaults()
    {
        // Arrange: user tắt Email + bật Marketing; các dòng không tồn tại → dùng default
        _preferences.GetByUserAsync(5, Arg.Any<CancellationToken>()).Returns(new List<NotificationPreference>
        {
            new() { UserId = 5, TemplateKey = NotificationPreferenceKeys.AllTemplates, Channel = NotificationChannel.Email, IsEnabled = false },
            new() { UserId = 5, TemplateKey = NotificationPreferenceKeys.Marketing, Channel = NotificationChannel.InApp, IsEnabled = true }
        });
        var useCase = new GetNotificationPreferencesUseCase(_preferences);

        // Act
        var prefs = await useCase.ExecuteAsync(5);

        // Assert: giá trị lưu DB thắng default; dòng thiếu dùng default (marketing = false)
        Assert.Equal(5, prefs.UserId);
        Assert.False(prefs.EmailEnabled);
        Assert.True(prefs.MarketingNotifications);
        Assert.True(prefs.SmsEnabled);
        Assert.True(prefs.PushEnabled);
        Assert.True(prefs.TransactionalNotifications);
        Assert.True(prefs.UrgentNotifications);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public async Task GetPreferences_invalid_user_id_throws_argument_exception(int userId)
    {
        var useCase = new GetNotificationPreferencesUseCase(_preferences);
        await Assert.ThrowsAsync<ArgumentException>(() => useCase.ExecuteAsync(userId));
    }

    // ===== UpdateNotificationPreferencesUseCase (S4) =====

    [Fact]
    public async Task UpdatePreferences_persists_each_changed_flag()
    {
        var useCase = new UpdateNotificationPreferencesUseCase(_preferences);

        await useCase.ExecuteAsync(5, new UpdatePreferencesRequest(SmsEnabled: false, MarketingNotifications: true));

        await _preferences.Received(1).UpsertAsync(
            Arg.Is<NotificationPreference>(p => p.UserId == 5
                && p.TemplateKey == NotificationPreferenceKeys.AllTemplates
                && p.Channel == NotificationChannel.Sms
                && !p.IsEnabled),
            Arg.Any<CancellationToken>());
        await _preferences.Received(1).UpsertAsync(
            Arg.Is<NotificationPreference>(p => p.UserId == 5
                && p.TemplateKey == NotificationPreferenceKeys.Marketing
                && p.Channel == NotificationChannel.InApp
                && p.IsEnabled),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task UpdatePreferences_null_flags_persist_nothing()
    {
        var useCase = new UpdateNotificationPreferencesUseCase(_preferences);

        await useCase.ExecuteAsync(5, new UpdatePreferencesRequest());

        await _preferences.DidNotReceiveWithAnyArgs().UpsertAsync(null!);
    }

    [Fact]
    public async Task UpdatePreferences_null_request_throws_argument_null_exception()
    {
        var useCase = new UpdateNotificationPreferencesUseCase(_preferences);
        await Assert.ThrowsAsync<ArgumentNullException>(() => useCase.ExecuteAsync(5, null!));
    }

    [Fact]
    public async Task UpdatePreferences_invalid_user_id_throws_argument_exception()
    {
        var useCase = new UpdateNotificationPreferencesUseCase(_preferences);
        await Assert.ThrowsAsync<ArgumentException>(
            () => useCase.ExecuteAsync(0, new UpdatePreferencesRequest(EmailEnabled: false)));
    }
}
