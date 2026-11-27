/**
 * Data Contracts cho Phân hệ Quản lý Sơ đồ & Phiên bản sơ đồ mặt bằng (US-059 & US-060)
 * Backend: ParkingService / FloorLayoutsController
 */

export type LayoutCellType = 'slot' | 'road' | 'entry' | 'exit' | 'wall' | 'pillar' | 'empty';

export interface LayoutCellDto {
  x: number;
  y: number;
  type: LayoutCellType | string;
  code?: string | null;
  slotType?: string | null;
  maxVehicleType?: string | null;
  state?: string | null;
  widthCells: number;
  heightCells: number;
}

export interface LayoutCellInputDto {
  x: number;
  y: number;
  type: string;
  code?: string | null;
  slotType?: string | null;
  maxVehicleType?: string | null;
  widthCells: number;
  heightCells: number;
}

export interface FloorLayoutDto {
  floorId: number;
  floorName: string;
  level: number;
  versionNo: number;
  isPublished: boolean;
  publishedAtUtc: string | null;
  columns: number;
  rows: number;
  cells: LayoutCellDto[];
}

export interface LayoutVersionSummaryDto {
  id: number;
  floorId: number;
  versionNo: number;
  isPublished: boolean;
  publishedAtUtc: string | null;
  createdByUserId: number;
  changeNote: string | null;
  createdAtUtc: string;
}

export interface FloorLayoutDetailDto {
  floorId: number;
  floorName: string;
  level: number;
  versionNo: number;
  isPublished: boolean;
  publishedAtUtc: string | null;
  columns: number;
  rows: number;
  changeNote: string | null;
  createdByUserId: number;
  createdAtUtc: string;
  cells: LayoutCellDto[];
}

export interface LayoutSlotDiffDto {
  code: string;
  diffType: 'Added' | 'Removed' | 'Modified' | string;
  oldX?: number | null;
  oldY?: number | null;
  newX?: number | null;
  newY?: number | null;
  oldSlotType?: string | null;
  newSlotType?: string | null;
  oldMaxVehicleType?: string | null;
  newMaxVehicleType?: string | null;
  currentSlotState?: string | null;
  hasActiveBooking: boolean;
  isSafetyBlocker: boolean;
  blockerReason?: string | null;
}

export interface FloorLayoutDiffDto {
  floorId: number;
  fromVersionNo: number;
  toVersionNo: number | null;
  gridSizeChanged: boolean;
  oldColumns: number;
  oldRows: number;
  newColumns: number;
  newRows: number;
  addedSlots: LayoutSlotDiffDto[];
  removedSlots: LayoutSlotDiffDto[];
  modifiedSlots: LayoutSlotDiffDto[];
  canSafelyApply: boolean;
  safetyBlockers: string[];
  warnings: string[];
}

export interface FloorLayoutSafetyCheckRequest {
  columns: number;
  rows: number;
  cells: LayoutCellInputDto[];
}

export interface FloorLayoutSafetyCheckResultDto {
  canProceed: boolean;
  totalActiveSlotsInDb: number;
  totalSlotsInNewLayout: number;
  removedSlotsCount: number;
  activeBookingAtRiskSlots: string[];
  safetyBlockers: string[];
  warnings: string[];
}

export interface RollbackFloorLayoutRequest {
  changeNote?: string | null;
}
