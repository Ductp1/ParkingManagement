using SupportService.Application.DTOs;

namespace SupportService.Application.Interfaces;

/// <summary>Nghiệp vụ Trung tâm trợ giúp (US-092).</summary>
public interface IFaqService
{
    Task<HelpCenterDto> GetHelpCenterAsync(string? audience, string? category, CancellationToken cancellationToken = default);
    Task<FaqItemDto> GetByIdAsync(int id, CancellationToken cancellationToken = default);
}
