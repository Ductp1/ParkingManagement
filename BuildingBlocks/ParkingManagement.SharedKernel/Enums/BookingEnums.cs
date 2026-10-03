namespace ParkingManagement.SharedKernel.Enums;

/// <summary>Vòng đời booking 7 trạng thái + các trạng thái kết thúc (Đặc tả v3 §4.1).</summary>
public enum BookingStatus
{
    Created = 0,
    PendingPayment = 1,
    /// <summary>Bãi Mức 0: chờ chủ bãi duyệt trong 10 phút.</summary>
    PendingOwnerApproval = 2,
    Confirmed = 3,
    CheckedIn = 4,
    Parking = 5,
    CheckedOut = 6,
    Completed = 7,
    Expired = 8,
    Cancelled = 9,
    CancelledNoRefund = 10,
    NoShow = 11,
    Rejected = 12
}

public enum AllocationMode { Dynamic = 1, DedicatedSlot = 2 }
