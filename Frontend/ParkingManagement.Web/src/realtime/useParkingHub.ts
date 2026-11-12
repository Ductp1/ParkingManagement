import { useEffect, useRef } from 'react';
import * as SignalR from '@microsoft/signalr';

/**
 * US-065/US-066 – Hub SignalR nhận cập nhật slot & sức chứa theo bãi xe.
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

export const useParkingHub = (lotId: number, onCapacityChanged?: (event: CapacityChangedEvent) => void): void => {
  const callbackRef = useRef(onCapacityChanged);
  callbackRef.current = onCapacityChanged;

  useEffect(() => {
    let stopped = false;

    const connection = new SignalR.HubConnectionBuilder()
      .withUrl(PARKING_HUB_URL)
      .withAutomaticReconnect([0, 3000, 5000, 10000, 15000])
      .configureLogging(SignalR.LogLevel.Warning)
      .build();

    connection.on('CapacityChanged', (msg: CapacityChangedEvent) => {
      if (msg.lotId === lotId) callbackRef.current?.(msg);
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
        if (!stopped) window.setTimeout(() => { void start(); }, 5000);
      }
    };
    void start();

    return () => {
      stopped = true;
      void connection.stop();
    };
  }, [lotId]);
};