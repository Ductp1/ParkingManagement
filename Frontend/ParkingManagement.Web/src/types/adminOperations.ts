/**
 * DTO dùng ở tab Operations (Parking Lots, Bookings, Gate Activity) – đúng theo backend hiện có.
 */

/** ParkingService – GET /api/v1/parking-lots/{id} (ParkingLotDto, ParkingLots.Application) */
export interface AdminParkingLotDto {
  id: number;
  name: string;
  address: string;
  latitude: number;
  longitude: number;
  totalSlots: number;
  availableSlots: number;
  occupancyRate: number;
  maxHeightCm: number;
  openingHours: string;
  isOpenNow: boolean;
  status: string;
  description: string | null;
  hotlinePhone: string | null;
  coverImageUrl: string | null;
  ratingAverage: number;
  ratingCount: number;
  canBook: boolean;
  amenities: string[] | null;
  photos: string[] | null;
}

/** ParkingService – GET /api/v1/parking-lots/owner/{ownerProfileId} */
export interface OwnerParkingLotItemDto {
  id: number;
  name: string;
  address: string;
  city: string;
  district: string | null;
  totalSlots: number;
  availableSlots: number;
  maxHeightCm: number;
  status: string;
  openingHours: string;
  createdAtUtc: string;
}

export interface SlotHierarchyDto {
  id: number;
  code: string;
  slotType: string;
  maxVehicleType: string;
  gridX: number;
  gridY: number;
  widthCells: number;
  heightCells: number;
  state: string;
  isActive: boolean;
}

export interface FloorHierarchyDto {
  id: number;
  name: string;
  level: number;
  maxHeightCm: number | null;
  maxWeightKg: number | null;
  gridColumns: number;
  gridRows: number;
  isClosed: boolean;
  slots: SlotHierarchyDto[];
}

export interface ZoneHierarchyDto {
  id: number;
  code: string;
  name: string;
  isOutdoor: boolean;
  isClosed: boolean;
  sortOrder: number;
  floors: FloorHierarchyDto[];
}

/** ParkingService – GET /api/v1/parking-lots/{id}/hierarchy */
export interface ParkingLotHierarchyDto {
  id: number;
  name: string;
  address: string;
  city: string;
  district: string | null;
  status: string;
  totalSlots: number;
  availableSlots: number;
  maxHeightCm: number;
  openingHours: string;
  zones: ZoneHierarchyDto[];
}

/** BookingService – GET /api/v1/bookings?userId= (BookingSummaryDto) */
export interface AdminBookingSummaryDto {
  id: number;
  code: string;
  status: string;
  parkingLotName: string;
  slotCode: string | null;
  plateNumber: string;
  startAtUtc: string;
  endAtUtc: string;
  totalAmount: number;
}

export interface PriceSnapshotDto {
  rateCardId: number;
  baseAmount: number;
  surchargeAmount: number;
  discountAmount: number;
  finalAmount: number;
  billingGracePeriodMinutes: number;
  rateCardJson: string;
}

export interface BookingStatusLogDto {
  fromStatus: string | null;
  toStatus: string;
  changedByUserId: number | null;
  reason: string | null;
  atUtc: string;
}

/** BookingService – GET /api/v1/bookings/{code} (BookingDetailDto) */
export interface AdminBookingDetailDto {
  id: number;
  code: string;
  status: string;
  userId: number;
  vehicleId: number;
  plateNumber: string;
  vehicleType: string;
  parkingLotId: number;
  parkingLotName: string;
  zoneId: number | null;
  slotId: number | null;
  slotCode: string | null;
  startAtUtc: string;
  endAtUtc: string;
  holdExpiresAtUtc: string | null;
  totalAmount: number;
  discountAmount: number;
  paidAmount: number;
  promotionCode: string | null;
  priceSnapshot: PriceSnapshotDto | null;
  history: BookingStatusLogDto[];
  qrToken: string | null;
}

/** PaymentService – GET /api/v1/payments?bookingId= (PaymentDto) */
export interface AdminPaymentDto {
  id: number;
  code: string;
  bookingId: number | null;
  bookingCode: string | null;
  method: string;
  status: string;
  amount: number;
  refundedAmount: number;
  providerTransactionId: string | null;
  paidAtUtc: string | null;
  invoiceNumber: string | null;
}

/** GateService – GET /api/v1/parking-sessions?parkingLotId=&status= (ParkingSessionDto) */
export interface AdminParkingSessionDto {
  id: number;
  code: string;
  parkingLotId: number;
  plateNumber: string;
  plateDisplay: string;
  isWalkIn: boolean;
  bookingCode: string | null;
  slotCode: string | null;
  status: string;
  entryAtUtc: string;
  exitAtUtc: string | null;
  minutesParked: number;
  checkInMethod: string;
  entryOcrConfidence: number | null;
  fee: number | null;
}

/** GateService – GET /api/v1/gate-devices (GateDeviceDto) */
export interface AdminGateDeviceDto {
  id: number;
  parkingLotId: number;
  code: string;
  name: string;
  deviceType: string;
  position: string | null;
  status: string;
  lastSeenAtUtc: string | null;
  firmwareVersion: string | null;
  isActive: boolean;
  createdAtUtc: string;
  updatedAtUtc: string | null;
}

/** VehicleService – GET /api/v1/vehicles?userId= (VehicleDto) */
export interface AdminVehicleDto {
  id: number;
  userId: number;
  plateNumber: string;
  plateDisplay: string;
  vehicleType: string;
  fuelType: string;
  brand: string | null;
  model: string | null;
  color: string | null;
  heightCm: number;
  isDefault: boolean;
}
