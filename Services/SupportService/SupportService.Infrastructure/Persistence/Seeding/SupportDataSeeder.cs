using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using ParkingManagement.ServiceDefaults.Persistence;
using ParkingManagement.SharedKernel.Contracts;
using ParkingManagement.SharedKernel.Enums;
using SupportService.Domain.Entities;

namespace SupportService.Infrastructure.Persistence.Seeding;

/// <summary>1 khiếu nại đang chờ chủ bãi Vincom phản hồi + 1 đánh giá 5 sao cho BK-0001.</summary>
public sealed class SupportDataSeeder(ILogger<SupportDataSeeder> logger) : IDataSeeder<SupportDbContext>
{
    public async Task SeedAsync(SupportDbContext db, CancellationToken cancellationToken)
    {
        await SeedCoreAsync(db, cancellationToken);
        await SeedExtrasAsync(db, cancellationToken);
    }

    /// <summary>Dữ liệu demo ban đầu (migration InitialCreate).</summary>
    private async Task SeedCoreAsync(SupportDbContext db, CancellationToken cancellationToken)
    {
        if (await db.Complaints.AnyAsync(cancellationToken)) return;

        db.Complaints.Add(new Complaint
        {
            Code = "CP-0001", UserId = DemoIds.Driver1User, BookingId = DemoIds.BookingCompleted, BookingCode = "BK-0001",
            ParkingLotId = DemoIds.LotVincom, OwnerProfileId = DemoIds.OwnerVincom, Category = ComplaintCategory.Overcharge,
            Description = "Tôi ra trước giờ kết thúc 10 phút nhưng vẫn bị tính đủ block cuối.",
            RequestedAmount = 10000, Status = ComplaintStatus.AwaitingOwner, OwnerResponseDueAtUtc = DateTime.UtcNow.AddHours(48)
        });
        db.Reviews.Add(new Review
        {
            BookingId = DemoIds.BookingCompleted, UserId = DemoIds.Driver1User, ReviewerName = "Nguyễn Văn An",
            ParkingLotId = DemoIds.LotVincom, OwnerProfileId = DemoIds.OwnerVincom, Rating = 5,
            Comment = "Bãi sạch, quét QR vào nhanh, nhân viên hướng dẫn tận tình."
        });
        await db.SaveChangesAsync(cancellationToken);
        logger.LogInformation("Seed SupportService xong: 1 khiếu nại, 1 đánh giá.");
    }

    /// <summary>Dữ liệu demo cho các bảng thêm ở migration AddDocumentCoverage – mỗi bảng kiểm tra riêng nên chạy được trên DB cũ.</summary>
    private static async Task SeedExtrasAsync(SupportDbContext db, CancellationToken cancellationToken)
    {
        if (!await db.ComplaintMessages.AnyAsync(cancellationToken))
        {
            var complaint = await db.Complaints.FirstOrDefaultAsync(c => c.Code == "CP-0001", cancellationToken);
            if (complaint is not null)
            {
                db.ComplaintMessages.AddRange(
                    new ComplaintMessage { ComplaintId = complaint.Id, SenderParty = ComplaintParty.Driver, SenderUserId = DemoIds.Driver1User,
                                           Message = "Tôi ra lúc 10:50 nhưng hóa đơn vẫn tính đến 11:00. Gửi kèm ảnh màn hình app.",
                                           AttachmentUrlsJson = "[\"/uploads/complaints/CP-0001/screenshot.jpg\"]" },
                    new ComplaintMessage { ComplaintId = complaint.Id, SenderParty = ComplaintParty.System,
                                           Message = "Đã chuyển khiếu nại cho chủ bãi. Hạn phản hồi: 48 giờ." },
                    new ComplaintMessage { ComplaintId = complaint.Id, SenderParty = ComplaintParty.Admin, SenderUserId = DemoIds.AdminUser, IsInternal = true,
                                           Message = "Log camera cổng ra ghi nhận 10:50 – khớp lời khách." });
            }
        }

        if (!await db.FaqArticles.AnyAsync(cancellationToken))
        {
            (string Cat, string Aud, string Q, string A)[] faqs =
            [
                ("Booking", "Driver", "Tôi được giữ chỗ trong bao lâu?", "Sau khi bấm Đặt chỗ, chỗ được giữ 15 phút để bạn thanh toán. Quá 15 phút booking tự hết hạn."),
                ("Booking", "Driver", "Tôi đến trễ thì sao?", "Bạn có 15 phút ân hạn. Quá 30 phút chưa check-in, bãi có quyền hủy booking do không đến (no-show)."),
                ("Refund", "Driver", "Hủy booking có được hoàn tiền không?", "Hủy trước giờ bắt đầu từ 60 phút trở lên được hoàn 100%. Hủy trong vòng 60 phút không được hoàn tiền."),
                ("Payment", "Driver", "Đỗ dưới 15 phút có mất phí không?", "Không. Mỗi lượt đỗ có 15 phút miễn phí; vượt quá mới bắt đầu tính tiền."),
                ("Payment", "Driver", "Bãi hết chỗ dù tôi đã đặt thì sao?", "Báo sự cố trong app. Nền tảng gợi ý bãi thay thế trong 1 km hoặc hoàn 100% kèm voucher đền bù."),
                ("ParkingLotOwner", "LotOwner", "Bao lâu tôi nhận được tiền?", "Doanh thu được quyết toán hằng tuần, sau khi trừ hoa hồng 10% và các khoản hoàn tiền."),
            ];
            var order = 0;
            foreach (var (cat, aud, q, a) in faqs)
                db.FaqArticles.Add(new FaqArticle { Category = cat, Audience = aud, Question = q, Answer = a, SortOrder = ++order });
        }

        await db.SaveChangesAsync(cancellationToken);
    }
}
