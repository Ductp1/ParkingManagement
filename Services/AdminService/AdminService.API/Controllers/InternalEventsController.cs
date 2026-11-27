using System.Text;
using AdminService.API.Security;
using AdminService.Application.Features.AuditLogs;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ParkingManagement.SharedKernel.Exceptions;

namespace AdminService.API.Controllers;

/// <summary>
/// T-802: endpoint NỘI BỘ nhận sự kiện tích hợp do OutboxDispatcher của service khác gửi (hiện chỉ xử lý AuditRecorded).
/// Không nằm dưới /api/v1/admin nên Gateway không định tuyến tới: chỉ gọi thẳng AdminService (:5108) trong mạng nội bộ.
/// Không dùng JWT người dùng: xác thực bằng khóa nội bộ của từng service (InternalApiKeyAuthenticator).
/// </summary>
[ApiController]
[Route("internal/v1/events")]
[AllowAnonymous]
[ApiExplorerSettings(IgnoreApi = true)]
public sealed class InternalEventsController(InternalApiKeyAuthenticator authenticator, IRecordAuditEventUseCase recordAuditEvent)
    : ControllerBase
{
    /// <summary>
    /// POST /internal/v1/events – body { eventId, eventType, occurredAtUtc, payload }, header Idempotency-Key + Authorization: Bearer.
    /// 200 Recorded / Duplicate (phía gửi đánh dấu đã xử lý), 202 Ignored (sự kiện loại khác).
    /// </summary>
    [HttpPost]
    [RequestSizeLimit(RecordAuditEventUseCase.MaxBodyBytes)]
    [ProducesResponseType<RecordAuditEventResult>(StatusCodes.Status200OK)]
    [ProducesResponseType<RecordAuditEventResult>(StatusCodes.Status202Accepted)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status503ServiceUnavailable)]
    public async Task<IActionResult> Receive(CancellationToken cancellationToken)
    {
        // Xác thực trước khi đọc body: request không có khóa không được tốn bộ nhớ của AdminService.
        var callerService = authenticator.Authenticate(Request.Headers.Authorization.ToString());

        string body;
        try
        {
            using var reader = new StreamReader(Request.Body, Encoding.UTF8);
            body = await reader.ReadToEndAsync(cancellationToken);
        }
        catch (BadHttpRequestException ex) when (ex.StatusCode == StatusCodes.Status413PayloadTooLarge)
        {
            throw new ValidationException($"Body tối đa {RecordAuditEventUseCase.MaxBodyBytes / 1024} KB.");
        }

        var result = await recordAuditEvent.ExecuteAsync(
            new RecordAuditEventCommand(callerService, Request.Headers["Idempotency-Key"].ToString(), body), cancellationToken);
        return result.Status == AuditEventReceiptStatus.Ignored ? Accepted(result) : Ok(result);
    }
}
