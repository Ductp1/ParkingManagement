export interface ParkingLotDto {
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
}

export interface ParkingLotSearchItemDto {
  id: number;
  name: string;
  address: string;
  distanceKm: number;
  availableSlots: number;
  totalSlots: number;
  maxHeightCm: number;
  isOpenNow: boolean;
}
