using AdminService.Application.Features.PartnerOnboarding;
using Microsoft.Extensions.Configuration;

namespace AdminService.Infrastructure.Integrations;

/// <summary>Trang bổ sung hồ sơ: cấu hình "App:PartnerEditUrl", mặc định frontend chạy local. Token nằm sau "#".</summary>
public sealed class PartnerEditLinkBuilder(IConfiguration config) : IPartnerEditLinkBuilder
{
    public string Build(string applicationCode, string token)
    {
        var page = string.IsNullOrWhiteSpace(config["App:PartnerEditUrl"]) ? "http://localhost:5173/partner/apply/edit" : config["App:PartnerEditUrl"]!;
        return $"{page}#code={Uri.EscapeDataString(applicationCode)}&token={Uri.EscapeDataString(token)}";
    }
}
