using ParkingManagement.SharedKernel.Enums;
using SupportService.Application.DTOs;
using SupportService.Domain.Entities;

namespace SupportService.Application.Interfaces;

/// <summary>US-090 AC1: lịch sử booking – Infrastructure gọi API có sẵn của BookingService (chỉ đọc).</summary>
public interface IBookingHistoryLookup
{
    /// <summary>Null nếu booking không tồn tại.</summary>
    Task<BookingEvidenceDto?> GetAsync(string bookingCode, CancellationToken cancellationToken = default);
}

/// <summary>US-090 AC1: log check-in/out của booking – Infrastructure gọi API có sẵn của GateService (chỉ đọc).</summary>
public interface IGateSessionLookup
{
    Task<IReadOnlyList<GateSessionEvidenceDto>> ListForBookingAsync(int parkingLotId, string bookingCode,
        CancellationToken cancellationToken = default);
}

/// <summary>Port đọc/ghi mức phạt đậu sai chỗ và biên bản vi phạm.</summary>
public interface ILotFineRepository
{
    Task<LotFineRate?> FindRateAsync(int parkingLotId, CancellationToken cancellationToken = default);
    /// <summary>Thêm mới hoặc cập nhật mức phạt của bãi.</summary>
    Task SaveRateAsync(LotFineRate rate, CancellationToken cancellationToken = default);
    /// <summary>Biên bản mới nhất trước, của các bãi <paramref name="parkingLotIds"/>.</summary>
    Task<IReadOnlyList<ParkingViolation>> ListViolationsAsync(IReadOnlyCollection<int> parkingLotIds, int take,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// US-090: chủ bãi phán quyết tranh chấp về bãi của mình – chuyển chỗ / ép hoàn tiền / bác khiếu nại, kèm phạt xe đậu sai chỗ.
/// Chỉ chủ bãi xử lý, không ai khác can thiệp. <c>ownerProfileId</c> / <c>ownerUserId</c> lấy từ JWT.
/// </summary>
public interface IDisputeResolutionService
{
    /// <summary>AC3: hàng chờ tranh chấp chưa đóng về bãi của chủ bãi, kèm SLA; quá hạn lên đầu.</summary>
    Task<IReadOnlyList<DisputeQueueItemDto>> ListQueueAsync(int ownerProfileId, ComplaintStatus? status, bool slaBreachedOnly,
        CancellationToken cancellationToken = default);

    /// <summary>AC1: hồ sơ tranh chấp đầy đủ.</summary>
    Task<DisputeCaseFileDto> GetCaseFileAsync(int ownerProfileId, string code, CancellationToken cancellationToken = default);

    /// <summary>AC3: chủ bãi nhắn cho tài xế (tính mốc phản hồi đầu tiên).</summary>
    Task<DisputeCaseFileDto> AddMessageAsync(int ownerProfileId, int ownerUserId, string code, DisputeMessageRequestDto request,
        CancellationToken cancellationToken = default);

    /// <summary>AC2: phán quyết, thực thi qua sự kiện, thông báo tài xế, lưu tiền lệ, ghi audit log (AC4).</summary>
    Task<DisputeCaseFileDto> RuleAsync(int ownerProfileId, int ownerUserId, string code, DisputeRulingRequestDto request,
        CancellationToken cancellationToken = default);

    /// <summary>AC2: tiền lệ phán quyết của các bãi của chủ bãi, theo loại khiếu nại.</summary>
    Task<IReadOnlyList<DisputePrecedentDto>> ListPrecedentsAsync(int ownerProfileId, ComplaintCategory? category,
        CancellationToken cancellationToken = default);
}

/// <summary>US-090: chủ bãi đặt mức phạt đậu sai chỗ cho bãi của mình và xem biên bản vi phạm.</summary>
public interface ILotFineService
{
    Task<LotFineRateDto> GetRateAsync(int ownerProfileId, int parkingLotId, CancellationToken cancellationToken = default);
    Task<LotFineRateDto> SetRateAsync(int ownerProfileId, int ownerUserId, int parkingLotId, LotFineRateRequestDto request,
        CancellationToken cancellationToken = default);
    Task<IReadOnlyList<ParkingViolationDto>> ListViolationsAsync(int ownerProfileId, int? parkingLotId,
        CancellationToken cancellationToken = default);
}
