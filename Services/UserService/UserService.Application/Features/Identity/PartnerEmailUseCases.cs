using System.Net.Mail;
using System.Text.Json;
using ParkingManagement.SharedKernel.Domain;
using ParkingManagement.SharedKernel.Exceptions;

namespace UserService.Application.Features.Identity;

public sealed record PartnerEmailRequest(string? Template, string? Destination, string? FullName, string? ApplicationCode, string? Note,
    IReadOnlyList<string>? Items, DateTime? DueAtUtc, string? Link);

/// <summary>
/// US-116 – AdminService nhờ UserService gửi email cho người nộp hồ sơ đối tác (yêu cầu bổ sung, xác nhận đã nhận bổ sung) bằng đúng
/// sender SMTP + outbox đang gửi email kích hoạt. Link sửa hồ sơ có token nên được mã hóa trước khi vào outbox (giống link kích hoạt).
/// </summary>
public sealed class PartnerEmailUseCases(IIdentityStore store, ISecretCipher cipher, IOtpDeliveryConfiguration delivery)
{
    public const string EmailEvent = "PartnerEmailRequested";
    public const string CipherPurpose = "partner-email";
    public const string InfoRequested = "PartnerInfoRequested";

    public async Task EnqueueAsync(PartnerEmailRequest request, CancellationToken ct)
    {
        if (request is null) throw new ValidationException("Thiếu dữ liệu email.");
        if (request.Template != InfoRequested) throw new ValidationException("Mẫu email không hợp lệ.");
        var destination = request.Destination?.Trim() ?? "";
        if (!MailAddress.TryCreate(destination, out var mail) || mail.Address != destination) throw new ValidationException("Email người nhận không hợp lệ.");
        var name = Required(request.FullName, "Họ tên", 150);
        var code = Required(request.ApplicationCode, "Mã hồ sơ", 30);
        var note = request.Note?.Trim();
        if (note is { Length: > 1000 }) throw new ValidationException("Ghi chú tối đa 1000 ký tự.");
        var items = (request.Items ?? []).Select(i => i?.Trim() ?? "").Where(i => i.Length > 0).ToList();
        if (items.Count > 20 || items.Any(i => i.Length > 200)) throw new ValidationException("Danh sách mục bổ sung không hợp lệ.");
        if (!Uri.TryCreate(request.Link, UriKind.Absolute, out var uri) || uri.Scheme is not ("http" or "https") || request.Link!.Length > 1000)
            throw new ValidationException("Link bổ sung hồ sơ không hợp lệ.");
        var link = request.Link;
        delivery.EnsureDestination(destination);
        delivery.EnsureAvailable();

        store.Add(new OutboxMessage
        {
            EventType = EmailEvent,
            PayloadJson = JsonSerializer.Serialize(new
            {
                request.Template, Destination = destination, FullName = name, ApplicationCode = code, Note = note, Items = items,
                request.DueAtUtc, EncryptedLink = cipher.Encrypt(link, CipherPurpose),
            }),
        });
        await store.SaveAsync(ct);
    }

    private static string Required(string? value, string field, int max)
    {
        var text = value?.Trim() ?? "";
        if (text.Length == 0) throw new ValidationException($"{field} không được để trống.");
        if (text.Length > max) throw new ValidationException($"{field} tối đa {max} ký tự.");
        return text;
    }
}
