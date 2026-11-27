using System.Net;

namespace UserService.Infrastructure.Integrations;

/// <summary>
/// Mẫu email của UserService: HTML (bảng + style inline để hiển thị đúng trên Gmail/Outlook) kèm bản chữ thuần.
/// Giờ hiển thị theo giờ Việt Nam (UTC+7). Mọi giá trị từ người dùng đều được encode HTML.
/// </summary>
public static class EmailTemplates
{
    public sealed record Email(string Subject, string Html, string Text);

    private static readonly TimeSpan VietnamOffset = TimeSpan.FromHours(7);
    private static string Vn(DateTime utc) => (DateTime.SpecifyKind(utc, DateTimeKind.Utc) + VietnamOffset).ToString("HH:mm 'ngày' dd/MM/yyyy");
    private static string E(string? value) => WebUtility.HtmlEncode(value ?? "");

    /// <summary>Mã OTP 6 số; tiêu đề và lời dẫn theo mục đích (đăng ký, đặt lại mật khẩu, đổi liên hệ, đăng nhập).</summary>
    public static Email Otp(string code, DateTime expiresAtUtc, string? purpose)
    {
        var (subject, title, intro) = purpose switch
        {
            "ResetPassword" => ("Mã đặt lại mật khẩu ParkMaster", "Đặt lại mật khẩu", "Dùng mã dưới đây để đặt mật khẩu mới cho tài khoản ParkMaster của bạn."),
            "ChangeContact" => ("Mã xác minh thông tin liên hệ ParkMaster", "Xác minh thông tin liên hệ", "Dùng mã dưới đây để xác nhận email hoặc số điện thoại mới."),
            "Login" => ("Mã đăng nhập ParkMaster", "Mã đăng nhập", "Dùng mã dưới đây để hoàn tất đăng nhập vào ParkMaster."),
            _ => ("Mã kích hoạt tài khoản ParkMaster", "Kích hoạt tài khoản", "Cảm ơn bạn đã đăng ký ParkMaster. Nhập mã dưới đây để kích hoạt tài khoản."),
        };
        var spaced = string.Join(" ", code.ToCharArray());
        var expires = Vn(expiresAtUtc);

        var body = $"""
            <p style="margin:0 0 20px;font-size:15px;line-height:1.6;color:#475569;">{E(intro)}</p>
            <table role="presentation" width="100%" cellpadding="0" cellspacing="0" style="border-collapse:separate;">
              <tr><td align="center" style="padding:22px 12px;background:#eff6ff;border:1px solid #bfdbfe;border-radius:14px;">
                <div style="font-size:13px;color:#475569;margin-bottom:8px;">Mã xác minh của bạn</div>
                <div style="font-family:'Courier New',Consolas,monospace;font-size:34px;font-weight:700;letter-spacing:6px;color:#0f172a;">{E(spaced)}</div>
                <div style="font-size:13px;color:#64748b;margin-top:10px;">Hết hạn lúc <strong style="color:#0f172a;">{E(expires)}</strong> (5 phút)</div>
              </td></tr>
            </table>
            <p style="margin:20px 0 0;font-size:13px;line-height:1.6;color:#64748b;">Không chia sẻ mã này với bất kỳ ai, kể cả nhân viên ParkMaster. Nếu bạn không yêu cầu mã, hãy bỏ qua email này.</p>
            """;
        var text = $"{title}\n\n{intro}\n\nMã xác minh: {code}\nHết hạn lúc {expires} (5 phút).\n\nKhông chia sẻ mã này với bất kỳ ai. Nếu bạn không yêu cầu mã, hãy bỏ qua email này.\n\nParkMaster";
        return new Email(subject, Layout(title, body), text);
    }

