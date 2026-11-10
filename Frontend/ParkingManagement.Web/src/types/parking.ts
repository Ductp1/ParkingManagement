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
  latitude: number;
  longitude: number;
  distanceKm: number;
  estimatedTravelTimeMinutes: number;
  availableSlots: number;
  totalSlots: number;
  maxHeightCm: number;
  isOpenNow: boolean;
  coverImageUrl?: string | null;
  ratingAverage?: number;
  ratingCount?: number;
  amenities?: string[];
  pricePerHour?: number;
}

export interface LayoutCellDto {
  x: number;
  y: number;
  type: string; // 'slot' | 'road' | 'entry' | 'exit' | 'wall' | 'pillar' | 'empty'
  code?: string | null;
  slotType?: string | null;
  maxVehicleType?: string | null;
  state?: string | null; // 'Available' | 'Occupied' | 'Reserved' | 'Maintenance'
  widthCells: number;
  heightCells: number;
}

export interface FloorLayoutDto {
  floorId: number;
  floorName: string;
  level: number;
  versionNo: number;
  isPublished: boolean;
  publishedAtUtc?: string | null;
  columns: number;
  rows: number;
  cells: LayoutCellDto[];
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
  maxHeightCm?: number;
  maxWeightKg?: number;
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

export interface ParkingLotHierarchyDto {
  id: number;
  name: string;
  address: string;
  city: string;
  district?: string;
  status: string;
  totalSlots: number;
  availableSlots: number;
  maxHeightCm: number;
  openingHours: string;
  zones: ZoneHierarchyDto[];
}
