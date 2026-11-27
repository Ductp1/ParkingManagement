import { adminRequest } from './adminClientApi';
import type { FloorLayoutDto, LayoutCellInputDto, LayoutVersionSummaryDto } from '@/types/ownerLayout';

const BASE = '/api/v1/parking-lots';
const layout = (floorId: number) => `${BASE}/floors/${floorId}/layout`;

export interface SaveLayoutRequest {
  columns: number;
  rows: number;
  cells: LayoutCellInputDto[];
  changeNote?: string;
}

export interface CreateZoneInput {
  code: string;
  name: string;
  isOutdoor: boolean;
}

export interface CreateFloorInput {
  name: string;
  level: number;
  gridColumns: number;
  gridRows: number;
}

/** ParkingService – sơ đồ tầng (US-059 / US-060) cho Admin: xem, thiết kế, xuất bản, khôi phục. */
export const adminLayoutApi = {
  /** Bản đang áp dụng (chưa xuất bản lần nào thì dựng từ vị trí các ô đỗ). */
  getLayout: (floorId: number) => adminRequest<FloorLayoutDto>({ method: 'GET', url: layout(floorId) }),

  /** Bản nháp; không có nháp thì server trả bản đang áp dụng để sửa tiếp. */
  getDraft: (floorId: number) => adminRequest<FloorLayoutDto>({ method: 'GET', url: `${layout(floorId)}/draft` }),

  saveDraft: (floorId: number, data: SaveLayoutRequest) =>
    adminRequest<FloorLayoutDto>({ method: 'POST', url: `${layout(floorId)}/draft`, data }),

  discardDraft: (floorId: number) => adminRequest<void>({ method: 'DELETE', url: `${layout(floorId)}/draft` }),

  /** Đồng bộ bảng Slots theo mã ô; 409 nếu bản vẽ bỏ ô đang có xe / giữ chỗ. */
  publish: (floorId: number, data: SaveLayoutRequest) =>
    adminRequest<FloorLayoutDto>({ method: 'POST', url: `${layout(floorId)}/publish`, data }),

  versions: (floorId: number) => adminRequest<LayoutVersionSummaryDto[]>({ method: 'GET', url: `${layout(floorId)}/versions` }),

  rollback: (floorId: number, versionNo: number, changeNote: string) =>
    adminRequest<FloorLayoutDto>({ method: 'POST', url: `${layout(floorId)}/versions/${versionNo}/rollback`, data: { changeNote } }),

  createZone: (lotId: number, data: CreateZoneInput) =>
    adminRequest<{ id: number }>({ method: 'POST', url: `${BASE}/${lotId}/zones`, data }),

  createFloor: (zoneId: number, data: CreateFloorInput) =>
    adminRequest<{ id: number }>({ method: 'POST', url: `${BASE}/zones/${zoneId}/floors`, data }),
};
