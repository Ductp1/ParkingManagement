using NotificationService.Application.Features.Ingestion;
using NSubstitute;
using ParkingManagement.SharedKernel.Contracts;
using ParkingManagement.SharedKernel.Enums;

namespace NotificationService.Test;

/// <summary>
/// US-083 (T-832): BookingConfirmationNotifications — render i18n + gửi InApp/Email với DedupeKey
/// evt:{EventId}:{Channel}. Fakes NSubstitute (không DB, không broker) theo pattern NotificationDispatcherTests.
/// </summary>
public class BookingConfirmationNotificationsTests
{
    private readonly ITemplateRenderer _renderer = Substitute.For<ITemplateRenderer>();
    private readonly IUserNotificationSettingsPort _settings = Substitute.For<IUserNotificationSettingsPort>();
    private readonly RecordingSender _sender = new();

    private BookingConfirmationNotifications Create() => new(_renderer, _settings, _sender);

    public BookingConfirmationNotificationsTests()
    {
        _renderer.RenderAsync(Arg.Any<string>(), Arg.Any<NotificationChannel>(), Arg.Any<string>(),
                Arg.Any<IReadOnlyDictionary<string, string>>(), Arg.Any<CancellationToken>())
            .Returns(new RenderedTemplate("Tiêu đề", "Nội dung", 7));
        _settings.GetLanguageAsync(Arg.Any<int>(), Arg.Any<CancellationToken>()).Returns("vi");
    }

    private static BookingConfirmed ConfirmedEvent() => new(
        BookingId: 12, Code: "BK-20261103-0001", UserId: 5, ParkingLotId: 2, OwnerProfileId: 7,
        ParkingLotName: "Bãi Vincom", SlotId: 3, SlotCode: "A-01", PlateNumber: "51F-123.45",
        StartAtUtc: new DateTime(2026, 11, 5, 2, 0, 0, DateTimeKind.Utc),
        EndAtUtc: new DateTime(2026, 11, 5, 5, 0, 0, DateTimeKind.Utc),
        TotalAmount: 120000m);

    [Fact]
    public async Task BookingConfirmed_sends_inapp_and_email_with_per_channel_dedupe_keys()
    {
        await Create().HandleBookingConfirmedAsync(ConfirmedEvent(), Guid.Parse("11111111-1111-1111-1111-111111111111"));

        Assert.Equal(2, _sender.Sent.Count);
        Assert.Equal(NotificationChannel.InApp, _sender.Sent[0].Channel);
        Assert.Equal(NotificationChannel.Email, _sender.Sent[1].Channel);
        Assert.All(_sender.Sent, s => Assert.Equal("BOOKING_CONFIRMED", s.TemplateKey));
        Assert.Equal("evt:11111111-1111-1111-1111-111111111111:InApp", _sender.Sent[0].DedupeKey);
        Assert.Equal("evt:11111111-1111-1111-1111-111111111111:Email", _sender.Sent[1].DedupeKey);
        Assert.Contains("\"bookingId\":12", _sender.Sent[0].DataJson);
    }

