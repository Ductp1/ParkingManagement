using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NotificationService.Application.Features.Ingestion;
using ParkingManagement.ServiceDefaults;
using ParkingManagement.SharedKernel.Contracts;
using ParkingManagement.SharedKernel.Exceptions;

namespace NotificationService.API.Controllers;

/// <summary>
/// US-086: API tổng hợp trạng thái gửi thông báo khẩn cấp cho "người gửi" (chủ bãi/Staff).
/// Phân quyền [Authorize] + role: Staff/Admin xem mọi bãi; LotOwner CHỈ xem đợt của bãi mình
/// (so khớp OwnerProfileId claim với alert.OwnerProfileId). Driver → 403.
/// </summary>
[ApiController]
[Authorize]
[Route("api/v1/notifications/emergency-alerts")]
public sealed class EmergencyAlertsController(
    IEmergencyAlertStore store,
    IEmergencyDeliveryQueries deliveryQueries,
    ICurrentUser currentUser) : ControllerBase
{
    private bool IsStaffOrAdmin => currentUser.Roles.Contains("Staff") || currentUser.Roles.Contains("Admin");

    /// <summary>GET /api/v1/notifications/emergency-alerts?lotId=3 — danh sách đợt khẩn cấp của bãi (mới nhất trước).</summary>
    [HttpGet]
    public async Task<ActionResult> List(int lotId, CancellationToken cancellationToken)
    {
        if (lotId <= 0) throw new ValidationException("lotId phải là số nguyên dương.");
        var alerts = await store.ListByLotAsync(lotId, 50, cancellationToken);

        // LotOwner chỉ thấy đợt của bãi MÌNH (scope theo OwnerProfileId claim).
        if (!IsStaffOrAdmin)
        {
            if (currentUser.OwnerProfileId is not int ownerProfileId)
                throw new ForbiddenException("Chỉ chủ bãi hoặc nhân viên mới xem được trạng thái gửi thông báo.");
            alerts = alerts.Where(a => a.OwnerProfileId == ownerProfileId).ToList();
        }

        return Ok(alerts.Select(a => new
        {
            a.BatchKey, a.ParkingLotId, a.LotName, a.Reason, a.StoppedAtUtc,
            a.TotalRecipients, a.StoppedByUserId, a.CreatedAtUtc,
        }));
    }

    /// <summary>GET /api/v1/notifications/emergency-alerts/{batchKey}/delivery-status — trạng thái gửi chi tiết + tổng hợp.</summary>
    [HttpGet("{batchKey}/delivery-status")]
    public async Task<ActionResult> DeliveryStatus(string batchKey, CancellationToken cancellationToken)
    {
        var alert = await store.GetByBatchKeyAsync(batchKey, cancellationToken)
                    ?? throw new NotFoundException("EmergencyAlert", batchKey);

        if (!IsStaffOrAdmin && alert.OwnerProfileId != currentUser.OwnerProfileId)
            throw new ForbiddenException("Bạn không có quyền xem đợt thông báo khẩn cấp của bãi này.");

        var rows = await deliveryQueries.GetRowsByBatchKeyAsync(batchKey, cancellationToken);
        var report = EmergencyDeliveryAggregator.Summarize(batchKey, rows);
        return Ok(new
        {
            alert.BatchKey, alert.ParkingLotId, alert.LotName, alert.Reason, alert.StoppedAtUtc,
            alert.TotalRecipients, report.OverallStatus, report.TotalMessages,
            channels = report.Channels,
            rows = report.Rows.Select(r => new
            {
                r.UserId, Channel = r.Channel.ToString(), Status = r.Status.ToString(),
                r.RetryCount, r.LastError, r.SentAtUtc, r.CreatedAtUtc,
            }),
        });
    }
}
