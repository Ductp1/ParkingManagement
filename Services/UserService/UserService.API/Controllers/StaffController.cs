using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using UserService.Application.Features.Staff;

namespace UserService.API.Controllers;

/// <summary>US-075: chủ bãi quản lý nhân viên trong tenant của mình. Nghiệp vụ nằm ở StaffUseCases.</summary>
[ApiController]
[Authorize(Policy = "RequireOwner")]
[Route("api/v1/users/me/staff")]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public sealed class StaffController(StaffUseCases staff) : ControllerBase
{
    /// <summary>GET /api/v1/users/me/staff?page=1 – Phân công đang hiệu lực (mỗi dòng là một nhân viên tại một bãi).</summary>
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<StaffItem>>> List([FromQuery] int page = 1, CancellationToken ct = default)
        => Ok(await staff.ListAsync(page, ct));

    /// <summary>GET /api/v1/users/me/staff/lots – Bãi của chủ bãi để chọn khi phân công.</summary>
    [HttpGet("lots")]
    public async Task<ActionResult<IReadOnlyList<OwnerLot>>> Lots(CancellationToken ct)
        => Ok(await staff.LotsAsync(ct));

    /// <summary>
    /// POST /api/v1/users/me/staff – Tạo nhân viên mới (gửi OTP xác minh) hoặc gán thêm bãi
    /// cho nhân viên đã thuộc chủ bãi này (dùng cùng Email/SĐT).
    /// </summary>
    [HttpPost]
    [EnableRateLimiting("auth")]
    public async Task<ActionResult<StaffItem>> Create(CreateStaff input, CancellationToken ct)
        => StatusCode(StatusCodes.Status201Created,
            await staff.AssignAsync(new CreateStaffRequest(input.FullName, input.Contact, input.ParkingLotId), ct));

    /// <summary>DELETE /api/v1/users/me/staff/{assignmentId} – Gỡ quyền tại một bãi và thu hồi phiên của nhân viên.</summary>
    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Revoke(int id, CancellationToken ct)
    {
        await staff.RevokeAsync(id, ct);
        return NoContent();
    }
}

public sealed record CreateStaff(
    [Required, MaxLength(150)] string FullName, [Required, MaxLength(256)] string Contact,
    [Range(1, int.MaxValue)] int ParkingLotId);