    [Fact]
    public async Task BookingConfirmed_uses_user_language_for_template_selection()
    {
        _settings.GetLanguageAsync(5, Arg.Any<CancellationToken>()).Returns("en");

        await Create().HandleBookingConfirmedAsync(ConfirmedEvent(), Guid.NewGuid());

        await _renderer.Received(2).RenderAsync("BOOKING_CONFIRMED", Arg.Any<NotificationChannel>(), "en",
            Arg.Any<IReadOnlyDictionary<string, string>>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task BookingConfirmed_passes_booking_placeholders_to_renderer()
    {
        IReadOnlyDictionary<string, string>? captured = null;
        _renderer.RenderAsync(Arg.Any<string>(), Arg.Any<NotificationChannel>(), Arg.Any<string>(),
                Arg.Do<IReadOnlyDictionary<string, string>>(d => captured = d), Arg.Any<CancellationToken>())
            .Returns(new RenderedTemplate("t", "b", 1));

        await Create().HandleBookingConfirmedAsync(ConfirmedEvent(), Guid.NewGuid());

        Assert.NotNull(captured);
        Assert.Equal("BK-20261103-0001", captured!["BookingCode"]);
        Assert.Equal("Bãi Vincom", captured["LotName"]);
        Assert.Contains("120.000", captured["Amount"]); // format VN
    }

    [Fact]
    public async Task PaymentCompleted_failed_sends_inapp_payment_failed_with_reason()
    {
        var evt = new BookingPaymentCompleted(99, 12, "BK-20261103-0001", 5, 120000m, "VNPAY",
            Succeeded: false, FailureReason: "Mã phản hồi 24");

        await Create().HandleBookingPaymentCompletedAsync(evt, Guid.NewGuid());

        var sent = Assert.Single(_sender.Sent);
        Assert.Equal(NotificationChannel.InApp, sent.Channel);
        Assert.Equal("PAYMENT_FAILED", sent.TemplateKey);
        await _renderer.Received(1).RenderAsync("PAYMENT_FAILED", NotificationChannel.InApp, "vi",
            Arg.Is<IReadOnlyDictionary<string, string>>(d => d["BookingCode"] == "BK-20261103-0001" && d["Reason"].Contains("24")),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task PaymentCompleted_succeeded_sends_inapp_payment_succeeded()
    {
        var evt = new BookingPaymentCompleted(99, 12, "BK-20261103-0001", 5, 120000m, "VNPAY",
            Succeeded: true, FailureReason: null);

        await Create().HandleBookingPaymentCompletedAsync(evt, Guid.NewGuid());

        var sent = Assert.Single(_sender.Sent);
        Assert.Equal("PAYMENT_SUCCEEDED", sent.TemplateKey);
    }

    [Fact]
    public async Task PaymentCompleted_uses_user_language_for_template_selection()
    {
        _settings.GetLanguageAsync(5, Arg.Any<CancellationToken>()).Returns("en");

        var evt = new BookingPaymentCompleted(99, 12, "BK-20261103-0001", 5, 120000m, "VNPAY",
            Succeeded: true, FailureReason: null);
        await Create().HandleBookingPaymentCompletedAsync(evt, Guid.NewGuid());

        await _renderer.Received(1).RenderAsync("PAYMENT_SUCCEEDED", NotificationChannel.InApp, "en",
            Arg.Any<IReadOnlyDictionary<string, string>>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task PaymentCompleted_succeeded_passes_amount_method_and_booking_placeholders()
    {
        IReadOnlyDictionary<string, string>? captured = null;
        _renderer.RenderAsync(Arg.Any<string>(), Arg.Any<NotificationChannel>(), Arg.Any<string>(),
                Arg.Do<IReadOnlyDictionary<string, string>>(d => captured = d), Arg.Any<CancellationToken>())
            .Returns(new RenderedTemplate("t", "b", 1));

        var evt = new BookingPaymentCompleted(99, 12, "BK-20261103-0001", 5, 120000m, "VNPAY",
            Succeeded: true, FailureReason: null);
        await Create().HandleBookingPaymentCompletedAsync(evt, Guid.NewGuid());

        Assert.NotNull(captured);
        Assert.Equal("BK-20261103-0001", captured!["BookingCode"]);
        Assert.Equal("120.000 ₫", captured["Amount"]); // format VN
        Assert.Equal("VNPAY", captured["Method"]);
    }

    /// <summary>Sender ghi lại mọi lượt gửi để assert (thay vì mock mờ).</summary>
    private sealed class RecordingSender : ITransactionalNotificationSender
    {
        public List<(int UserId, NotificationChannel Channel, string TemplateKey, string Title, string Body, string? DataJson, string? DedupeKey)> Sent { get; } = [];

        public Task SendAsync(int userId, NotificationChannel channel, string templateKey, string title, string body,
            string? dataJson, string? dedupeKey, CancellationToken cancellationToken = default)
        {
            Sent.Add((userId, channel, templateKey, title, body, dataJson, dedupeKey));
            return Task.CompletedTask;
        }
    }
}
