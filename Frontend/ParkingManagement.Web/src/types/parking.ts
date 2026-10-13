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
  description?: string | null;
  hotlinePhone?: string | null;
  coverImageUrl?: string | null;
  ratingAverage?: number;
  ratingCount?: number;
  canBook?: boolean;
  amenities?: string[];
  photos?: string[];
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
