namespace ParkingManagement.SharedKernel.Enums;

public enum ParkingSessionStatus { Active = 0, Completed = 1, Abnormal = 2 }

public enum GateMethod { Qr = 1, BookingCode = 2, Ocr = 3, Manual = 4, Pms = 5 }

public enum GateEventType
{
    VehicleArrived = 1, CheckIn = 2, CheckOut = 3, OcrFailed = 4,
    ManualPlateCorrection = 5, EmergencyBarrierOpen = 6, PaymentFailed = 7, ManualSlotOverride = 8,
    /// <summary>UC-24 / US-039: Staff hủy booking No-show sau 30 phút quá hạn check-in.</summary>
    NoShowCancel = 9
}

/// <summary>UC-51 / US-052: loại sự kiện PMS đẩy về nền tảng (xe vào/ra).</summary>
public enum PmsEventType { VehicleEntry = 1, VehicleExit = 2 }

/// <summary>UC-51 E1: sự kiện PMS không khớp booking được đưa vào hàng chờ xử lý tay cho Staff.</summary>
public enum PmsQuarantineStatus { Pending = 0, Handled = 1, Discarded = 2 }

public enum ShiftStatus { Open = 0, Closed = 1, Flagged = 2 }
