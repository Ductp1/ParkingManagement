/**
 * DTO ParkingService – Vận hành bãi (US-067/068/069). Nguồn: LotOperationsController
 * (/api/v1/parking-lots/{id}/dashboard · heartbeat · emergency-stop · emergency-resume · emergency-logs).
 */

/** US-067: tổng quan vận hành real-time của bãi (ghép occupancy + booking counts + heartbeat US-068). */
export interface OperationsOverviewDto {
  parkingLotId: number;
  name: string;
  status: string;
  emergencyStopped: boolean;
  emergencyStopReason?: string | null;
  emergencyStoppedAtUtc?: string | null;
  totalSlots: number;
  occupiedSlots: number;
  availableSlots: number;
  activeBookings: number;
  upcomingBookings: number;
  /** US-068: quá hạn ngưỡng không heartbeat → dashboard đang hiển thị dữ liệu cũ. */
  isDataStale: boolean;
  lastHeartbeatAtUtc?: string | null;
  staleThresholdMinutes: number;
  /** JSON breakdown slot theo state, VD {"Available":42,"Occupied":50}. */
  slotBreakdownJson: string;
}

/** US-068: kết quả ghi nhận heartbeat (recoveredFromStale=true → bãi vừa phục hồi khỏi stale). */
export interface HeartbeatResultDto {
  receivedAtUtc: string;
  recoveredFromStale: boolean;
}

/** US-069: kết quả đóng bãi khẩn cấp (affectedBookings = số booking bị ảnh hưởng, đã fan-out US-086). */
export interface EmergencyStopResultDto {
  status: string;
  affectedBookings: number;
  auditLogId?: string | null;
  /** US-086: dùng để tra cứu GET /notifications/delivery-status (tracking trạng thái gửi đa kênh). */
  eventId?: string | null;
}

/** US-069: kết quả gỡ đóng bãi. */
export interface EmergencyResumeResultDto {
  status: string;
  auditLogId?: string | null;
  eventId?: string | null;
}

/** US-086: 1 dòng trạng thái gửi (1 notification = 1 user × 1 kênh). */
export interface DeliveryStatusItemDto {
  notificationId: number;
  userId: number;
  channel: string;
  templateKey: string;
  title: string;
  /** Sent | Pending | Failed */
  status: string;
  sentAtUtc?: string | null;
  retryCount: number;
  lastError?: string | null;
  createdAtUtc: string;
}

/** US-086: tổng hợp trạng thái gửi của 1 event khẩn cấp. */
export interface DeliveryStatusDto {
  eventId: string;
  total: number;
  sent: number;
  pending: number;
  failed: number;
  items: DeliveryStatusItemDto[];
}

/** US-069: dòng nhật ký audit Emergency Stop ("Stop" | "Lift"). */
export interface EmergencyStopLogDto {
  id: number;
  parkingLotId: number;
  action: string;
  actorUserId: number;
  actorRole: string;
  reason: string;
  affectedBookings: number;
  createdAtUtc: string;
}