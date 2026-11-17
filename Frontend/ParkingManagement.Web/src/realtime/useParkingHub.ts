import { useEffect, useRef } from 'react';
import * as SignalR from '@microsoft/signalr';

/**
 * US-065/US-066 – Hub SignalR nhận cập nhật slot & sức chứa theo bãi xe.
 * US-067/068/069/086 – thêm event "LotStatusChanged" (emergency stop/lift, stale/recovered)
 * để dashboard vận hành cập nhật tức thì thay vì chờ chu kỳ poll 30s.
 * Gọi TRỰC TIẾP :5106 vì Gateway chưa có route /hubs/* (xem useNotificationHub.ts).
 * Event "CapacityChanged" → cập nhật số chỗ trống real-time không cần F5.
 */
const PARKING_HUB_URL = import.meta.env.VITE_PARKING_HUB_URL || 'http://localhost:5106/hubs/parking';

export interface CapacityChangedEvent {
  lotId: number;
  capacity: number;
  transientBuffer: number;
  subscriberQuota: number;
  availableForTransient: number;
  availableForSubscriber: number;
  version: number;
  timestampUtc: string;
}

/** US-067/068/069/086: payload event "LotStatusChanged" (khớp LotStatusMessage của NotificationService). */
export interface LotStatusChangedEvent {
  lotId: number;
  lotName: string;
  status: string;
  emergencyStopped: boolean;
  emergencyReason?: string | null;
  isDataStale: boolean;
  timestampUtc: string;
}

export interface SlotStateChangedEvent {
  slotId: number;
  parkingLotId: number;
  newState: string;
  previousState?: string | null;
  reason?: string | null;
  changedAtUtc: string;
}

export const useParkingHub = (
  lotId: number,
  onCapacityChanged?: (event: CapacityChangedEvent) => void,
  onLotStatusChanged?: (event: LotStatusChangedEvent) => void,
  onSlotStateChanged?: (event: SlotStateChangedEvent) => void,
): void => {
  const callbackRef = useRef(onCapacityChanged);
  callbackRef.current = onCapacityChanged;
  const statusCallbackRef = useRef(onLotStatusChanged);
  statusCallbackRef.current = onLotStatusChanged;
  const slotCallbackRef = useRef(onSlotStateChanged);
  slotCallbackRef.current = onSlotStateChanged;

  useEffect(() => {
    if (!lotId || lotId <= 0) return;
    let stopped = false;

    const connection = new SignalR.HubConnectionBuilder()
      .withUrl(PARKING_HUB_URL)
      .withAutomaticReconnect([0, 3000, 5000, 10000, 15000])
      .configureLogging(SignalR.LogLevel.Warning)
      .build();

    connection.on('CapacityChanged', (msg: CapacityChangedEvent) => {
      if (msg.lotId === lotId) callbackRef.current?.(msg);
    });

    connection.on('LotStatusChanged', (msg: LotStatusChangedEvent) => {
      if (msg.lotId === lotId) statusCallbackRef.current?.(msg);
    });

    connection.on('SlotStateChanged', (msg: SlotStateChangedEvent) => {
      if (msg.parkingLotId === lotId) slotCallbackRef.current?.(msg);
    });

    const subscribe = async (): Promise<void> => {
      try {
        await connection.invoke('SubscribeToLotAsync', lotId);
      } catch {
        // lần reconnect sau sẽ subscribe lại.
      }
    };

    connection.onreconnected(async () => {
      // Reconnect xong → subscribe lại group bãi (state trên server đã mất cùng connection cũ).
      await subscribe();
    });

    const start = async (): Promise<void> => {
      try {
        await connection.start();
        await subscribe();
      } catch {
        if (!stopped) window.setTimeout(() => { if (!stopped) void start(); }, 5000);
      }
    };
    void start();

    return () => {
      stopped = true;
      void connection.stop();
    };
  }, [lotId]);
};