    /// <summary>US-075: chủ bãi mời nhân viên trực cổng – bấm link để tự đặt mật khẩu và kích hoạt tài khoản.</summary>
    public static Email StaffActivation(string fullName, string ownerName, string lotName, string activationLink, DateTime expiresAtUtc)
    {
        var title = "Kích hoạt tài khoản nhân viên trực cổng";
        var expires = Vn(expiresAtUtc);
        var body = $"""
            <p style="margin:0 0 6px;font-size:15px;line-height:1.6;color:#0f172a;">Xin chào <strong>{E(fullName)}</strong>,</p>
            <p style="margin:0 0 20px;font-size:15px;line-height:1.6;color:#475569;"><strong style="color:#0f172a;">{E(ownerName)}</strong> mời bạn làm nhân viên trực cổng tại <strong style="color:#0f172a;">{E(lotName)}</strong> trên ParkMaster. Bấm nút dưới đây để đặt mật khẩu và kích hoạt tài khoản.</p>
            <table role="presentation" cellpadding="0" cellspacing="0" style="margin:0 0 20px;">
              <tr><td style="border-radius:10px;background:#2563eb;">
                <a href="{E(activationLink)}" style="display:inline-block;padding:13px 26px;font-size:15px;font-weight:600;color:#ffffff;text-decoration:none;">Kích hoạt tài khoản</a>
              </td></tr>
            </table>
            <p style="margin:0 0 12px;font-size:13px;line-height:1.6;color:#64748b;">Link dùng được một lần, hết hạn lúc <strong style="color:#0f172a;">{E(expires)}</strong>. Quá hạn, nhờ chủ bãi gửi lại lời mời.</p>
            <p style="margin:0;font-size:13px;line-height:1.6;color:#64748b;">Nếu bạn không nhận lời mời này, hãy bỏ qua email.</p>
            """;
        var text = $"{title}\n\nXin chào {fullName},\n\n{ownerName} mời bạn làm nhân viên trực cổng tại {lotName} trên ParkMaster. Mở link dưới đây để đặt mật khẩu và kích hoạt tài khoản:\n{activationLink}\n\nLink dùng được một lần, hết hạn lúc {expires}.\n\nParkMaster";
        return new Email("Lời mời làm nhân viên trực cổng ParkMaster", Layout(title, body), text);
    }

    /// <summary>US-116: hồ sơ đối tác đã được duyệt – chủ bãi bấm link để tự đặt mật khẩu và kích hoạt tài khoản.</summary>
    public static Email OwnerActivation(string fullName, string businessName, string activationLink, DateTime expiresAtUtc)
    {
        var title = "Kích hoạt tài khoản chủ bãi";
        var expires = Vn(expiresAtUtc);
        var body = $"""
            <p style="margin:0 0 6px;font-size:15px;line-height:1.6;color:#0f172a;">Xin chào <strong>{E(fullName)}</strong>,</p>
            <p style="margin:0 0 20px;font-size:15px;line-height:1.6;color:#475569;">Hồ sơ đối tác của <strong style="color:#0f172a;">{E(businessName)}</strong> đã được ParkMaster duyệt. Bấm nút dưới đây để đặt mật khẩu và kích hoạt tài khoản chủ bãi.</p>
            <table role="presentation" cellpadding="0" cellspacing="0" style="margin:0 0 20px;">
              <tr><td style="border-radius:10px;background:#2563eb;">
                <a href="{E(activationLink)}" style="display:inline-block;padding:13px 26px;font-size:15px;font-weight:600;color:#ffffff;text-decoration:none;">Kích hoạt tài khoản</a>
              </td></tr>
            </table>
            <p style="margin:0 0 12px;font-size:13px;line-height:1.6;color:#64748b;">Link dùng được một lần, hết hạn lúc <strong style="color:#0f172a;">{E(expires)}</strong>. Sau khi kích hoạt, đăng nhập để khai báo từng bãi và nộp hồ sơ KYB.</p>
            <p style="margin:0;font-size:13px;line-height:1.6;color:#64748b;">Nếu bạn không đăng ký làm đối tác ParkMaster, hãy bỏ qua email này.</p>
            """;
        var text = $"{title}\n\nXin chào {fullName},\n\nHồ sơ đối tác của {businessName} đã được ParkMaster duyệt. Mở link dưới đây để đặt mật khẩu và kích hoạt tài khoản chủ bãi:\n{activationLink}\n\nLink dùng được một lần, hết hạn lúc {expires}.\n\nParkMaster";
        return new Email("Kích hoạt tài khoản chủ bãi ParkMaster", Layout(title, body), text);
    }

