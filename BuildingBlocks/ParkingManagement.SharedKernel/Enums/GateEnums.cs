namespace ParkingManagement.SharedKernel.Enums;

public enum ParkingSessionStatus { Active = 0, Completed = 1, Abnormal = 2 }

public enum GateMethod { Qr = 1, BookingCode = 2, Ocr = 3, Manual = 4 }

public enum GateEventType
{
    VehicleArrived = 1, CheckIn = 2, CheckOut = 3, OcrFailed = 4,
    ManualPlateCorrection = 5, EmergencyBarrierOpen = 6, PaymentFailed = 7, ManualSlotOverride = 8
}

public enum ShiftStatus { Open = 0, Closed = 1, Flagged = 2 }
