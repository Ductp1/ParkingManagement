using ParkingManagement.SharedKernel.Domain;

namespace SupportService.Domain.Entities;

/// <summary>[SupportService] Câu hỏi thường gặp trong Trung tâm trợ giúp (US-092); cũng là nguồn trả lời cho AI Chatbot ở Phase 2 (US-107).</summary>
public class FaqArticle : BaseEntity
{
    /// <summary>Booking, Payment, Refund, Account, ParkingLotOwner...</summary>
    public string Category { get; set; } = string.Empty;
    public string Question { get; set; } = string.Empty;
    public string Answer { get; set; } = string.Empty;
    /// <summary>Driver / LotOwner / All.</summary>
    public string Audience { get; set; } = "All";
    public int SortOrder { get; set; }
    public bool IsPublished { get; set; } = true;
    public int ViewCount { get; set; }
}
