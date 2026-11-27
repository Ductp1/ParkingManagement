using System.Text.Json;
using BookingService.Application.Features.Bookings;
using BookingService.Infrastructure.Integrations;
using Microsoft.Extensions.Logging.Abstractions;
using ParkingManagement.ServiceDefaults.Messaging;
using ParkingManagement.SharedKernel.Contracts;
using Xunit;

namespace BookingService.Test;

/// <summary>
/// Unit Tests cho PaymentIntegrationEventHandler: Kiểm tra việc deserialize và chuyển tiếp
/// các event PaymentSucceeded, PaymentFailed vào UseCase trong quy trình Saga.
/// </summary>
public class PaymentIntegrationEventHandlerTests
{
    [Fact]
    public async Task HandleAsync_PaymentSucceeded_calls_usecase_succeeded()
    {
        // Arrange
        var fakeUseCase = new FakePaymentResultUseCase();
        var handler = new PaymentIntegrationEventHandler(fakeUseCase, NullLogger<PaymentIntegrationEventHandler>.Instance);

        var payloadObj = new PaymentSucceeded(501, 101, null, 50000, "VNPAY");
        var jsonBytes = JsonSerializer.SerializeToUtf8Bytes(payloadObj);
        using var doc = JsonDocument.Parse(jsonBytes);

        var envelope = new IntegrationEventEnvelope(
            Guid.NewGuid(),
            nameof(PaymentSucceeded),
            DateTime.UtcNow,
            doc.RootElement.Clone());

        // Act
        await handler.HandleAsync(envelope, CancellationToken.None);

        // Assert
        Assert.NotNull(fakeUseCase.LastSucceededEvent);
        Assert.Equal(501, fakeUseCase.LastSucceededEvent.PaymentId);
        Assert.Equal(101, fakeUseCase.LastSucceededEvent.BookingId);
        Assert.Equal(50000, fakeUseCase.LastSucceededEvent.Amount);
    }

    [Fact]
    public async Task HandleAsync_PaymentFailed_calls_usecase_failed()
    {
        // Arrange
        var fakeUseCase = new FakePaymentResultUseCase();
        var handler = new PaymentIntegrationEventHandler(fakeUseCase, NullLogger<PaymentIntegrationEventHandler>.Instance);

        var payloadObj = new PaymentFailed(502, 102, "Khách hàng hủy giao dịch");
        var jsonBytes = JsonSerializer.SerializeToUtf8Bytes(payloadObj);
        using var doc = JsonDocument.Parse(jsonBytes);

        var envelope = new IntegrationEventEnvelope(
            Guid.NewGuid(),
            nameof(PaymentFailed),
            DateTime.UtcNow,
            doc.RootElement.Clone());

        // Act
        await handler.HandleAsync(envelope, CancellationToken.None);

        // Assert
        Assert.NotNull(fakeUseCase.LastFailedEvent);
        Assert.Equal(502, fakeUseCase.LastFailedEvent.PaymentId);
        Assert.Equal(102, fakeUseCase.LastFailedEvent.BookingId);
        Assert.Equal("Khách hàng hủy giao dịch", fakeUseCase.LastFailedEvent.Reason);
    }

    private sealed class FakePaymentResultUseCase : IProcessPaymentResultUseCase
    {
        public PaymentSucceeded? LastSucceededEvent { get; private set; }
        public PaymentFailed? LastFailedEvent { get; private set; }

        public Task<BookingDetailDto> ProcessPaymentSucceededAsync(PaymentSucceeded @event, CancellationToken cancellationToken = default)
        {
            LastSucceededEvent = @event;
            return Task.FromResult(new BookingDetailDto(
                @event.BookingId ?? 0, "BK-TEST", "Confirmed", 1, 1, "51A-12345", "Car4", 1, "Lot 1", 1, 1, "A-01",
                DateTime.UtcNow, DateTime.UtcNow.AddHours(2), null, @event.Amount, 0, @event.Amount, null, null, [], "qr"));
        }

        public Task<BookingDetailDto> ProcessPaymentFailedAsync(PaymentFailed @event, CancellationToken cancellationToken = default)
        {
            LastFailedEvent = @event;
            return Task.FromResult(new BookingDetailDto(
                @event.BookingId ?? 0, "BK-TEST", "Cancelled", 1, 1, "51A-12345", "Car4", 1, "Lot 1", 1, 1, "A-01",
                DateTime.UtcNow, DateTime.UtcNow.AddHours(2), null, 0, 0, 0, null, null, [], "qr"));
        }
    }
}

