using SupportService.Application.DTOs;

namespace SupportService.Application.Interfaces;

/// <summary>Nghiệp vụ gửi và theo dõi khiếu nại / ticket hỗ trợ (US-088).</summary>
public interface IComplaintService
{
    Task<ComplaintDetailDto> CreateAsync(CreateComplaintRequestDto request, CancellationToken cancellationToken = default);
    Task<ComplaintDetailDto> GetForUserAsync(string code, int userId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<ComplaintSummaryDto>> ListForUserAsync(int userId, CancellationToken cancellationToken = default);
}
