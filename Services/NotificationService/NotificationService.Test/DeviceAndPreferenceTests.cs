using NotificationService.Application.Features.DeviceTokens;
using NotificationService.Application.Features.Preferences;
using NSubstitute;

namespace NotificationService.Test;

/// <summary>
/// Unit Tests cho Device Token (S1-T602) và Notification Preferences (S4)
/// – theo Test Plan v3 (TESTING_GUIDE.md).
/// RegisterDevice/UnregisterDevice: validation UserId, Token, DeviceId.
/// GetPreferences: trả default (bật email/sms/push/transactional/urgent, tắt marketing).
/// UpdatePreferences: validation input.
/// </summary>
public class DeviceAndPreferenceTests
{
    // ===== RegisterDeviceTokenUseCase =====

    [Fact]
    public async Task RegisterDevice_valid_request_completes_without_error()
    {
        // Arrange
        var useCase = new RegisterDeviceTokenUseCase();
        var request = new RegisterDeviceTokenRequest(
            UserId: 5, Token: "fcm-token-abc123", DeviceId: "device-01", DeviceType: "Android");

        // Act & Assert: hợp lệ thì không ném exception
        await useCase.ExecuteAsync(request);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-2)]
    public async Task RegisterDevice_invalid_user_id_throws_argument_exception(int userId)
    {
        // Arrange
        var useCase = new RegisterDeviceTokenUseCase();
        var request = new RegisterDeviceTokenRequest(userId, "token", "device-01");

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentException>(() => useCase.ExecuteAsync(request));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task RegisterDevice_blank_token_throws_argument_exception(string token)
    {
        // Arrange: token FCM rỗng/chỉ khoảng trắng là vô hiệu
        var useCase = new RegisterDeviceTokenUseCase();
        var request = new RegisterDeviceTokenRequest(5, token, "device-01");

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentException>(() => useCase.ExecuteAsync(request));
    }

    [Fact]
    public async Task RegisterDevice_null_request_throws_argument_null_exception()
    {
        // Arrange
        var useCase = new RegisterDeviceTokenUseCase();

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentNullException>(
            () => useCase.ExecuteAsync(null!));
    }

    // ===== UnregisterDeviceTokenUseCase =====

    [Fact]
    public async Task UnregisterDevice_valid_input_completes_without_error()
    {
        // Arrange
        var useCase = new UnregisterDeviceTokenUseCase();

        // Act & Assert
        await useCase.ExecuteAsync(userId: 5, deviceId: "device-01");
    }

    [Fact]
    public async Task UnregisterDevice_invalid_user_id_throws_argument_exception()
    {
        // Arrange
        var useCase = new UnregisterDeviceTokenUseCase();

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentException>(
            () => useCase.ExecuteAsync(userId: 0, deviceId: "device-01"));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task UnregisterDevice_blank_device_id_throws_argument_exception(string deviceId)
    {
        // Arrange
        var useCase = new UnregisterDeviceTokenUseCase();

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentException>(
            () => useCase.ExecuteAsync(userId: 5, deviceId: deviceId));
    }

    // ===== GetNotificationPreferencesUseCase (S4) =====

    [Fact]
    public async Task GetPreferences_valid_user_returns_default_preferences()
    {
        // Arrange: mặc định bật mọi kênh hữu ích, TẮT marketing (không spam người dùng)
        var useCase = new GetNotificationPreferencesUseCase();

        // Act
        var prefs = await useCase.ExecuteAsync(5);

        // Assert
        Assert.Equal(5, prefs.UserId);
        Assert.True(prefs.EmailEnabled);
        Assert.True(prefs.SmsEnabled);
        Assert.True(prefs.PushEnabled);
        Assert.True(prefs.TransactionalNotifications);
        Assert.True(prefs.UrgentNotifications);
        Assert.False(prefs.MarketingNotifications);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public async Task GetPreferences_invalid_user_id_throws_argument_exception(int userId)
    {
        // Arrange
        var useCase = new GetNotificationPreferencesUseCase();

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentException>(() => useCase.ExecuteAsync(userId));
    }

    // ===== UpdateNotificationPreferencesUseCase (S4) =====

    [Fact]
    public async Task UpdatePreferences_valid_request_completes_without_error()
    {
        // Arrange: user tắt SMS, bật marketing
        var useCase = new UpdateNotificationPreferencesUseCase();
        var request = new UpdatePreferencesRequest(SmsEnabled: false, MarketingNotifications: true);

        // Act & Assert
        await useCase.ExecuteAsync(5, request);
    }

    [Fact]
    public async Task UpdatePreferences_null_request_throws_argument_null_exception()
    {
        // Arrange
        var useCase = new UpdateNotificationPreferencesUseCase();

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentNullException>(() => useCase.ExecuteAsync(5, null!));
    }

    [Fact]
    public async Task UpdatePreferences_invalid_user_id_throws_argument_exception()
    {
        // Arrange
        var useCase = new UpdateNotificationPreferencesUseCase();

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentException>(
            () => useCase.ExecuteAsync(0, new UpdatePreferencesRequest(EmailEnabled: false)));
    }
}
