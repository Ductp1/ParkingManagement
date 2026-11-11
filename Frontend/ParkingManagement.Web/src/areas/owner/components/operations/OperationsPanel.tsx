import React, { useCallback, useEffect, useMemo, useRef, useState } from 'react';
import {
  Activity,
  AlarmClock,
  CalendarClock,
  Car,
  RotateCcw,
  RefreshCw,
  Siren,
  WifiOff,
} from 'lucide-react';
import { ownerOperationsApi } from '@/api/ownerOperationsApi';
import type {
  OperationsOverviewDto,
  EmergencyStopLogDto,
  DeliveryStatusDto,
} from '@/types/ownerOperations';
import { useParkingHub } from '@/realtime/useParkingHub';
import { EmergencyStopAlert } from '../capacity/EmergencyStopAlert';

interface OperationsPanelProps {
  lotId: number;
  lotName: string;
  /** Gọi sau khi Stop/Resume để OwnerLotsPage tải lại danh sách bãi (status đổi Suspended/Active). */
  onLotChanged?: () => void;
}

/**
 * US-067/068/069 – Dashboard vận hành real-time của bãi (Owner Portal):
 *  - Tổng quan occupancy + booking counts (tự làm mới 30s + push SignalR "CapacityChanged").
 *  - US-068: banner "dữ liệu cũ" khi mất heartbeat quá hạn ngưỡng + nút giả lập heartbeat.
 *  - US-069: nút Dừng khẩn cấp / Mở lại bãi (bắt buộc lý do) + lịch sử audit.
 *  - US-086 chạy ở backend: khi Stop, NotificationService fan-out đa kênh cho khách bị ảnh hưởng.
 */
