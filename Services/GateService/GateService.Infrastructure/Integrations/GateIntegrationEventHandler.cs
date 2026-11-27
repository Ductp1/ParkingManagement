using System.Text.Json;
using GateService.Application.Features.Shifts;
using Microsoft.Extensions.Logging;
using ParkingManagement.ServiceDefaults.Messaging;
using ParkingManagement.SharedKernel.Contracts;
using ParkingManagement.SharedKernel.Enums;

namespace GateService.Infrastructure.Integrations;

/// <summary>
/// US-076: đóng ngay ca đang mở khi người trực bị gỡ / đình chỉ, chủ bãi bị khóa hoặc bãi bị đình chỉ.
/// Dừng khẩn cấp (LotEmergencyStopped) KHÔNG đóng ca: cổng vào đã bị chặn ở luật nhận xe, người trực vẫn phải cho xe ra được.
/// </summary>
public sealed class GateIntegrationEventHandler(ShiftLifecycleUseCases lifecycle, ILogger<GateIntegrationEventHandler> logger)
    : IIntegrationEventHandler
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    public async Task HandleAsync(IntegrationEventEnvelope envelope, CancellationToken cancellationToken)
    {
        var closed = 0;
        switch (envelope.EventType)
        {
            case nameof(StaffAssignmentRevoked):
            {
                var e = Deserialize<StaffAssignmentRevoked>(envelope);
                closed = await lifecycle.AutoCloseAsync(s => s.Status == ShiftStatus.Open && s.StaffUserId == e.StaffUserId
                    && s.ParkingLotId == e.ParkingLotId, ShiftClosedReason.StaffRevoked, cancellationToken);
                break;
            }
            case nameof(UserSuspended):
            {
                var e = Deserialize<UserSuspended>(envelope);
                closed = await lifecycle.AutoCloseAsync(s => s.Status == ShiftStatus.Open && s.StaffUserId == e.UserId,
                    ShiftClosedReason.StaffSuspended, cancellationToken);
                break;
            }
            case nameof(OwnerProfileLocked):
            {
                var e = Deserialize<OwnerProfileLocked>(envelope);
                closed = await lifecycle.AutoCloseAsync(s => s.Status == ShiftStatus.Open && s.OwnerProfileId == e.OwnerProfileId,
                    ShiftClosedReason.OwnerLocked, cancellationToken);
                break;
            }
            case nameof(ParkingLotSuspended):
            {
                var e = Deserialize<ParkingLotSuspended>(envelope);
                closed = await lifecycle.AutoCloseAsync(s => s.Status == ShiftStatus.Open && s.ParkingLotId == e.ParkingLotId,
                    ShiftClosedReason.LotSuspended, cancellationToken);
                break;
            }
            default:
                logger.LogInformation("Event {EventType} (eventId={EventId}) không có xử lý ở GateService.", envelope.EventType, envelope.EventId);
                return;
        }
        if (closed > 0) logger.LogInformation("US-076: đóng {Count} ca do {EventType}.", closed, envelope.EventType);
    }

    private static T Deserialize<T>(IntegrationEventEnvelope envelope) where T : class
        => JsonSerializer.Deserialize<T>(envelope.Payload.GetRawText(), Json)
           ?? throw new JsonException($"Payload {envelope.EventType} rỗng hoặc sai định dạng.");
}
