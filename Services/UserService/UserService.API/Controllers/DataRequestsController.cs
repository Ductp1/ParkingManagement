using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using ParkingManagement.SharedKernel.Enums;
using UserService.Application.Features.Privacy;

namespace UserService.API.Controllers;

/// <summary>US-007: quyền được xem — toàn bộ dữ liệu UserService lưu về chính mình (số giấy tờ đã che).</summary>
[ApiController]
[Authorize]
[Route("api/v1/users/me/personal-data")]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public sealed class PersonalDataController(DataRequestUseCases requests) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<PersonalDataView>> Mine(CancellationToken ct) => Ok(await requests.ViewMineAsync(ct));
}

/// <summary>US-007: người dùng gửi yêu cầu xuất / sửa / xóa dữ liệu của chính mình.</summary>
[ApiController]
[Authorize]
[Route("api/v1/users/me/data-requests")]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public sealed class DataRequestsController(DataRequestUseCases requests) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<DataRequestItem>>> List([FromQuery] int page = 1, CancellationToken ct = default)
        => Ok(await requests.ListMineAsync(page, ct));

    [HttpPost]
    [EnableRateLimiting("auth")]
    public async Task<ActionResult<DataRequestItem>> Submit(SubmitDataRequest input, CancellationToken ct)
        => Accepted(await requests.SubmitAsync(new CreateDataRequest(input.Type, input.Password, input.Reason), ct));

    [HttpPost("{id:int}/download")]
    [EnableRateLimiting("auth")]
    public async Task<IActionResult> Download(int id, DownloadDataRequest input, CancellationToken ct)
        => File(await requests.ExportAsync(id, new ConfirmDataRequest(input.Password), ct), "application/json", "user-profile.json");
}

/// <summary>US-007: Admin xử lý yêu cầu dữ liệu (duyệt xóa → ẩn danh hóa, hoặc từ chối có lý do).</summary>
[ApiController]
[Authorize(Policy = "RequireAdmin")]
[Route("api/v1/users/data-requests")]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public sealed class AdminDataRequestsController(DataRequestUseCases requests) : ControllerBase
{
    /// <summary>GET /api/v1/users/data-requests?status=Submitted&amp;type=Deletion&amp;page=1</summary>
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<AdminDataRequestItem>>> List([FromQuery] DataRequestStatus? status,
        [FromQuery] DataRequestType? type, [FromQuery] int page = 1, CancellationToken ct = default)
        => Ok(await requests.ListAllAsync(status, type, page, ct));

    [HttpPost("{id:int}/approve")]
    public async Task<ActionResult<AdminDataRequestItem>> Approve(int id, HandleDataRequestBody? input, CancellationToken ct)
        => Ok(await requests.ApproveDeletionAsync(id, new HandleDataRequest(input?.Note), ct));

    [HttpPost("{id:int}/complete")]
    public async Task<ActionResult<AdminDataRequestItem>> Complete(int id, HandleDataRequestBody input, CancellationToken ct)
        => Ok(await requests.CompleteCorrectionAsync(id, new HandleDataRequest(input.Note), ct));

    [HttpPost("{id:int}/reject")]
    public async Task<ActionResult<AdminDataRequestItem>> Reject(int id, HandleDataRequestBody input, CancellationToken ct)
        => Ok(await requests.RejectAsync(id, new HandleDataRequest(input.Note), ct));
}

public sealed record SubmitDataRequest(DataRequestType Type, [Required, MaxLength(72)] string Password, [MaxLength(1000)] string? Reason = null);
public sealed record DownloadDataRequest([Required, MaxLength(72)] string Password);
public sealed record HandleDataRequestBody([MaxLength(1000)] string? Note);
