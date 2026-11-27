using Microsoft.AspNetCore.Mvc;
using SupportService.Application.DTOs;
using SupportService.Application.Interfaces;

namespace SupportService.API.Controllers;

[ApiController]
[Route("api/v1/faqs")]
public sealed class FaqsController(IFaqService faqService) : ControllerBase
{
    /// <summary>GET /api/v1/faqs?audience=Driver&amp;category=Booking – Trung tâm trợ giúp: FAQ theo chủ đề + kênh liên hệ (US-092).</summary>
    [HttpGet]
    public async Task<ActionResult<HelpCenterDto>> GetHelpCenter([FromQuery] string? audience, [FromQuery] string? category, CancellationToken cancellationToken)
        => Ok(await faqService.GetHelpCenterAsync(audience, category, cancellationToken));

    /// <summary>GET /api/v1/faqs/3 – Xem chi tiết 1 câu hỏi, tăng lượt xem (US-092).</summary>
    [HttpGet("{id:int}")]
    public async Task<ActionResult<FaqItemDto>> GetById([FromRoute] int id, CancellationToken cancellationToken)
        => Ok(await faqService.GetByIdAsync(id, cancellationToken));
}
