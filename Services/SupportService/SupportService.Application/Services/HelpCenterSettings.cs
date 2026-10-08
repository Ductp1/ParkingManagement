using SupportService.Application.DTOs;

namespace SupportService.Application.Services;

/// <summary>Cấu hình kênh liên hệ của Trung tâm trợ giúp, nạp từ appsettings → "HelpCenter:Contacts".</summary>
public sealed class HelpCenterSettings
{
    public List<ContactChannelDto> Contacts { get; init; } = [];
}
