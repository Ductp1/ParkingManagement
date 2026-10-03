namespace ParkingManagement.SharedKernel.Contracts;

/// <summary>
/// ID của dữ liệu demo, dùng chung cho seeder của 9 service.
/// Vì mỗi service có database riêng (không có khóa ngoại chéo), các seeder phải thống nhất ID với nhau.
/// Trên database mới tạo, IDENTITY bắt đầu từ 1 nên seeder thêm dữ liệu đúng thứ tự sẽ ra đúng các ID này.
/// </summary>
public static class DemoIds
{
    public const string Password = "Demo@123";

    // UserService – Users (thứ tự insert)
    public const int AdminUser = 1;
    public const int OwnerVincomUser = 2;
    public const int OwnerTsnUser = 3;
    public const int StaffVincomUser = 4;
    public const int Driver1User = 5;
    public const int Driver2User = 6;

    // UserService – OwnerProfiles (tenant)
    public const int OwnerVincom = 1;
    public const int OwnerTsn = 2;

    // VehicleService – Vehicles
    public const int Driver1Car = 1;      // 51F-123.45 Sedan
    public const int Driver1Suv = 2;      // 30A-678.90 SUV
    public const int Driver2Ev = 3;       // 51H-919.91 xe điện

    // ParkingService – ParkingLots
    public const int LotVincom = 1;
    public const int LotTsn = 2;
    public const int LotLandmark = 3;
    public const int LotBenThanh = 4;

    // ParkingService – Slots (Vincom có 80 slot đầu tiên, TSN bắt đầu từ 81)
    public const int SlotVincomB1A01 = 1;
    public const int SlotVincomB1A02 = 2;
    public const int SlotTsnMdA01 = 81;
    public const int SlotTsnMdA03 = 83;
    public const int ZoneVincomB = 1;
    public const int ZoneTsnP2 = 2;

    // PaymentService – RateCards, Promotions
    public const int RateCardVincom = 1;
    public const int RateCardTsn = 2;
    public const int PromotionWelcome10 = 1;

    // BookingService – Bookings
    public const int BookingCompleted = 1;  // BK-0001
    public const int BookingConfirmed = 2;  // BK-0002
    public const int BookingPending = 3;    // BK-0003

    // GateService – ParkingSessions
    public const int SessionCompleted = 1;  // PS-0001
    public const int SessionWalkIn = 2;     // PS-0002

    // PaymentService – Payments
    public const int PaymentCompleted = 1;
    public const int PaymentConfirmed = 2;

    // SupportService
    public const int ComplaintOvercharge = 1;
}
