using ParkingManagement.SharedKernel.Enums;

namespace SupportService.Application.DTOs;

/// <summary>Body của POST /api/v1/complaints – tài xế gửi khiếu nại cho 1 booking (US-088).</summary>
public sealed record CreateComplaintRequestDto(
    int UserId,
    string BookingCode,
    ComplaintCategory Category,
    string Description,
    decimal? RequestedAmount = null,
    IReadOnlyList<string>? EvidenceUrls = null);

/// <summary>Một tin nhắn trong hội thoại của ticket (không gồm ghi chú nội bộ của Admin).</summary>
public sealed record ComplaintMessageDto(int Id, string SenderParty, string Message, IReadOnlyList<string> AttachmentUrls, DateTime CreatedAtUtc);

/// <summary>Chi tiết ticket để tài xế theo dõi trạng thái (US-088 AC1, AC4).</summary>
public sealed record ComplaintDetailDto(
    int Id,
    string Code,
    string Status,
    string Category,
    string Priority,
    string AssignedTeam,
    string? BookingCode,
    int ParkingLotId,
    string Description,
    IReadOnlyList<string> EvidenceUrls,
    decimal? RequestedAmount,
    DateTime? OwnerResponseDueAtUtc,
    string? OwnerResponse,
    string? Resolution,
    decimal? ResolvedRefundAmount,
    DateTime CreatedAtUtc,
    DateTime? UpdatedAtUtc,
    IReadOnlyList<ComplaintMessageDto> Messages);

/// <summary>Một dòng trong danh sách "Khiếu nại của tôi".</summary>
public sealed record ComplaintSummaryDto(int Id, string Code, string Status, string Category, string Priority,
    string? BookingCode, DateTime CreatedAtUtc, DateTime? UpdatedAtUtc);
