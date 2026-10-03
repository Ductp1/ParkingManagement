namespace ParkingManagement.SharedKernel.Enums;

/// <summary>3 cấp độ tích hợp kỹ thuật của bãi (Kiến trúc v3 §2.1).</summary>
public enum IntegrationTier { Manual = 0, PmsApi = 1, EdgeIot = 2 }

/// <summary>Loại ô đỗ trên sơ đồ.</summary>
public enum SlotType { Standard = 1, Oversized = 2, EvCharging = 3, Disabled = 4, Motorbike = 5, Vip = 6 }

/// <summary>Slot State Machine: Available → Reserved → Occupied → Available (SRS v2 F5.3).</summary>
public enum SlotState { Available = 0, Reserved = 1, Occupied = 2, Maintenance = 3 }

public enum KybStatus { Draft = 0, Submitted = 1, UnderReview = 2, Approved = 3, Rejected = 4, NeedsMoreInfo = 5 }
