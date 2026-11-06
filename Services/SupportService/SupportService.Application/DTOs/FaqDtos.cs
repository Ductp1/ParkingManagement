namespace SupportService.Application.DTOs;

/// <summary>Một câu hỏi thường gặp hiển thị cho người dùng.</summary>
public sealed record FaqItemDto(int Id, string Category, string Audience, string Question, string Answer, int SortOrder, int ViewCount);

/// <summary>Nhóm FAQ theo chủ đề (Booking, Payment, Refund, CheckIn...).</summary>
public sealed record FaqCategoryDto(string Category, IReadOnlyList<FaqItemDto> Items);

/// <summary>Kênh liên hệ hỗ trợ (Ticket, Email, Hotline...), cấu hình trong appsettings → "HelpCenter:Contacts".</summary>
public sealed record ContactChannelDto(string Channel, string Value, string? Note = null);

/// <summary>Response của GET /api/v1/faqs – Trung tâm trợ giúp (US-092).</summary>
public sealed record HelpCenterDto(string Audience, IReadOnlyList<FaqCategoryDto> Categories, IReadOnlyList<ContactChannelDto> Contacts);
