namespace ParkingManagement.SharedKernel.Enums;

/// <summary>Loại xe – quyết định slot được phép đỗ và đơn giá (Đặc tả v3 §2.4–2.5).</summary>
public enum VehicleType { Sedan = 1, Suv = 2, Pickup = 3, Van = 4, Oversized = 5, Motorbike = 6 }

public enum FuelType { Gasoline = 1, Diesel = 2, Electric = 3, PlugInHybrid = 4, Hybrid = 5 }
