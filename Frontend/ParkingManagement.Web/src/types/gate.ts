export type GateMethod = 'Qr' | 'BookingCode' | 'Ocr' | 'Manual';

export type ParkingSessionStatus = 'Active' | 'Completed' | 'Abnormal';

export interface ParkingSessionDto {
  id: number;
  code: string;
  parkingLotId: number;
  plateNumber: string;
  plateDisplay: string;
  isWalkIn: boolean;
  bookingCode?: string | null;
  slotCode?: string | null;
  status: string;
  entryAtUtc: string;
  exitAtUtc?: string | null;
  minutesParked: number;
  checkInMethod: string;
  entryOcrConfidence?: number | null;
  fee?: number | null;
}

export interface CheckInCommand {
  parkingLotId: number;
  method: GateMethod;
  plate: string;
  bookingCode?: string;
  staffUserId?: number;
  qrToken?: string;
}

export interface CheckOutCommand {
  parkingLotId: number;
  plate: string;
  method?: GateMethod;
  staffUserId?: number;
}