export const OperationsPanel: React.FC<OperationsPanelProps> = ({ lotId, lotName, onLotChanged }) => {
  const [overview, setOverview] = useState<OperationsOverviewDto | null>(null);
  const [logs, setLogs] = useState<EmergencyStopLogDto[]>([]);
  const [loading, setLoading] = useState(true);
  const [busy, setBusy] = useState(false);
  const [message, setMessage] = useState<{ type: 'success' | 'error'; text: string } | null>(null);
  const [dialog, setDialog] = useState<{ mode: 'stop' | 'resume' } | null>(null);
  const [reason, setReason] = useState('');
  // US-086: tracking trạng thái gửi đa kênh của event khẩn cấp gần nhất.
  const [delivery, setDelivery] = useState<DeliveryStatusDto | null>(null);
  const [tracking, setTracking] = useState(false);
  const timerRef = useRef<number | null>(null);

  const loadOverview = useCallback(async (silent = false) => {
    if (!silent) setLoading(true);
    try {
      const data = await ownerOperationsApi.getDashboard(lotId);
      setOverview(data);
    } catch {
      if (!silent) setOverview(null);
    } finally {
      if (!silent) setLoading(false);
    }
  }, [lotId]);

  const loadLogs = useCallback(async () => {
    try {
      setLogs(await ownerOperationsApi.getEmergencyLogs(lotId, 10));
    } catch {
      /* lịch sử audit là tùy chọn – im lặng */
    }
  }, [lotId]);

  useEffect(() => {
    void loadOverview();
    void loadLogs();
    timerRef.current = window.setInterval(() => { void loadOverview(true); }, 30_000);
    return () => { if (timerRef.current) window.clearInterval(timerRef.current); };
  }, [loadOverview, loadLogs]);

  // US-065/066: có thay đổi capacity qua SignalR → refresh ngay dashboard (không đợi 30s).
  useParkingHub(lotId, () => { void loadOverview(true); }, () => { void loadOverview(true); });

  const reasonValid = reason.trim().length >= 5;

  const submitDialog = async () => {
    if (!reasonValid || !dialog) return;
    setBusy(true);
    setMessage(null);
    try {
      let eventId: string | null = null;
      if (dialog.mode === 'stop') {
        const result = await ownerOperationsApi.emergencyStop(lotId, reason.trim());
        eventId = result.eventId ?? null;
        setMessage({
          type: 'success',
          text: `Đã đóng bãi khẩn cấp. ${result.affectedBookings} booking bị ảnh hưởng — hệ thống đã gửi thông báo đa kênh cho khách (US-086).`,
        });
      } else {
        const result = await ownerOperationsApi.emergencyResume(lotId, reason.trim());
        eventId = result.eventId ?? null;
        setMessage({ type: 'success', text: 'Đã gỡ đóng bãi khẩn cấp. Bãi nhận đặt chỗ và check-in bình thường.' });
      }
      setDialog(null);
      setReason('');
      await Promise.all([loadOverview(true), loadLogs()]);
      onLotChanged?.();
      // US-086: theo dõi trạng thái gửi đa kênh của event vừa phát sinh.
      if (eventId) void trackDelivery(eventId);
    } catch (err) {
      setMessage({ type: 'error', text: extractError(err) });
    } finally {
      setBusy(false);
    }
  };

  const simulateHeartbeat = async () => {
    setBusy(true);
    setMessage(null);
    try {
      const result = await ownerOperationsApi.sendHeartbeat(lotId);
      setMessage({
        type: 'success',
        text: result.recoveredFromStale
          ? 'Đã ghi nhận heartbeat — bãi vừa PHỤC HỒI khỏi trạng thái dữ liệu cũ (US-086 đã báo chủ bãi).'
          : 'Đã ghi nhận heartbeat — dữ liệu bãi vẫn ở trạng thái tươi.',
      });
      await loadOverview(true);
    } catch (err) {
      setMessage({ type: 'error', text: extractError(err) });
    } finally {
      setBusy(false);
    }
  };

  /** US-086: poll tracking theo EventId — dừng khi hết pending hoặc hết ~12 giây. */
  const trackDelivery = async (eventId: string) => {
    setTracking(true);
    try {
      for (let attempt = 0; attempt < 6; attempt++) {
        try {
          const dto = await ownerOperationsApi.getDeliveryStatus(eventId);
          setDelivery(dto);
          if (dto.total > 0 && dto.pending === 0) break;
        } catch {
          // NotificationService chưa nhận event (RabbitMQ/service down) — thử lại ở vòng sau.
        }
        await new Promise((resolve) => setTimeout(resolve, 2000));
      }
    } finally {
      setTracking(false);
    }
  };

  const slotBreakdown = useMemo<{ state: string; count: number }[]>(() => {
    if (!overview) return [];
    try {
      const parsed = JSON.parse(overview.slotBreakdownJson) as Record<string, number>;
      return Object.entries(parsed).map(([state, count]) => ({ state, count }));
    } catch {
      return [];
    }
  }, [overview]);

  const fillRate = overview && overview.totalSlots > 0
    ? Math.round((overview.occupiedSlots / overview.totalSlots) * 100)
    : 0;

  return (
    <div className="mt-6 space-y-5" data-testid="operations-panel">
      {message && (
        <div
          className={`p-3.5 rounded-2xl text-xs font-semibold border ${
            message.type === 'success'
              ? 'bg-emerald-50 border-emerald-200 text-emerald-800'
              : 'bg-rose-50 border-rose-200 text-rose-800'
          }`}
        >
          {message.text}
        </div>
      )}

      {loading ? (
        <div className="py-16 text-center text-xs sm:text-sm text-slate-500 bg-white rounded-3xl border border-slate-200/90 shadow-xs">
          <RefreshCw size={24} className="animate-spin mx-auto mb-3 text-indigo-600" />
          Đang tải tổng quan vận hành…
        </div>
      ) : !overview ? (
        <div className="p-8 text-center text-slate-400 text-xs bg-white rounded-3xl border border-slate-200/90 shadow-xs">
          Không tải được tổng quan vận hành. Kiểm tra ParkingService (:5103) đang chạy.
        </div>
      ) : (
        <>
          <EmergencyStopAlert
            isEmergencyStopped={overview.emergencyStopped}
            emergencyReason={overview.emergencyStopReason}
          />
          {/* US-068: banner dữ liệu cũ khi mất heartbeat */}
          {overview.isDataStale && (
            <div className="p-4 rounded-2xl bg-amber-50 border-2 border-amber-300 text-amber-900 flex items-start gap-3">
              <WifiOff className="w-5 h-5 text-amber-600 shrink-0 mt-0.5" />
              <div className="flex-1 text-xs sm:text-sm">
                <p className="font-bold">Dữ liệu bãi đang CŨ (stale)</p>
                <p className="mt-0.5 text-amber-800">
                  Không nhận heartbeat hơn {overview.staleThresholdMinutes} phút
                  {overview.lastHeartbeatAtUtc ? ` (lần cuối: ${formatTime(overview.lastHeartbeatAtUtc)})` : ' — chưa từng nhận heartbeat'}. Số liệu dưới đây có thể không phản ánh thực tế.
                </p>
              </div>
              <button
                type="button"
                disabled={busy}
                onClick={() => { void simulateHeartbeat(); }}
                className="shrink-0 px-3 py-1.5 rounded-xl bg-amber-600 hover:bg-amber-700 disabled:opacity-50 text-white text-[11px] font-bold transition-all cursor-pointer flex items-center gap-1.5"
              >
                <AlarmClock size={13} /> Giả lập heartbeat
              </button>
            </div>
          )}

          {/* KPI cards (US-067) */}
          <div className="grid grid-cols-2 lg:grid-cols-4 gap-3.5 sm:gap-4">
            <div className="p-4 rounded-2xl bg-white border border-slate-200/90 shadow-2xs">
              <div className="flex items-center justify-between">
                <span className="text-[11px] font-bold text-slate-400 uppercase tracking-wider">Trạng thái bãi</span>
                <Activity size={16} className={overview.emergencyStopped ? 'text-rose-600' : 'text-emerald-600'} />
              </div>
              <div className={`text-lg font-extrabold mt-2 ${overview.emergencyStopped ? 'text-rose-700' : 'text-emerald-700'}`}>
                {humanizeStatus(overview.status)}
              </div>
              <p className="text-[11px] text-slate-500 mt-1">
                Dữ liệu: {overview.isDataStale ? 'CŨ ⚠' : 'Tươi · real-time'}
              </p>
            </div>

            <div className="p-4 rounded-2xl bg-white border border-slate-200/90 shadow-2xs">
              <div className="flex items-center justify-between">
                <span className="text-[11px] font-bold text-slate-400 uppercase tracking-wider">Lấp đầy</span>
                <Car size={16} className="text-indigo-600" />
              </div>
              <div className="text-lg sm:text-xl font-extrabold text-slate-900 mt-2">
                {overview.occupiedSlots}
                <span className="text-xs font-semibold text-slate-500"> / {overview.totalSlots} ô</span>
              </div>
              <div className="mt-1.5 h-1.5 rounded-full bg-slate-100 overflow-hidden">
                <div className="h-full rounded-full bg-indigo-500 transition-all" style={{ width: `${fillRate}%` }} />
              </div>
              <p className="text-[11px] text-slate-500 mt-1">{fillRate}% lấp đầy · {overview.availableSlots} chỗ trống</p>
            </div>

            <div className="p-4 rounded-2xl bg-white border border-slate-200/90 shadow-2xs">
              <div className="flex items-center justify-between">
                <span className="text-[11px] font-bold text-slate-400 uppercase tracking-wider">Booking đang đỗ</span>
                <Car size={16} className="text-emerald-600" />
              </div>
              <div className="text-lg sm:text-xl font-extrabold text-emerald-700 mt-2">{overview.activeBookings}</div>
              <p className="text-[11px] text-slate-500 mt-1">Xe đang trong bãi (BookingService)</p>
            </div>

            <div className="p-4 rounded-2xl bg-white border border-slate-200/90 shadow-2xs">
              <div className="flex items-center justify-between">
                <span className="text-[11px] font-bold text-slate-400 uppercase tracking-wider">Booking sắp đến</span>
                <CalendarClock size={16} className="text-amber-600" />
              </div>
              <div className="text-lg sm:text-xl font-extrabold text-amber-700 mt-2">{overview.upcomingBookings}</div>
              <p className="text-[11px] text-slate-500 mt-1">Chưa check-in (đã xác nhận / chờ duyệt)</p>
            </div>
          </div>
          {/* Slot breakdown + Emergency controls */}
          <div className="grid grid-cols-1 lg:grid-cols-12 gap-5">
            <div className="lg:col-span-7 rounded-3xl bg-white border border-slate-200/90 p-5 shadow-xs">
              <h3 className="text-sm font-bold text-slate-900">Phân bố ô đỗ theo trạng thái</h3>
              <p className="text-[11px] text-slate-500 mt-0.5">Tổng hợp từ bảng Slots — cập nhật real-time qua SignalR.</p>
              <div className="flex flex-wrap gap-2 mt-3">
                {slotBreakdown.length === 0 ? (
                  <span className="text-xs text-slate-400">Bãi chưa khai báo slot nào.</span>
                ) : (
                  slotBreakdown.map((s) => (
                    <span key={s.state} className="px-3 py-1.5 rounded-xl bg-slate-50 border border-slate-200 text-xs font-bold text-slate-700">
                      {humanizeStatus(s.state)}: <span className="text-indigo-600">{s.count}</span>
                    </span>
                  ))
                )}
              </div>
            </div>

            <div className="lg:col-span-5 rounded-3xl bg-white border border-slate-200/90 p-5 shadow-xs">
              <h3 className="text-sm font-bold text-slate-900">Điều khiển khẩn cấp (US-069)</h3>
              <p className="text-[11px] text-slate-500 mt-0.5">
                Đóng bãi yêu cầu lý do ≥ 5 ký tự; khách bị ảnh hưởng nhận thông báo đa kênh tự động (US-086).
              </p>
              {overview.emergencyStopped ? (
                <button
                  type="button"
                  disabled={busy}
                  onClick={() => { setDialog({ mode: 'resume' }); setReason(''); }}
                  className="mt-4 w-full py-3 rounded-xl bg-emerald-600 hover:bg-emerald-700 disabled:opacity-50 text-white text-xs font-bold shadow-md transition-all cursor-pointer flex items-center justify-center gap-2"
                >
                  <RotateCcw size={15} /> Mở lại bãi (gỡ đóng khẩn cấp)
                </button>
              ) : (
                <button
                  type="button"
                  disabled={busy}
                  onClick={() => { setDialog({ mode: 'stop' }); setReason(''); }}
                  className="mt-4 w-full py-3 rounded-xl bg-rose-600 hover:bg-rose-700 disabled:opacity-50 text-white text-xs font-bold shadow-md shadow-rose-600/20 transition-all cursor-pointer flex items-center justify-center gap-2"
                >
                  <Siren size={15} /> DỪNG KHẨN CẤP — đóng bãi ngay
                </button>
              )}
              <p className="text-[11px] text-slate-400 mt-2 flex items-center gap-1">
                <AlarmClock size={12} /> Heartbeat lần cuối:{' '}
                {overview.lastHeartbeatAtUtc ? formatTime(overview.lastHeartbeatAtUtc) : 'chưa có'}
              </p>
            </div>
          </div>

          {/* US-086: theo dõi trạng thái gửi đa kênh */}
          {delivery && (
            <div className="rounded-3xl bg-white border border-slate-200/90 p-5 shadow-xs">
              <div className="flex items-start justify-between gap-3">
                <div>
                  <h3 className="text-sm font-bold text-slate-900">Theo dõi trạng thái gửi thông báo (US-086)</h3>
                  <p className="text-[11px] text-slate-500 mt-0.5">
                    Event <span className="font-mono">{delivery.eventId.slice(0, 8)}…</span>
                    {' '}· Tổng <strong>{delivery.total}</strong>
                    {' '}· Đã gửi <strong className="text-emerald-600">{delivery.sent}</strong>
                    {' '}· Đang chờ <strong className="text-amber-600">{delivery.pending}</strong>
                    {' '}· Lỗi <strong className="text-rose-600">{delivery.failed}</strong>
                    {tracking && ' · đang cập nhật…'}
                  </p>
                </div>
                <button
                  type="button"
                  onClick={() => setDelivery(null)}
                  className="shrink-0 text-[11px] font-bold text-slate-400 hover:text-slate-600 cursor-pointer"
                >
                  Ẩn
                </button>
              </div>
              {delivery.total === 0 ? (
                <p className="text-xs text-slate-400 mt-2">
                  Chưa có bản ghi gửi nào cho event này — NotificationService có thể chưa nhận event
                  (kiểm tra RabbitMQ/NotificationService đang chạy) hoặc không có ai bị ảnh hưởng.
                </p>
              ) : (
                <div className="mt-3 overflow-x-auto">
                  <table className="w-full text-left text-xs">
                    <thead>
                      <tr className="text-slate-400 uppercase text-[10px] tracking-wider border-b border-slate-100">
                        <th className="py-2 pr-4">Người nhận (UserId)</th>
                        <th className="py-2 pr-4">Kênh</th>
                        <th className="py-2 pr-4">Trạng thái</th>
                        <th className="py-2 pr-4">Gửi lúc</th>
                        <th className="py-2">Lỗi / retry</th>
                      </tr>
                    </thead>
                    <tbody>
                      {delivery.items.map((item) => (
                        <tr key={item.notificationId} className="border-b border-slate-50">
                          <td className="py-2 pr-4 font-bold text-slate-700">#{item.userId}</td>
                          <td className="py-2 pr-4 text-slate-600">{item.channel}</td>
                          <td className="py-2 pr-4">
                            <span className={`px-2 py-0.5 rounded-full text-[10px] font-bold ${
                              item.status === 'Sent'
                                ? 'bg-emerald-50 text-emerald-700 border border-emerald-200'
                                : item.status === 'Pending'
                                  ? 'bg-amber-50 text-amber-700 border border-amber-200'
                                  : 'bg-rose-50 text-rose-700 border border-rose-200'
                            }`}>
                              {item.status === 'Sent' ? 'Đã gửi' : item.status === 'Pending' ? 'Đang chờ' : 'Lỗi'}
                            </span>
                          </td>
                          <td className="py-2 pr-4 text-slate-600">{item.sentAtUtc ? formatTime(item.sentAtUtc) : '—'}</td>
                          <td className="py-2 text-slate-500">
                            {item.lastError ? `${item.lastError} (retry ${item.retryCount})` : item.retryCount > 0 ? `retry ${item.retryCount}` : '—'}
                          </td>
                        </tr>
                      ))}
                    </tbody>
                  </table>
                </div>
              )}
            </div>
          )}

          {/* Audit log (US-069) */}
          <div className="rounded-3xl bg-white border border-slate-200/90 p-5 shadow-xs">
            <h3 className="text-sm font-bold text-slate-900">Lịch sử Dừng khẩn cấp</h3>
            {logs.length === 0 ? (
              <p className="text-xs text-slate-400 mt-2">Chưa có lần đóng/mở khẩn cấp nào được ghi nhận.</p>
            ) : (
              <div className="mt-3 overflow-x-auto">
                <table className="w-full text-left text-xs">
                  <thead>
                    <tr className="text-slate-400 uppercase text-[10px] tracking-wider border-b border-slate-100">
                      <th className="py-2 pr-4">Thời điểm</th>
                      <th className="py-2 pr-4">Hành động</th>
                      <th className="py-2 pr-4">Vai trò</th>
                      <th className="py-2 pr-4">Lý do</th>
                      <th className="py-2">Booking ảnh hưởng</th>
                    </tr>
                  </thead>
                  <tbody>
                    {logs.map((log) => (
                      <tr key={log.id} className="border-b border-slate-50">
                        <td className="py-2 pr-4 text-slate-600">{formatTime(log.createdAtUtc)}</td>
                        <td className="py-2 pr-4">
                          <span className={`px-2 py-0.5 rounded-full text-[10px] font-bold ${
                            log.action === 'Stop'
                              ? 'bg-rose-50 text-rose-700 border border-rose-200'
                              : 'bg-emerald-50 text-emerald-700 border border-emerald-200'
                          }`}>
                            {log.action === 'Stop' ? 'Đóng bãi' : 'Mở lại'}
                          </span>
                        </td>
                        <td className="py-2 pr-4 text-slate-600">{log.actorRole}</td>
                        <td className="py-2 pr-4 text-slate-700 max-w-[280px] truncate" title={log.reason}>{log.reason}</td>
                        <td className="py-2 font-bold text-slate-700">{log.affectedBookings}</td>
                      </tr>
                    ))}
                  </tbody>
                </table>
              </div>
            )}
          </div>
        </>
      )}
      {/* Modal lý do Stop/Resume */}
      {dialog && (
        <div className="fixed inset-0 z-50 flex items-center justify-center bg-slate-900/50 backdrop-blur-[2px] p-4">
          <div className="w-full max-w-md rounded-3xl bg-white p-6 shadow-2xl">
            <div className="flex items-center gap-3">
              <div className={`p-2.5 rounded-2xl ${dialog.mode === 'stop' ? 'bg-rose-100 text-rose-600' : 'bg-emerald-100 text-emerald-600'}`}>
                {dialog.mode === 'stop' ? <Siren size={20} /> : <RotateCcw size={20} />}
              </div>
              <div>
                <h3 className="text-sm font-bold text-slate-900">
                  {dialog.mode === 'stop' ? `Dừng khẩn cấp "${lotName}"` : `Mở lại bãi "${lotName}"`}
                </h3>
                <p className="text-[11px] text-slate-500">
                  {dialog.mode === 'stop'
                    ? 'Bãi sẽ từ chối đặt chỗ/check-in mới. Khách có booking đang hiệu lực nhận thông báo đa kênh.'
                    : 'Bãi trở lại trạng thái Active, nhận đặt chỗ và check-in bình thường.'}
                </p>
              </div>
            </div>

            <label className="block mt-4 text-[11px] font-bold text-slate-600 uppercase tracking-wider">
              Lý do (bắt buộc, ≥ 5 ký tự)
            </label>
            <textarea
              value={reason}
              onChange={(e) => setReason(e.target.value)}
              rows={3}
              autoFocus
              placeholder={dialog.mode === 'stop' ? 'VD: Cháy hệ thống điện tầng B1' : 'VD: Đã xử lý xong sự cố điện'}
              className="mt-1.5 w-full rounded-xl border border-slate-200 p-3 text-xs focus:outline-hidden focus:ring-2 focus:ring-indigo-500/40"
            />
            {reason.trim().length > 0 && !reasonValid && (
              <p className="text-[11px] text-rose-600 mt-1">Lý do phải có ít nhất 5 ký tự.</p>
            )}

            <div className="flex justify-end gap-2 mt-5">
              <button
                type="button"
                disabled={busy}
                onClick={() => setDialog(null)}
                className="px-4 py-2.5 rounded-xl border border-slate-300 text-slate-700 text-xs font-bold hover:bg-slate-50 disabled:opacity-50 transition-all cursor-pointer"
              >
                Hủy bỏ
              </button>
              <button
                type="button"
                disabled={!reasonValid || busy}
                onClick={() => { void submitDialog(); }}
                className={`px-5 py-2.5 rounded-xl text-white text-xs font-bold shadow-md disabled:opacity-50 disabled:cursor-not-allowed transition-all cursor-pointer ${
                  dialog.mode === 'stop' ? 'bg-rose-600 hover:bg-rose-700' : 'bg-emerald-600 hover:bg-emerald-700'
                }`}
              >
                {busy ? 'Đang xử lý…' : dialog.mode === 'stop' ? 'Xác nhận DỪNG KHẨN CẤP' : 'Xác nhận mở lại bãi'}
              </button>
            </div>
          </div>
        </div>
      )}
    </div>
  );
};

const formatTime = (iso: string): string => {
  const date = new Date(iso);
  return Number.isNaN(date.getTime()) ? iso : date.toLocaleString('vi-VN');
};

const humanizeStatus = (status: string): string => {
  const map: Record<string, string> = {
    Active: 'Đang hoạt động',
    Suspended: 'Đang đóng khẩn cấp',
    PendingApproval: 'Chờ duyệt',
    Available: 'Trống',
    Occupied: 'Đang đỗ',
    Reserved: 'Đã đặt',
    Blocked: 'Bị chặn',
    Maintenance: 'Bảo trì',
  };
  return map[status] ?? status;
};

const extractError = (err: unknown): string => {
  const detail = (err as { response?: { data?: { detail?: string; title?: string } } })?.response?.data;
  return detail?.detail || detail?.title || 'Thao tác thất bại. Vui lòng thử lại.';
};
