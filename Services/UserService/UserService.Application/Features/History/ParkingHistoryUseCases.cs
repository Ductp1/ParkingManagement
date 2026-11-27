using ParkingManagement.SharedKernel.Contracts;
using ParkingManagement.SharedKernel.Exceptions;

namespace UserService.Application.Features.History;

// ===== DTO =====
public sealed record BookingRecord(int Id, string Code, string Status, string ParkingLotName, string? SlotCode,
    string PlateNumber, DateTime StartAtUtc, DateTime EndAtUtc, decimal TotalAmount);
public sealed record PaymentRecord(string Code, string Method, string Status, decimal Amount, decimal RefundedAmount,
    DateTime? PaidAtUtc, string? InvoiceNumber);
public sealed record ParkingHistoryItem(int BookingId, string Code, string Status, string ParkingLotName, string? SlotCode,
    string PlateNumber, DateTime StartAtUtc, DateTime EndAtUtc, decimal TotalAmount, IReadOnlyList<PaymentRecord> Payments);
public sealed record ParkingHistorySummary(int TotalBookings, int CompletedBookings, decimal TotalSpent);
public sealed record ParkingHistoryPage(IReadOnlyList<ParkingHistoryItem> Items, int Page, int PageSize, int Total,
    bool PaymentsAvailable, ParkingHistorySummary Summary);

// ===== PORT =====
/// <summary>Đọc dữ liệu thuộc BookingService/PaymentService; UserService không lưu bản sao.</summary>
public interface IDriverActivitySource
{
    Task<IReadOnlyList<BookingRecord>> BookingsAsync(int userId, CancellationToken ct);
    Task<IReadOnlyList<PaymentRecord>> PaymentsAsync(int bookingId, CancellationToken ct);
}

// ===== USE CASE (US-082) =====
/// <summary>
/// Lịch sử gửi xe và giao dịch của tài xế. userId luôn lấy từ token, không nhận từ client.
/// PaymentService lỗi → vẫn trả lịch sử đặt chỗ, đánh dấu PaymentsAvailable = false.
/// </summary>
public sealed class ParkingHistoryUseCases(IDriverActivitySource source, ICurrentUser current)
{
    public const int PageSize = 10;
    private static readonly string[] Finished = ["CheckedOut", "Completed"];
    private static readonly Dictionary<string, string[]> Filters = new(StringComparer.OrdinalIgnoreCase)
    {
        ["upcoming"] = ["Created", "PendingPayment", "PendingOwnerApproval", "Confirmed"],
        ["active"] = ["CheckedIn", "Parking"],
        ["completed"] = Finished,
        ["cancelled"] = ["Expired", "Cancelled", "CancelledNoRefund", "NoShow", "Rejected"],
    };

    public async Task<ParkingHistoryPage> ListMineAsync(string? filter, int page, CancellationToken ct)
    {
        var userId = current.UserId ?? throw new AuthenticationException("Chưa đăng nhập.");
        if (page < 1 || page > int.MaxValue / PageSize) throw new ValidationException("page không hợp lệ.");
        string[]? statuses = null;
        if (!string.IsNullOrWhiteSpace(filter) && !Filters.TryGetValue(filter, out statuses))
            throw new ValidationException("Bộ lọc chỉ nhận upcoming, active, completed hoặc cancelled.");

        var all = await source.BookingsAsync(userId, ct);
        var matched = all.Where(b => statuses is null || statuses.Contains(b.Status))
            .OrderByDescending(b => b.StartAtUtc).ThenByDescending(b => b.Id).ToList();
        var slice = matched.Skip((page - 1) * PageSize).Take(PageSize).ToList();

        var available = true;
        IReadOnlyList<PaymentRecord>[] payments;
        try { payments = await Task.WhenAll(slice.Select(b => source.PaymentsAsync(b.Id, ct))); }
        catch (DependencyUnavailableException)
        {
            available = false;
            payments = slice.Select(_ => (IReadOnlyList<PaymentRecord>)[]).ToArray();
        }

        var items = slice.Select((b, i) => new ParkingHistoryItem(b.Id, b.Code, b.Status, b.ParkingLotName, b.SlotCode,
            b.PlateNumber, b.StartAtUtc, b.EndAtUtc, b.TotalAmount, payments[i])).ToList();
        // Tổng chi tiêu tính trên mọi lượt gửi đã kết thúc, không chỉ trang hiện tại.
        var summary = new ParkingHistorySummary(all.Count, all.Count(b => Finished.Contains(b.Status)),
            all.Where(b => Finished.Contains(b.Status)).Sum(b => b.TotalAmount));
        return new ParkingHistoryPage(items, page, PageSize, matched.Count, available, summary);
    }
}
