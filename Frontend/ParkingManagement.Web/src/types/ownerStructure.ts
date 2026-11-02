/**
 * Data Contracts cho Phân hệ Quản lý Cấu trúc Bãi xe: Zone -> Floor -> Slot (US-058)
 * Backend: ParkingService / ParkingLotStructureController & ParkingLotsController
 */

export interface ZoneDto {
  id: number;
  parkingLotId: number;
  code: string;
  name: string;
  isOutdoor: boolean;
  isClosed: boolean;
  closedReason: string | null;
  sortOrder: number;
}

export interface FloorDto {
  id: number;
  zoneId: number;
  name: string;
  level: number;
  maxHeightCm: number | null;
  maxWeightKg: number | null;
  gridColumns: number;
  gridRows: number;
  isClosed: boolean;
}

export interface SlotDto {
  id: number;
  floorId: number;
  code: string;
  slotType: 'Standard' | 'Compact' | 'EvCharging' | 'Handicapped' | 'Vip' | 'Oversized';
  maxVehicleType: 'Motorbike' | 'CompactCar' | 'Sedan' | 'Suv' | 'Van' | 'Truck';
  gridX: number;
  gridY: number;
  widthCells: number;
  heightCells: number;
  state: 'Available' | 'Reserved' | 'Occupied' | 'Disabled';
  isActive: boolean;
}

// Request DTOs
export interface CreateZoneRequest {
  code: string;
  name: string;
  isOutdoor?: boolean;
  sortOrder?: number;
}

export interface UpdateZoneRequest {
  code: string;
  name: string;
  isOutdoor: boolean;
  isClosed: boolean;
  closedReason?: string | null;
  sortOrder: number;
}

export interface CreateFloorRequest {
  name: string;
  level: number;
  maxHeightCm?: number | null;
  maxWeightKg?: number | null;
  gridColumns: number;
  gridRows: number;
}

export interface UpdateFloorRequest {
  name: string;
  maxHeightCm?: number | null;
  maxWeightKg?: number | null;
  gridColumns: number;
  gridRows: number;
  isClosed: boolean;
}

// Hợp đồng bàn giao số 4 (Hierarchy Tree View)
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