    /// <summary>
    /// US-116: Admin yêu cầu người nộp bổ sung hồ sơ. Chỉ có mã hồ sơ, ghi chú của Admin, các mục cần bổ sung, hạn và link sửa –
    /// không có mã số thuế, giấy tờ hay dữ liệu nhạy cảm.
    /// </summary>
    public static Email PartnerInfoRequested(string fullName, string applicationCode, string? note, IReadOnlyList<string> items,
        DateTime? dueAtUtc, string editLink)
    {
        var title = $"Hồ sơ {applicationCode} cần bổ sung thông tin";
        var due = dueAtUtc is { } d ? Vn(d) : null;
        var itemsHtml = items.Count == 0 ? "" : $"""
            <p style="margin:0 0 6px;font-size:14px;font-weight:600;color:#0f172a;">Các mục cần bổ sung</p>
            <ul style="margin:0 0 18px;padding-left:20px;font-size:14px;line-height:1.7;color:#334155;">{string.Concat(items.Select(i => $"<li>{E(i)}</li>"))}</ul>
            """;
        var noteHtml = string.IsNullOrWhiteSpace(note) ? "" : $"""
            <p style="margin:0 0 6px;font-size:14px;font-weight:600;color:#0f172a;">Ghi chú của ParkMaster</p>
            <p style="margin:0 0 18px;padding:12px 14px;border-left:3px solid #d97706;background:#fff7ed;font-size:14px;line-height:1.6;color:#334155;white-space:pre-line;">{E(note)}</p>
            """;
        var dueHtml = due is null ? "Link" : "Vui lòng bổ sung trước <strong style=\"color:#0f172a;\">" + E(due) + "</strong>. Link";
        var body = $"""
            <p style="margin:0 0 6px;font-size:15px;line-height:1.6;color:#0f172a;">Xin chào <strong>{E(fullName)}</strong>,</p>
            <p style="margin:0 0 18px;font-size:15px;line-height:1.6;color:#475569;">ParkMaster cần Bạn bổ sung một số thông tin cho hồ sơ <strong style="color:#0f172a;">{E(applicationCode)}</strong> để tiếp tục thẩm định.</p>
            {noteHtml}{itemsHtml}
            <table role="presentation" cellpadding="0" cellspacing="0" style="margin:0 0 20px;">
              <tr><td style="border-radius:10px;background:#2563eb;">
                <a href="{E(editLink)}" style="display:inline-block;padding:13px 26px;font-size:15px;font-weight:600;color:#ffffff;text-decoration:none;">Bổ sung hồ sơ</a>
              </td></tr>
            </table>
            <p style="margin:0 0 12px;font-size:13px;line-height:1.6;color:#64748b;">{dueHtml} dùng cho hồ sơ này và hết hiệu lực sau khi Bạn gửi bổ sung.</p>
            <p style="margin:0;font-size:13px;line-height:1.6;color:#64748b;">Nếu Bạn không nộp hồ sơ đối tác ParkMaster, hãy bỏ qua email này.</p>
            """;
        var text = $"{title}\n\nXin chào {fullName},\n\nParkMaster cần Bạn bổ sung thông tin cho hồ sơ {applicationCode}.\n"
            + (string.IsNullOrWhiteSpace(note) ? "" : $"\nGhi chú của ParkMaster:\n{note}\n")
            + (items.Count == 0 ? "" : $"\nCác mục cần bổ sung:\n{string.Join("\n", items.Select(i => $"- {i}"))}\n")
            + (due is null ? "" : $"\nHạn bổ sung: {due}\n")
            + $"\nMở link để bổ sung hồ sơ:\n{editLink}\n\nNếu Bạn không nộp hồ sơ đối tác ParkMaster, hãy bỏ qua email này.";
        return new Email(title, Layout(title, body), text);
    }

    /// <summary>Khung chung: logo P xanh, tiêu đề, thẻ nội dung trắng, chân trang hỗ trợ.</summary>
    private static string Layout(string title, string body) => $"""
        <!doctype html>
        <html lang="vi"><head><meta charset="utf-8"><meta name="viewport" content="width=device-width,initial-scale=1"><title>{E(title)}</title></head>
        <body style="margin:0;padding:0;background:#eef2f7;font-family:'Segoe UI',Roboto,Helvetica,Arial,sans-serif;color:#0f172a;">
          <table role="presentation" width="100%" cellpadding="0" cellspacing="0" style="background:#eef2f7;">
            <tr><td align="center" style="padding:32px 12px;">
              <table role="presentation" width="100%" cellpadding="0" cellspacing="0" style="max-width:520px;">
                <tr><td style="padding:0 4px 18px;">
                  <table role="presentation" cellpadding="0" cellspacing="0"><tr>
                    <td style="width:36px;height:36px;background:#2563eb;border-radius:9px;text-align:center;vertical-align:middle;font-size:20px;font-weight:800;color:#ffffff;">P</td>
                    <td style="padding-left:10px;font-size:18px;font-weight:700;color:#0f172a;">ParkMaster</td>
                  </tr></table>
                </td></tr>
                <tr><td style="background:#ffffff;border-radius:18px;padding:32px 28px;box-shadow:0 1px 3px rgba(15,23,42,.08);">
                  <h1 style="margin:0 0 12px;font-size:22px;line-height:1.3;font-weight:700;color:#0f172a;">{E(title)}</h1>
                  {body}
                </td></tr>
                <tr><td style="padding:20px 8px 0;text-align:center;font-size:12px;line-height:1.6;color:#94a3b8;">
                  Email tự động từ ParkMaster, vui lòng không trả lời.<br>Hỗ trợ: 1900 6868
                </td></tr>
              </table>
            </td></tr>
          </table>
        </body></html>
        """;
}
