import { apiClient } from './client';

// DTO khớp GateService ShiftContracts (US-075 / US-076).
export type ShiftStatus = 'Scheduled' | 'Open' | 'HandoverPending' | 'Completed' | 'Missed' | 'AutoClosed';
export type ClosedReason = 'Normal' | 'Forgot' | 'StaffRevoked' | 'StaffSuspended' | 'OwnerLocked';

export interface ShiftView {
  id: number;
  parkingLotId: number;
  staffUserId: number;
  scheduledStartUtc: string;
  scheduledEndUtc: string;
  startedAtUtc: string | null;
  endedAtUtc: string | null;
  status: ShiftStatus;
  businessDate: string;
  templateId: number | null;
  isCashHolder: boolean;
  isFlagged: boolean;
  closedReason: ClosedReason | null;
  cashSystem: number | null;
  cashCounted: number | null;
  cashReceived: number | null;
  discrepancyStatus: 'Open' | 'Resolved' | null;
  discrepancyNote: string | null;
  handoverToShiftId: number | null;
  handoverAcceptedAtUtc: string | null;
  handoverNote: string | null;
}

export interface ShiftTemplate {
  id: number;
  parkingLotId: number;
  name: string;
  startTime: string;
  endTime: string;
  isCashHolder: boolean;
  isActive: boolean;
}
export type SaveTemplate = Omit<ShiftTemplate, 'id' | 'parkingLotId'>;

export interface SaveShift {
  staffUserId: number;
  startUtc: string;
  endUtc: string;
  isCashHolder: boolean;
  templateId?: number | null;
}

export interface CoverageWarning {
  code: 'COVERAGE_GAP' | 'NO_CASH_HOLDER_COVERAGE';
  fromUtc: string;
  toUtc: string;
}
export interface ShiftSchedule {
  shifts: ShiftView[];
  warnings: CoverageWarning[];
}

export interface ShiftSummary {
  shift: ShiftView;
  vehiclesInLot: number;
  entries: number;
  exits: number;
  openingCash: number;
  cashSystem: number | null;
  expectedCash: number | null;
  exceptions: number;
  pmsPending: number;
  manualBarrierOpens: number;
  noShowCancels: number;
  handoverTo: ShiftView | null;
}

export interface MyShifts {
  shifts: ShiftView[];
  incomingHandovers: ShiftView[];
}

const BASE = '/api/v1/gate';
const get = async <T>(url: string, params?: object) => (await apiClient.get<T>(BASE + url, { params })).data;
const post = async <T>(url: string, body?: object) => (await apiClient.post<T>(BASE + url, body)).data;

export const shiftApi = {
  templates: (lotId: number) => get<ShiftTemplate[]>(`/lots/${lotId}/shift-templates`),
  saveTemplate: async (lotId: number, templateId: number | null, body: SaveTemplate) =>
    templateId === null
      ? post<ShiftTemplate>(`/lots/${lotId}/shift-templates`, body)
      : (await apiClient.put<ShiftTemplate>(`${BASE}/lots/${lotId}/shift-templates/${templateId}`, body)).data,
  deleteTemplate: async (lotId: number, templateId: number) => {
    await apiClient.delete(`${BASE}/lots/${lotId}/shift-templates/${templateId}`);
  },

  week: (lotId: number, weekStart: string) => get<ShiftSchedule>(`/lots/${lotId}/shifts`, { weekStart }),
  create: (lotId: number, shifts: SaveShift[]) => post<ShiftSchedule>(`/lots/${lotId}/shifts`, { shifts }),
  update: async (id: number, body: SaveShift) => (await apiClient.put<ShiftView>(`${BASE}/shifts/${id}`, body)).data,
  remove: async (id: number) => {
    await apiClient.delete(`${BASE}/shifts/${id}`);
  },
  adHoc: (lotId: number, body: { staffUserId: number; durationMinutes: number; isCashHolder: boolean }) =>
    post<ShiftView>(`/lots/${lotId}/shifts/ad-hoc`, body),
  copyWeek: (lotId: number, fromWeekStart: string, toWeekStart: string) =>
    post<{ created: number; skipped: string[] }>(`/lots/${lotId}/shifts/copy-week`, { fromWeekStart, toWeekStart }),

  handovers: (lotId: number) => get<ShiftView[]>(`/lots/${lotId}/handovers`),
  resolveDiscrepancy: (id: number, note: string) => post<ShiftView>(`/shifts/${id}/discrepancy/resolve`, { note }),

  mine: () => get<MyShifts>('/me/shifts'),
  start: (id: number) => post<ShiftView>(`/shifts/${id}/start`),
  summary: (id: number) => get<ShiftSummary>(`/shifts/${id}/summary`),
  end: (id: number, cashCounted: number | null, note: string) =>
    post<ShiftView>(`/shifts/${id}/end`, { cashCounted, note: note || null }),
  acceptHandover: (id: number, cashReceived: number) => post<ShiftView>(`/shifts/${id}/handover/accept`, { cashReceived }),
};
