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

    /// <summary>Gửi tài khoản đăng nhập + mật khẩu tạm cho nhân viên do chủ bãi tạo.</summary>
    public static Email StaffCredentials(string fullName, string email, string temporaryPassword, string ownerName, string lotName, string loginUrl)
    {
        var title = "Tài khoản nhân viên trực cổng";
        var body = $"""
            <p style="margin:0 0 6px;font-size:15px;line-height:1.6;color:#0f172a;">Xin chào <strong>{E(fullName)}</strong>,</p>
            <p style="margin:0 0 20px;font-size:15px;line-height:1.6;color:#475569;"><strong style="color:#0f172a;">{E(ownerName)}</strong> đã thêm bạn làm nhân viên trực cổng tại <strong style="color:#0f172a;">{E(lotName)}</strong> trên ParkMaster. Dưới đây là thông tin đăng nhập của bạn.</p>
            <table role="presentation" width="100%" cellpadding="0" cellspacing="0" style="border-collapse:separate;border:1px solid #e2e8f0;border-radius:14px;">
              <tr>
                <td style="padding:14px 18px;border-bottom:1px solid #e2e8f0;font-size:13px;color:#64748b;width:140px;">Tên đăng nhập</td>
                <td style="padding:14px 18px;border-bottom:1px solid #e2e8f0;font-size:15px;font-weight:600;color:#0f172a;word-break:break-all;">{E(email)}</td>
              </tr>
              <tr>
                <td style="padding:14px 18px;font-size:13px;color:#64748b;">Mật khẩu tạm</td>
                <td style="padding:14px 18px;"><span style="display:inline-block;padding:6px 12px;background:#f1f5f9;border-radius:8px;font-family:'Courier New',Consolas,monospace;font-size:20px;font-weight:700;letter-spacing:2px;color:#0f172a;">{E(temporaryPassword)}</span></td>
              </tr>
            </table>
            <table role="presentation" cellpadding="0" cellspacing="0" style="margin:24px 0 0;">
              <tr><td style="border-radius:10px;background:#2563eb;">
                <a href="{E(loginUrl)}" style="display:inline-block;padding:13px 26px;font-size:15px;font-weight:600;color:#ffffff;text-decoration:none;">Đăng nhập ParkMaster</a>
              </td></tr>
            </table>
            <div style="margin:24px 0 0;padding:14px 16px;background:#fffbeb;border:1px solid #fde68a;border-radius:12px;font-size:13px;line-height:1.6;color:#92400e;">
              <strong>Để bảo mật tài khoản:</strong> sau khi đăng nhập lần đầu, vào <em>Hồ sơ cá nhân → Đặt lại mật khẩu</em> để đổi sang mật khẩu của riêng bạn. Không chia sẻ mật khẩu cho người khác.
            </div>
            """;
        var text = $"{title}\n\nXin chào {fullName},\n\n{ownerName} đã thêm bạn làm nhân viên trực cổng tại {lotName} trên ParkMaster.\n\nTên đăng nhập: {email}\nMật khẩu tạm: {temporaryPassword}\n\nĐăng nhập: {loginUrl}\n\nSau khi đăng nhập lần đầu, vào Hồ sơ cá nhân → Đặt lại mật khẩu để đổi mật khẩu của riêng bạn.\n\nParkMaster";
        return new Email("Tài khoản ParkMaster của bạn đã sẵn sàng", Layout(title, body), text);
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
