import React, { useCallback, useEffect, useMemo, useState } from 'react';
import {
  gateApi,
  type ExitFeeQuoteDto,
  type GateEventDto,
  type GateExceptionActionType,
  type GateExceptionResultDto,
  type GateExceptionTypeEnum,
  type GateNoShowCandidateDto,
  type OverrideSlotStatusResultDto,
  type ShiftBookingItemDto,
} from '@/api/gateApi';
import { ownerStructureApi } from '@/api/ownerStructureApi';
import { LaneCameraIdle, LaneCameraScanner } from '@/areas/gate/components/LaneCameraScanner';

interface GateEvent {
  id: string;
  plate: string;
  lane: string;
  type: string;
  time: string;
  status: string;
  statusColor: string;
}

/** UI-33 (US-049): 1 slot của bãi (flatten hierarchy Zones → Floors → Slots) cho Staff chọn ghi đè. */
interface GateSlotOption {
  id: number;
  code: string;
  floorLabel: string;
  state: string;
  isActive: boolean;
}

const LOTS = [
  { id: 1, name: 'Bãi xe Vincom Đồng Khởi' },
  { id: 2, name: 'Bãi xe Sân bay Tân Sơn Nhất (P2)' },
];

// UI-33 (US-048): loại ngoại lệ + hành động thủ công – khớp GateExceptionTypes.Allowed / GateExceptionAction.
const EXCEPTION_TYPES = [
  { value: 'OcrFailed', label: 'OCR/ANPR đọc sai biển số' },
  { value: 'ManualPlateCorrection', label: 'Staff sửa biển số thủ công' },
  { value: 'PaymentFailed', label: 'Thanh toán lỗi tại cổng' },
  { value: 'EmergencyBarrierOpen', label: 'Mở barie khẩn cấp' },
  { value: 'VehicleArrived', label: 'Xe đến không khớp vé' },
] as const;

const EXCEPTION_ACTIONS = [
  { value: 'RecordOnly', label: 'Chỉ ghi nhận (không thực thi)' },
  { value: 'ManualCheckIn', label: 'Check-in thủ công (cho xe vào)' },
  { value: 'ManualCheckOut', label: 'Check-out thủ công (cho xe ra)' },
  { value: 'ConfirmCashPayment', label: 'Xác nhận đã thu tiền mặt' },
  { value: 'EmergencyBarrierOpen', label: 'Mở barie khẩn cấp' },
] as const;

// UI-33 (US-049): trạng thái slot được phép ghi đè (Reserved do hệ thống booking quản lý).
const SLOT_STATES = [
  { value: 'Available', label: 'Trống (Available)' },
  { value: 'Occupied', label: 'Đang đỗ (Occupied)' },
  { value: 'Maintenance', label: 'Bảo trì (Maintenance)' },
] as const;

const labelCls = 'text-[11px] font-bold text-slate-600 uppercase';
const inputCls = 'w-full px-3 py-2 text-xs border border-slate-300 rounded-xl focus:outline-hidden focus:border-blue-500';
const selectCls = 'w-full px-3 py-2 text-xs border border-slate-300 rounded-xl bg-white font-semibold text-slate-700 focus:outline-hidden focus:border-blue-500 cursor-pointer';
const actionBtnCls = 'w-full px-5 py-2.5 rounded-xl text-xs font-bold text-white cursor-pointer disabled:opacity-50 disabled:cursor-not-allowed flex items-center justify-center gap-1.5';

const slotStateBadge = (state: string): { label: string; cls: string } => {
  switch (state) {
    case 'Available':
      return { label: 'TRỐNG', cls: 'bg-emerald-50 text-emerald-700 border-emerald-200' };
    case 'Occupied':
      return { label: 'ĐANG ĐỖ', cls: 'bg-blue-50 text-blue-700 border-blue-200' };
    case 'Maintenance':
      return { label: 'BẢO TRÌ', cls: 'bg-amber-50 text-amber-700 border-amber-200' };
    default:
      return { label: state.toUpperCase(), cls: 'bg-slate-50 text-slate-600 border-slate-200' };
  }
};

const formatVnd = (v: number | null | undefined) => `${(v ?? 0).toLocaleString('vi-VN')}đ`;
const formatTime = (iso: string) => new Date(iso).toLocaleTimeString('vi-VN', { hour12: false });
const formatWindow = (start: string, end: string | null) =>
  end ? `${formatTime(start)} – ${formatTime(end)}` : `${formatTime(start)} → ...`;

const bookingBadge = (b: ShiftBookingItemDto): { label: string; cls: string } => {
  if (b.isOverdue) return { label: 'QUÁ GIỜ', cls: 'bg-red-50 text-red-700 border-red-200' };
  switch (b.status) {
    case 'Confirmed':
      return { label: 'SẮP ĐẾN', cls: 'bg-blue-50 text-blue-700 border-blue-200' };
    case 'CheckedIn':
    case 'Parking':
      return { label: 'ĐANG ĐỖ', cls: 'bg-emerald-50 text-emerald-700 border-emerald-200' };
    case 'NoShow':
      return { label: 'NO-SHOW', cls: 'bg-slate-100 text-slate-500 border-slate-200' };
    default:
      return { label: b.status.toUpperCase(), cls: 'bg-slate-50 text-slate-600 border-slate-200' };
  }
};

export const GateConsolePage: React.FC = () => {
  const [lotId, setLotId] = useState(LOTS[0].id);
  const [staffUserId, setStaffUserId] = useState('4');
  const [search, setSearch] = useState('');
  const [bookings, setBookings] = useState<ShiftBookingItemDto[]>([]);
  const [inboundPlate, setInboundPlate] = useState('');
  const [inboundBookingCode, setInboundBookingCode] = useState('');
  const [inboundQrToken, setInboundQrToken] = useState('');
  const [outboundPlate, setOutboundPlate] = useState('');
  const [quote, setQuote] = useState<ExitFeeQuoteDto | null>(null);
  const [events, setEvents] = useState<GateEvent[]>([]);
  const [alertNotice, setAlertNotice] = useState<string | null>(null);
  const [error, setError] = useState<string | null>(null);
  const [busy, setBusy] = useState(false);
  const [scanner, setScanner] = useState<{ mode: 'qr' | 'plate'; target: 'inbound' | 'outbound' } | null>(null);

  // UI-32 (US-039): ứng viên No-show + modal xác nhận hủy.
  const [noShowCandidates, setNoShowCandidates] = useState<GateNoShowCandidateDto[]>([]);
  const [noShowTarget, setNoShowTarget] = useState<ShiftBookingItemDto | null>(null);
  const [noShowReason, setNoShowReason] = useState('');

  // UI-33 (US-048): form ngoại lệ + audit log.
  const [exType, setExType] = useState<GateExceptionTypeEnum>('OcrFailed');
  const [exAction, setExAction] = useState<GateExceptionActionType>('RecordOnly');
  const [exPlate, setExPlate] = useState('');
  const [exBookingCode, setExBookingCode] = useState('');
  const [exReason, setExReason] = useState('');
  const [exResult, setExResult] = useState<GateExceptionResultDto | null>(null);
  const [exLog, setExLog] = useState<GateEventDto[]>([]);

  // UI-33 (US-049): sơ đồ slot + form ghi đè.
  const [slots, setSlots] = useState<GateSlotOption[]>([]);
  const [slotsLoading, setSlotsLoading] = useState(false);
  const [slotId, setSlotId] = useState('');
  const [slotNewState, setSlotNewState] = useState<'Available' | 'Occupied' | 'Maintenance'>('Occupied');
  const [slotReason, setSlotReason] = useState('');
  const [slotResult, setSlotResult] = useState<OverrideSlotStatusResultDto | null>(null);

  const handleScanResult = (value: string) => {
    if (!scanner) return;
    if (scanner.mode === 'qr' && scanner.target === 'inbound') {
      setInboundQrToken(value);
    } else if (scanner.mode === 'plate' && scanner.target === 'inbound') {
      setInboundPlate(value);
    } else if (scanner.mode === 'plate' && scanner.target === 'outbound') {
      setOutboundPlate(value);
      setQuote(null);
    }
    setScanner(null);
  };

  const showAlert = (msg: string) => {
    setAlertNotice(msg);
    setTimeout(() => setAlertNotice(null), 4500);
  };

  // UC-20 (US-076): booking trong ca trực của bãi được gán.
  const loadBookings = useCallback(async () => {
    try {
      const data = await gateApi.shiftBookings({
        parkingLotId: lotId,
        staffUserId: Number(staffUserId) || undefined,
        search: search.trim() || undefined,
      });
      setBookings(data);
      setError(null);
    } catch (e) {
      setError((e as Error).message);
    }
  }, [lotId, staffUserId, search]);

  useEffect(() => {
    void loadBookings();
  }, [loadBookings]);

  // UI-32 (US-039): ứng viên No-show quá mốc ân hạn 30' của bãi đang trực.
  const loadNoShowCandidates = useCallback(async () => {
    try {
      const data = await gateApi.noShowCandidates(lotId);
      setNoShowCandidates(data);
    } catch {
      setNoShowCandidates([]);
    }
  }, [lotId]);

  // UI-33 (US-048): nhật ký audit ngoại lệ mới nhất của bãi.
  const loadExceptionLog = useCallback(async () => {
    try {
      const data = await gateApi.listGateExceptions({ parkingLotId: lotId, limit: 6 });
      setExLog(data);
    } catch {
      setExLog([]);
    }
  }, [lotId]);

  // UI-33 (US-049): flatten hierarchy (Zones → Floors → Slots) để Staff chọn slot ghi đè.
  const loadSlots = useCallback(async () => {
    setSlotsLoading(true);
    try {
      const hierarchy = await ownerStructureApi.getHierarchy(lotId);
      const options = (hierarchy.zones ?? []).flatMap((zone) =>
        (zone.floors ?? []).flatMap((floor) =>
          (floor.slots ?? []).map((slot) => ({
            id: slot.id,
            code: slot.code,
            floorLabel: `${zone.name} · ${floor.name}`,
            state: slot.state,
            isActive: slot.isActive,
          })),
        ),
      );
      setSlots(options);
      setSlotId((prev) => (prev && options.some((s) => String(s.id) === prev) ? prev : options[0] ? String(options[0].id) : ''));
    } catch {
      setSlots([]);
      setSlotId('');
    } finally {
      setSlotsLoading(false);
    }
  }, [lotId]);

  useEffect(() => {
    void loadNoShowCandidates();
    void loadExceptionLog();
    void loadSlots();
  }, [loadNoShowCandidates, loadExceptionLog, loadSlots]);

  const pushEvent = (plate: string, lane: GateEvent['lane'], type: GateEvent['type'], status: string, statusColor: string) => {
    const ev: GateEvent = {
      id: `EV-${Math.floor(100000 + Math.random() * 900000)}`,
      plate,
      lane,
      type,
      time: new Date().toLocaleTimeString('vi-VN', { hour12: false }),
      status,
      statusColor,
    };
    setEvents((prev) => [ev, ...prev]);
  };

  const handleInboundCheckIn = async () => {
    if (busy) return;
    const plate = inboundPlate.trim();
    if (!plate) {
      showAlert('Vui lòng nhập biển số xe trước khi mở barie vào.');
      return;
    }
    setBusy(true);
    try {
      const bookingCode = inboundBookingCode.trim().toUpperCase() || undefined;
      const qrToken = inboundQrToken.trim() || undefined;
      const session = await gateApi.checkIn({
        parkingLotId: lotId,
        method: qrToken ? 'Qr' : bookingCode ? 'BookingCode' : 'Manual',
        plate,
        bookingCode,
        qrToken,
        staffUserId: Number(staffUserId) || undefined,
      });
      const methodLabel = qrToken ? 'QR vé đặt chỗ' : bookingCode ? `Booking ${bookingCode}` : 'Vãng lai';
      pushEvent(session.plateDisplay, 'Làn vào #1', 'Check-in',
        methodLabel,
        qrToken || bookingCode ? 'bg-emerald-50 text-emerald-700 border-emerald-200' : 'bg-slate-50 text-slate-600 border-slate-200');
      showAlert(`Làn vào #1: Đã check-in xe ${session.plateDisplay} (${methodLabel}) và nâng cần Barie!`);
      setInboundPlate('');
      setInboundBookingCode('');
      setInboundQrToken('');
      void loadBookings();
    } catch (e) {
      setError((e as Error).message);
    } finally {
      setBusy(false);
    }
  };

  const handleExitQuote = async () => {
    if (busy) return;
    const plate = outboundPlate.trim();
    if (!plate) {
      showAlert('Nhập biển số xe ra để chốt phí.');
      return;
    }
    setBusy(true);
    setQuote(null);
    try {
      const q = await gateApi.exitQuote(lotId, plate);
      setQuote(q);
    } catch (e) {
      setError((e as Error).message);
    } finally {
      setBusy(false);
    }
  };

  const handleConfirmCheckOut = async () => {
    if (busy || !quote) return;
    setBusy(true);
    try {
      const result = await gateApi.checkOut({
        parkingLotId: lotId,
        plate: outboundPlate.trim(),
        method: 'Manual',
        staffUserId: Number(staffUserId) || undefined,
        confirmFee: true,
      });
      const fee = result.fee;
      pushEvent(result.session.plateDisplay, 'Làn ra #2', 'Check-out',
        fee && fee.additionalDue > 0 ? `Đã thu ${formatVnd(fee.additionalDue)}` : 'Không thu thêm',
        fee && fee.additionalDue > 0 ? 'bg-blue-50 text-blue-700 border-blue-200' : 'bg-emerald-50 text-emerald-700 border-emerald-200');
      showAlert(`Làn ra #2: Xe ${result.session.plateDisplay} đã ra, tổng phí ${formatVnd(fee?.sessionFee)} (thu thêm ${formatVnd(fee?.additionalDue)}).`);
      if (result.warning) setError(result.warning);
      setQuote(null);
      setOutboundPlate('');
      void loadBookings();
    } catch (e) {
      setError((e as Error).message);
    } finally {
      setBusy(false);
    }
  };

  // ===== UI-32 (US-039): hủy booking No-show sau 30' quá hạn check-in =====
  const requireStaffId = (): number | null => {
    const id = Number(staffUserId);
    if (!id) {
      showAlert('Vui lòng nhập Mã NV (StaffUserId) ở góc phải trên trước khi thao tác.');
      return null;
    }
    return id;
  };

  const handleConfirmNoShowCancel = async () => {
    if (busy || !noShowTarget?.bookingCode) return;
    const staffId = requireStaffId();
    if (!staffId) return;
    setBusy(true);
    try {
      const result = await gateApi.cancelNoShow(noShowTarget.bookingCode, {
        parkingLotId: lotId,
        staffUserId: staffId,
        reason: noShowReason.trim() || undefined,
      });
      pushEvent(noShowTarget.plateDisplay, 'Cổng trực', 'Hủy No-show',
        result.slotReleased ? `Đã nhả slot ${result.slotCode ?? '—'}` : 'Không có slot giữ',
        'bg-rose-50 text-rose-700 border-rose-200');
      showAlert(`HỦY NO-SHOW ${result.bookingCode} (quá hạn ${result.minutesOverdue}'). ${result.auditNote}`);
      setNoShowTarget(null);
      setNoShowReason('');
      void loadBookings();
      void loadNoShowCandidates();
    } catch (e) {
      setError((e as Error).message);
    } finally {
      setBusy(false);
    }
  };

  // ===== UI-33 (US-048): ghi nhận + xử lý ngoại lệ thủ công tại cổng =====
  const handleRecordException = async () => {
    if (busy) return;
    const staffId = requireStaffId();
    if (!staffId) return;
    if (!exReason.trim()) {
      showAlert('Bắt buộc nhập lý do xử lý ngoại lệ (audit log dùng cho tranh chấp).');
      return;
    }
    setBusy(true);
    try {
      const result = await gateApi.recordGateException({
        parkingLotId: lotId,
        action: exAction,
        exceptionType: exType,
        reason: exReason.trim(),
        staffUserId: staffId,
        plate: exPlate.trim() || undefined,
        bookingCode: exBookingCode.trim().toUpperCase() || undefined,
      });
      pushEvent(result.plateDisplay ?? (exPlate.trim() || '—'), 'Cổng trực', 'Ngoại lệ',
        `${result.action} · ${result.status}`,
        'bg-purple-50 text-purple-700 border-purple-200');
      showAlert(`Đã ghi nhận ngoại lệ #${result.eventId} (${result.exceptionType}). ${result.auditNote}`);
      setExResult(result);
      setExReason('');
      void loadExceptionLog();
      void loadBookings();
    } catch (e) {
      setError((e as Error).message);
    } finally {
      setBusy(false);
    }
  };

  // ===== UI-33 (US-049): Staff ghi đè trạng thái slot khi cảm biến hỏng/sai lệch =====
  const handleSlotOverride = async () => {
    if (busy) return;
    const staffId = requireStaffId();
    if (!staffId) return;
    const targetSlotId = Number(slotId);
    if (!targetSlotId) {
      showAlert('Vui lòng chọn slot cần ghi đè trạng thái (bấm "Tải slot" nếu danh sách trống).');
      return;
    }
    if (!slotReason.trim()) {
      showAlert('Bắt buộc nhập lý do ghi đè (cảm biến hỏng / sai lệch thực tế).');
      return;
    }
    setBusy(true);
    try {
      const result = await gateApi.overrideSlot(targetSlotId, {
        parkingLotId: lotId,
        newState: slotNewState,
        staffUserId: staffId,
        reason: slotReason.trim(),
      });
      pushEvent(`Slot ${result.slotCode}`, 'Sơ đồ bãi', 'Ghi đè slot',
        `${result.fromState} → ${result.toState}`,
        'bg-indigo-50 text-indigo-700 border-indigo-200');
      showAlert(`Đã ghi đè slot ${result.slotCode}: ${result.fromState} → ${result.toState}.`);
      setSlotResult(result);
      setSlotReason('');
      void loadSlots();
    } catch (e) {
      setError((e as Error).message);
    } finally {
      setBusy(false);
    }
  };

  // UI-32: tập mã booking đủ điều kiện hủy No-show; UI-33: slot đang chọn trong dropdown.
  const noShowCodes = useMemo(() => new Set(noShowCandidates.map((c) => c.code)), [noShowCandidates]);
  const noShowInfo = noShowTarget?.bookingCode
    ? noShowCandidates.find((c) => c.code === noShowTarget.bookingCode)
    : undefined;
  const selectedSlot = slots.find((s) => String(s.id) === slotId) ?? null;

  return (
    <div className="max-w-7xl mx-auto px-4 sm:px-6 lg:px-8 py-8 space-y-6">
      {/* Toast Notice */}
      {alertNotice && (
        <div className="fixed bottom-6 right-6 z-50 flex items-center gap-2 px-4 py-3 bg-slate-900 text-white rounded-xl shadow-xl text-sm font-semibold animate-bounce">
          <span className="material-symbols-outlined text-emerald-400 text-[20px]">check_circle</span>
          <span>{alertNotice}</span>
        </div>
      )}

      {/* Error Banner */}
      {error && (
        <div className="flex items-center gap-2 px-4 py-3 bg-red-50 text-red-700 border border-red-200 rounded-xl text-sm font-semibold">
          <span className="material-symbols-outlined text-[20px]">error</span>
          <span className="flex-1">{error}</span>
          <button onClick={() => setError(null)} className="text-xs font-bold hover:text-red-900 cursor-pointer">ĐÓNG</button>
        </div>
      )}

      {/* UI-32 (US-039): Modal xác nhận hủy booking No-show */}
      {noShowTarget && (
        <div className="fixed inset-0 z-50 flex items-center justify-center bg-slate-900/60 p-4">
          <div className="w-full max-w-md bg-white rounded-2xl border border-slate-200 shadow-2xl p-6 space-y-4">
            <div>
              <h3 className="text-base font-bold text-slate-900 flex items-center gap-2">
                <span className="material-symbols-outlined text-rose-600">person_off</span>
                Hủy booking No-show (US-039)
              </h3>
              <p className="text-xs text-slate-500 mt-1">
                Booking đã quá mốc ân hạn check-in 30 phút. Thao tác sẽ chuyển booking sang NO-SHOW, nhả slot đã giữ
                và ghi audit GateEvents kèm mã NV + lý do.
              </p>
            </div>
            <div className="rounded-xl border border-slate-200 bg-slate-50 px-4 py-3 text-xs space-y-1.5">
              <div className="flex justify-between"><span className="text-slate-500">Mã booking</span><span className="font-mono font-bold text-slate-900">{noShowTarget.bookingCode}</span></div>
              <div className="flex justify-between"><span className="text-slate-500">Biển số</span><span className="font-mono font-bold text-slate-900">{noShowTarget.plateDisplay}</span></div>
              <div className="flex justify-between"><span className="text-slate-500">Slot giữ chỗ</span><span className="font-mono text-slate-700">{noShowTarget.slotCode ?? '—'}</span></div>
              <div className="flex justify-between"><span className="text-slate-500">Quá hạn</span><span className="font-bold text-rose-600">{noShowInfo ? `${noShowInfo.minutesOverdue} phút` : '—'}</span></div>
            </div>
            <div>
              <label className={labelCls}>Lý do (ghi vào audit log)</label>
              <textarea
                value={noShowReason}
                onChange={(e) => setNoShowReason(e.target.value)}
                rows={2}
                placeholder="VD: Khách xác nhận không đến nữa..."
                className={`mt-1 ${inputCls}`}
              />
              <p className="text-[10px] text-slate-400 mt-1">Bỏ trống → hệ thống ghi mặc định "Khách không đến sau 30 phút".</p>
            </div>
            <div className="flex gap-2">
              <button
                onClick={() => setNoShowTarget(null)}
                className="flex-1 px-4 py-2.5 text-xs font-bold text-slate-600 bg-white border border-slate-300 rounded-xl hover:bg-slate-50 cursor-pointer"
              >
                ĐÓNG (Khách báo đang đến)
              </button>
              <button
                onClick={() => void handleConfirmNoShowCancel()}
                disabled={busy}
                className="flex-1 px-4 py-2.5 text-xs font-bold text-white bg-rose-600 rounded-xl hover:bg-rose-700 cursor-pointer disabled:opacity-50 disabled:cursor-not-allowed"
              >
                XÁC NHẬN HỦY NO-SHOW
              </button>
            </div>
          </div>
        </div>
      )}

      {/* Header */}
      <div className="flex flex-col md:flex-row md:items-center justify-between gap-4 pb-6 border-b border-slate-200">
        <div>
          <div className="inline-flex items-center gap-2 px-3 py-1 rounded-full bg-amber-100 text-amber-800 text-xs font-semibold uppercase tracking-wider mb-2">
            🚧 Bàn Điều Khiển Cổng & Barie Tự Động (Gate Console)
          </div>
          <h1 className="text-2xl md:text-3xl font-extrabold text-slate-900 tracking-tight">
            Điều khiển Cổng Barie & Giám sát ANPR
          </h1>
          <p className="text-slate-500 text-sm mt-1">
            Trực ban soát vé: quét QR/mã booking (ân hạn đến trễ 15 phút), chốt phí quá giờ và điều khiển barie thời gian thực.
          </p>
        </div>
        <div className="flex flex-col items-start md:items-end gap-2">
          <div className="flex items-center gap-2">
            <span className="w-3 h-3 rounded-full bg-emerald-500 animate-pulse"></span>
            <span className="text-xs font-bold text-emerald-700 bg-emerald-50 px-3 py-1.5 rounded-lg border border-emerald-200">
              BARIE CONTROLLER: ONLINE (0.3s LATENCY)
            </span>
          </div>
          <div className="flex items-center gap-2">
            <select
              value={lotId}
              onChange={(e) => setLotId(Number(e.target.value))}
              className="px-3 py-2 text-xs border border-slate-300 rounded-xl bg-white font-semibold text-slate-700 focus:outline-hidden focus:border-blue-500 cursor-pointer"
            >
              {LOTS.map((l) => (
                <option key={l.id} value={l.id}>{l.id} · {l.name}</option>
              ))}
            </select>
            <input
              type="text"
              value={staffUserId}
              onChange={(e) => setStaffUserId(e.target.value.replace(/\D/g, ''))}
              placeholder="Mã NV"
              title="StaffUserId (dữ liệu demo: 4 = Staff Vincom)"
              className="w-24 px-3 py-2 text-xs border border-slate-300 rounded-xl font-mono focus:outline-hidden focus:border-blue-500"
            />
            <button
              onClick={() => void loadBookings()}
              className="px-3 py-2 text-xs font-bold text-slate-600 bg-white border border-slate-300 rounded-xl hover:bg-slate-50 cursor-pointer flex items-center gap-1"
            >
              <span className="material-symbols-outlined text-[16px]">refresh</span>
              Làm mới
            </button>
          </div>
        </div>
      </div>

      {/* UC-20 (US-076): Danh sách booking trong ca trực */}
      <div className="p-6 bg-white rounded-2xl border border-slate-200 shadow-xs space-y-4">
        <div className="flex flex-col sm:flex-row sm:items-center justify-between gap-3">
          <div>
            <h3 className="font-bold text-slate-900 text-base flex items-center gap-2">
              <span className="material-symbols-outlined text-blue-600">event_available</span>
              Booking Trong Ca Trực (Shift Bookings)
            </h3>
            <p className="text-xs text-slate-500 mt-0.5">
              Sắp theo giờ đến · tìm theo mã booking/biển số · &quot;QUÁ GIỜ&quot; = trễ quá 15&apos; ân hạn (US-044) · không hiển thị SĐT/CCCD khách
            </p>
          </div>
          <div className="relative sm:w-72">
            <span className="material-symbols-outlined absolute left-3 top-1/2 -translate-y-1/2 text-slate-400 text-[18px]">search</span>
            <input
              type="text"
              value={search}
              onChange={(e) => setSearch(e.target.value)}
              placeholder="Tìm mã booking hoặc biển số..."
              className="w-full pl-9 pr-3 py-2 text-xs border border-slate-300 rounded-xl focus:outline-hidden focus:border-blue-500 font-mono"
            />
          </div>
        </div>

        <div className="overflow-x-auto border border-slate-200 rounded-xl">
          <table className="w-full text-left text-xs">
            <thead className="bg-slate-50 border-b border-slate-200 text-slate-600 font-bold uppercase">
              <tr>
                <th className="px-4 py-3">Mã booking</th>
                <th className="px-4 py-3">Biển số</th>
                <th className="px-4 py-3">Slot</th>
                <th className="px-4 py-3">Khung giờ</th>
                <th className="px-4 py-3">Trạng thái</th>
                <th className="px-4 py-3 text-right">Giá đã khóa</th>
                <th className="px-4 py-3"></th>
              </tr>
            </thead>
            <tbody className="divide-y divide-slate-100">
              {bookings.length === 0 && (
                <tr>
                  <td colSpan={7} className="px-4 py-6 text-center text-slate-400">
                    Không có booking nào trong ca trực của bãi này.
                  </td>
                </tr>
              )}
              {bookings.map((b) => {
                const badge = bookingBadge(b);
                return (
                  <tr key={b.code} className="hover:bg-slate-50 transition-colors">
                    <td className="px-4 py-3 font-mono font-bold text-slate-700">
                      {b.code}
                      {b.source === 'WalkIn' && (
                        <span className="ml-2 px-1.5 py-0.5 rounded bg-slate-100 text-slate-500 text-[10px] font-bold">VÃNG LAI</span>
                      )}
                    </td>
                    <td className="px-4 py-3">
                      <span className="font-mono font-bold text-slate-900 bg-slate-100 px-2 py-0.5 rounded border border-slate-200">
                        {b.plateDisplay}
                      </span>
                    </td>
                    <td className="px-4 py-3 font-mono text-slate-500">{b.slotCode ?? '—'}</td>
                    <td className="px-4 py-3 font-mono text-slate-500">{formatWindow(b.startAtUtc, b.endAtUtc)}</td>
                    <td className="px-4 py-3">
                      <span className={`px-2 py-0.5 rounded-md text-[10px] font-bold border ${badge.cls}`}>{badge.label}</span>
                    </td>
                    <td className="px-4 py-3 text-right font-mono font-semibold text-slate-700">{formatVnd(b.totalAmount)}</td>
                    <td className="px-4 py-3 text-right">
                      <div className="flex items-center justify-end gap-1.5">
                        {b.bookingCode && b.status === 'Confirmed' && noShowCodes.has(b.bookingCode) && (
                          <button
                            onClick={() => { setNoShowTarget(b); setNoShowReason(''); }}
                            title="Booking quá mốc ân hạn check-in 30 phút — Staff được phép hủy No-show (US-039)"
                            className="px-3 py-1.5 text-[10px] font-bold text-rose-700 bg-rose-50 border border-rose-200 rounded-lg hover:bg-rose-100 cursor-pointer"
                          >
                            HỦY NO-SHOW
                          </button>
                        )}
                        {b.status !== 'NoShow' && (
                          <button
                            onClick={() => { setOutboundPlate(b.plateDisplay); setQuote(null); }}
                            className="px-3 py-1.5 text-[10px] font-bold text-amber-700 bg-amber-50 border border-amber-200 rounded-lg hover:bg-amber-100 cursor-pointer"
                          >
                            CHỐT RA
                          </button>
                        )}
                      </div>
                    </td>
                  </tr>
                );
              })}
            </tbody>
          </table>
        </div>
      </div>

      {/* 2-Column Console Layout */}
      <div className="grid grid-cols-1 lg:grid-cols-2 gap-6">
        {/* Left: Check-in */}
        <div className="bg-white p-6 rounded-2xl border border-slate-200 shadow-xs space-y-4">
          <div className="flex items-center justify-between">
            <h2 className="text-base font-bold text-slate-900 flex items-center gap-2">
              <span className="material-symbols-outlined text-blue-600">login</span>
              LÀN VÀO (INBOUND CHECK-IN #01)
            </h2>
            <span className="text-xs font-bold text-emerald-600 bg-emerald-50 px-2 py-0.5 rounded border border-emerald-200">
              CAMERA ANPR SẴN SÀNG
            </span>
          </div>

          {scanner?.target === 'inbound' ? (
            <LaneCameraScanner
              key={scanner.mode}
              mode={scanner.mode}
              onDetected={handleScanResult}
              onClose={() => setScanner(null)}
            />
          ) : (
            <LaneCameraIdle hint="Bấm QUÉT QR VÉ hoặc QUÉT BIỂN SỐ bên dưới để bật camera." />
          )}

          <div className="grid grid-cols-2 gap-2">
            {inboundQrToken ? (
              <div className="flex items-center justify-between gap-2 px-3 py-2 rounded-xl bg-emerald-50 border border-emerald-200 text-xs font-bold text-emerald-700">
                <span className="flex items-center gap-1.5">
                  <span className="material-symbols-outlined text-[16px]">qr_code_2</span>
                  Đã quét QR vé
                </span>
                <button onClick={() => setInboundQrToken('')} className="text-emerald-600 hover:text-emerald-900 cursor-pointer" title="Bỏ QR đã quét">
                  <span className="material-symbols-outlined text-[16px]">close</span>
                </button>
              </div>
            ) : (
              <button
                onClick={() => setScanner({ mode: 'qr', target: 'inbound' })}
                className={`px-3 py-2 rounded-xl text-xs font-bold flex items-center justify-center gap-1.5 cursor-pointer ${
                  scanner?.target === 'inbound' && scanner.mode === 'qr' ? 'bg-slate-900 text-white ring-2 ring-emerald-400' : 'bg-slate-800 text-white hover:bg-slate-900'
                }`}
              >
                <span className="material-symbols-outlined text-[18px]">qr_code_scanner</span>
                QUÉT QR VÉ ĐẶT CHỖ
              </button>
            )}
            <button
              onClick={() => setScanner({ mode: 'plate', target: 'inbound' })}
              className={`px-3 py-2 rounded-xl text-xs font-bold flex items-center justify-center gap-1.5 cursor-pointer ${
                scanner?.target === 'inbound' && scanner.mode === 'plate' ? 'bg-blue-700 text-white ring-2 ring-blue-300' : 'bg-blue-600 text-white hover:bg-blue-700'
              }`}
            >
              <span className="material-symbols-outlined text-[18px]">directions_car</span>
              QUÉT BIỂN SỐ
            </button>
          </div>

          <div className="flex gap-2">
            <input
              type="text"
              value={inboundPlate}
              onChange={(e) => setInboundPlate(e.target.value)}
              placeholder="Nhập biển số (VD: 51F-123.45)..."
              className="flex-1 px-4 py-2.5 text-xs border border-slate-300 rounded-xl focus:outline-hidden focus:border-blue-500 font-mono uppercase"
            />
            <button
              onClick={() => void handleInboundCheckIn()}
              disabled={busy}
              className="px-5 py-2.5 bg-emerald-600 text-white rounded-xl text-xs font-bold hover:bg-emerald-700 shadow-xs transition-colors flex items-center gap-1.5 cursor-pointer shrink-0 disabled:opacity-50 disabled:cursor-not-allowed"
            >
              <span className="material-symbols-outlined text-[18px]">lock_open</span>
              MỞ BARIE VÀO
            </button>
          </div>
          <div className="flex gap-2 items-center">
            <input
              type="text"
              value={inboundBookingCode}
              onChange={(e) => setInboundBookingCode(e.target.value)}
              placeholder="Mã booking (tùy chọn – VD: BK-0002). Có mã = kiểm tra Confirmed + ân hạn 15'"
              className="flex-1 px-4 py-2 text-xs border border-slate-300 rounded-xl focus:outline-hidden focus:border-blue-500 font-mono uppercase"
            />
          </div>
        </div>

        {/* Right: Check-out */}
        <div className="bg-white p-6 rounded-2xl border border-slate-200 shadow-xs space-y-4">
          <div className="flex items-center justify-between">
            <h2 className="text-base font-bold text-slate-900 flex items-center gap-2">
              <span className="material-symbols-outlined text-amber-600">logout</span>
              LÀN RA (OUTBOUND CHECK-OUT #02)
            </h2>
            <span className="text-xs font-bold text-amber-600 bg-amber-50 px-2 py-0.5 rounded border border-amber-200">
              CAMERA ANPR SẴN SÀNG
            </span>
          </div>

          {scanner?.target === 'outbound' ? (
            <LaneCameraScanner
              key={scanner.mode}
              mode={scanner.mode}
              onDetected={handleScanResult}
              onClose={() => setScanner(null)}
            />
          ) : (
            <LaneCameraIdle hint="Bấm QUÉT BIỂN SỐ bên dưới để bật camera." />
          )}

          <button
            onClick={() => setScanner({ mode: 'plate', target: 'outbound' })}
            className={`w-full px-3 py-2 rounded-xl text-xs font-bold flex items-center justify-center gap-1.5 cursor-pointer ${
              scanner?.target === 'outbound' ? 'bg-amber-700 text-white ring-2 ring-amber-300' : 'bg-amber-600 text-white hover:bg-amber-700'
            }`}
          >
            <span className="material-symbols-outlined text-[18px]">directions_car</span>
            QUÉT BIỂN SỐ
          </button>

          <div className="flex gap-2">
            <input
              type="text"
              value={outboundPlate}
              onChange={(e) => { setOutboundPlate(e.target.value); setQuote(null); }}
              placeholder="Nhập biển số xe ra (VD: 51F-123.45)..."
              className="flex-1 px-4 py-2.5 text-xs border border-slate-300 rounded-xl focus:outline-hidden focus:border-amber-500 font-mono uppercase"
            />
            <button
              onClick={() => void handleExitQuote()}
              disabled={busy}
              className="px-5 py-2.5 bg-slate-800 text-white rounded-xl text-xs font-bold hover:bg-slate-900 shadow-xs transition-colors flex items-center gap-1.5 cursor-pointer shrink-0 disabled:opacity-50 disabled:cursor-not-allowed"
            >
              <span className="material-symbols-outlined text-[18px]">calculate</span>
              TÍNH PHÍ
            </button>
          </div>

          {/* UC-22: Bảng chốt phí trước khi mở barie */}
          {quote && (
            <div className="p-4 rounded-xl border border-amber-200 bg-amber-50/60 space-y-3">
              <div className="flex items-center justify-between">
                <span className="text-xs font-bold text-amber-800 uppercase tracking-wider flex items-center gap-1">
                  <span className="material-symbols-outlined text-[16px]">receipt_long</span>
                  Chốt phí lượt {quote.sessionCode}
                </span>
                <span className="text-[10px] font-mono text-slate-500">
                  {quote.bookingCode ? `Booking ${quote.bookingCode} · ` : ''}Vào {formatTime(quote.entryAtUtc)} · {quote.minutesParked} phút
                </span>
              </div>

              {quote.isWithinBillingGrace && (
                <div className="text-xs font-bold text-emerald-700 bg-emerald-50 border border-emerald-200 rounded-lg px-3 py-2">
                  Trong ân hạn 15 phút (US-046) → phí 0đ, miễn phí mở barie.
                </div>
              )}

              <div className="space-y-1">
                {quote.lines.map((line, idx) => (
                  <div key={idx} className="flex items-center justify-between text-xs text-slate-600">
                    <span>{line.description}</span>
                    <span className="font-mono">{formatVnd(line.amount)}</span>
                  </div>
                ))}
              </div>

              <div className="border-t border-amber-200 pt-2 space-y-1">
                <div className="flex items-center justify-between text-xs text-slate-600">
                  <span>Tổng phí lượt gửi</span>
                  <span className="font-mono font-bold">{formatVnd(quote.sessionFee)}</span>
                </div>
                <div className="flex items-center justify-between text-xs text-slate-600">
                  <span>Đã trả trước (Price Lock)</span>
                  <span className="font-mono">−{formatVnd(quote.prepaidAmount)}</span>
                </div>
                {quote.overstayMinutes > 0 && (
                  <div className="flex items-center justify-between text-xs text-red-600 font-semibold">
                    <span>Quá giờ {quote.overstayMinutes} phút (× hệ số)</span>
                    <span className="font-mono">{formatVnd(quote.overstayAmount)}</span>
                  </div>
                )}
                <div className="flex items-center justify-between text-sm font-extrabold text-slate-900 pt-1">
                  <span>Cần thu thêm (tiền mặt)</span>
                  <span className="font-mono text-amber-700">{formatVnd(quote.additionalDue)}</span>
                </div>
              </div>

              <button
                onClick={() => void handleConfirmCheckOut()}
                disabled={busy}
                className="w-full px-5 py-2.5 bg-amber-600 text-white rounded-xl text-xs font-bold hover:bg-amber-700 shadow-xs transition-colors flex items-center justify-center gap-1.5 cursor-pointer disabled:opacity-50 disabled:cursor-not-allowed"
              >
                <span className="material-symbols-outlined text-[18px]">task_alt</span>
                XÁC NHẬN ĐÃ THU TIỀN & MỞ BARIE RA
              </button>
            </div>
          )}
        </div>
      </div>

      {/* UI-33 (US-048): Xử lý ngoại lệ thủ công */}
      <div className="grid grid-cols-1 lg:grid-cols-2 gap-6">
        <div className="bg-white p-6 rounded-2xl border border-slate-200 shadow-xs space-y-3">
          <h2 className="text-base font-bold text-slate-900 flex items-center gap-2">
            <span className="material-symbols-outlined text-purple-600">build_circle</span>
            XỬ LÝ NGOẠI LỆ THỦ CÔNG (US-048)
          </h2>
          <p className="text-xs text-slate-500">
            QR/biển số lỗi, thanh toán lỗi, hỏng barie... Mọi thao tác ghi GateEvents (mã NV, thời điểm, lý do) làm bằng chứng đối soát.
          </p>
          <div className="grid grid-cols-2 gap-2">
            <div>
              <label className={labelCls}>Loại ngoại lệ</label>
              <select value={exType} onChange={(e) => setExType(e.target.value as GateExceptionTypeEnum)} className={`mt-1 ${selectCls}`}>
                {EXCEPTION_TYPES.map((t) => (<option key={t.value} value={t.value}>{t.label}</option>))}
              </select>
            </div>
            <div>
              <label className={labelCls}>Hành động</label>
              <select value={exAction} onChange={(e) => setExAction(e.target.value as GateExceptionActionType)} className={`mt-1 ${selectCls}`}>
                {EXCEPTION_ACTIONS.map((a) => (<option key={a.value} value={a.value}>{a.label}</option>))}
              </select>
            </div>
          </div>
          <div className="grid grid-cols-2 gap-2">
            <div>
              <label className={labelCls}>Biển số (tuỳ chọn)</label>
              <input type="text" value={exPlate} onChange={(e) => setExPlate(e.target.value)} placeholder="51F-123.45" className={`mt-1 ${inputCls} font-mono`} />
            </div>
            <div>
              <label className={labelCls}>Mã booking (tuỳ chọn)</label>
              <input type="text" value={exBookingCode} onChange={(e) => setExBookingCode(e.target.value)} placeholder="BK-..." className={`mt-1 ${inputCls} font-mono`} />
            </div>
          </div>
          <div>
            <label className={labelCls}>Lý do (bắt buộc – audit tranh chấp)</label>
            <textarea value={exReason} onChange={(e) => setExReason(e.target.value)} rows={2} placeholder="VD: Camera ANPR hỏng, xác nhận biển số khớp QR vé..." className={`mt-1 ${inputCls}`} />
          </div>
          <button onClick={() => void handleRecordException()} disabled={busy} className={`${actionBtnCls} bg-purple-600 hover:bg-purple-700`}>
            <span className="material-symbols-outlined text-[18px]">note_add</span>
            GHI NHẬN & XỬ LÝ NGOẠI LỆ
          </button>
          {exResult && (
            <div className="text-xs rounded-lg border border-purple-200 bg-purple-50 px-3 py-2 text-purple-800 font-semibold">
              Sự kiện #{exResult.eventId} · {exResult.action} · {exResult.status} — {exResult.auditNote}
            </div>
          )}
          <div className="pt-1">
            <p className={`${labelCls} mb-1`}>Nhật ký ngoại lệ mới nhất (audit GateEvents)</p>
            <div className="overflow-x-auto border border-slate-200 rounded-xl">
              <table className="w-full text-left text-xs">
                <thead className="bg-slate-50 border-b border-slate-200 text-slate-600 font-bold uppercase">
                  <tr>
                    <th className="px-3 py-2">#</th>
                    <th className="px-3 py-2">Loại</th>
                    <th className="px-3 py-2">Biển số</th>
                    <th className="px-3 py-2">NV</th>
                    <th className="px-3 py-2">Thời điểm</th>
                  </tr>
                </thead>
                <tbody className="divide-y divide-slate-100">
                  {exLog.length === 0 && (
                    <tr><td colSpan={5} className="px-3 py-4 text-center text-slate-400">Chưa có ngoại lệ nào được ghi nhận ở bãi này.</td></tr>
                  )}
                  {exLog.map((ev) => (
                    <tr key={ev.id} className="hover:bg-slate-50 transition-colors">
                      <td className="px-3 py-2 font-mono text-slate-500">{ev.id}</td>
                      <td className="px-3 py-2 font-semibold text-slate-700">{ev.eventType}</td>
                      <td className="px-3 py-2 font-mono">{ev.plateDisplay ?? '—'}</td>
                      <td className="px-3 py-2 font-mono">{ev.performedByUserId ?? '—'}</td>
                      <td className="px-3 py-2 font-mono text-slate-500">{formatTime(ev.createdAtUtc)}</td>
                    </tr>
                  ))}
                </tbody>
              </table>
            </div>
          </div>
        </div>

        {/* UI-33 (US-049): Ghi đè trạng thái slot */}
        <div className="bg-white p-6 rounded-2xl border border-slate-200 shadow-xs space-y-3">
          <div className="flex items-center justify-between">
            <div>
              <h2 className="text-base font-bold text-slate-900 flex items-center gap-2">
                <span className="material-symbols-outlined text-indigo-600">grid_view</span>
                GHI ĐÈ TRẠNG THÁI SLOT (US-049)
              </h2>
              <p className="text-xs text-slate-500 mt-0.5">
                Cảm biến hỏng / sai lệch thực tế → Staff sửa tay (SlotStateLog nguồn StaffOverride). Không cho đặt Reserved.
              </p>
            </div>
            <button onClick={() => void loadSlots()} className="px-3 py-2 text-xs font-bold text-slate-600 bg-white border border-slate-300 rounded-xl hover:bg-slate-50 cursor-pointer flex items-center gap-1">
              <span className="material-symbols-outlined text-[16px]">refresh</span>
              Tải slot
            </button>
          </div>
          <div>
            <label className={labelCls}>Slot ({slots.length} ô)</label>
            <select value={slotId} onChange={(e) => setSlotId(e.target.value)} className={`mt-1 ${selectCls}`}>
              {slotsLoading && <option value="">Đang tải sơ đồ slot...</option>}
              {!slotsLoading && slots.length === 0 && <option value="">Không tải được danh sách slot ở bãi này</option>}
              {slots.map((s) => (
                <option key={s.id} value={String(s.id)}>{s.code} · {s.floorLabel} · {s.state}{s.isActive ? '' : ' (ngừng hoạt động)'}</option>
              ))}
            </select>
            {selectedSlot && (
              <span className={`mt-1.5 inline-block px-2 py-0.5 rounded-md text-[10px] font-bold border ${slotStateBadge(selectedSlot.state).cls}`}>
                Hiện tại: {slotStateBadge(selectedSlot.state).label}
              </span>
            )}
          </div>
          <div>
            <label className={labelCls}>Trạng thái mới</label>
            <select value={slotNewState} onChange={(e) => setSlotNewState(e.target.value as 'Available' | 'Occupied' | 'Maintenance')} className={`mt-1 ${selectCls}`}>
              {SLOT_STATES.map((s) => (<option key={s.value} value={s.value}>{s.label}</option>))}
            </select>
          </div>
          <div>
            <label className={labelCls}>Lý do (bắt buộc)</label>
            <textarea value={slotReason} onChange={(e) => setSlotReason(e.target.value)} rows={2} placeholder="VD: Cảm biến slot B1-05 báo sai, thực tế còn trống..." className={`mt-1 ${inputCls}`} />
          </div>
          <button onClick={() => void handleSlotOverride()} disabled={busy} className={`${actionBtnCls} bg-indigo-600 hover:bg-indigo-700`}>
            <span className="material-symbols-outlined text-[18px]">published_with_changes</span>
            XÁC NHẬN GHI ĐÈ TRẠNG THÁI SLOT
          </button>
          {slotResult && (
            <div className="text-xs rounded-lg border border-indigo-200 bg-indigo-50 px-3 py-2 text-indigo-800 font-semibold">
              Đã ghi đè slot {slotResult.slotCode}: {slotResult.fromState} → {slotResult.toState} · {slotResult.auditNote}
            </div>
          )}
        </div>
      </div>

      {/* Live Event Passage Feed */}
      <div className="p-6 bg-white rounded-2xl border border-slate-200 shadow-xs space-y-4">
        <div className="flex items-center justify-between">
          <div>
            <h3 className="font-bold text-slate-900 text-base flex items-center gap-2">
              <span className="material-symbols-outlined text-blue-600">history</span>
              Nhật ký Lượt xe Qua Cổng Thời Gian Thực (Live Gate Events)
            </h3>
            <p className="text-xs text-slate-500 mt-0.5">Tự động ghi nhận nhật ký vào/ra, trạng thái đối soát vé và lệnh relay Barie</p>
          </div>
          <span className="text-xs font-semibold text-slate-400 font-mono">Cổng trạm Vincom Mega Mall</span>
        </div>

        <div className="overflow-x-auto border border-slate-200 rounded-xl">
          <table className="w-full text-left text-xs">
            <thead className="bg-slate-50 border-b border-slate-200 text-slate-600 font-bold uppercase">
              <tr>
                <th className="px-4 py-3">Mã sự kiện</th>
                <th className="px-4 py-3">Biển số nhận diện</th>
                <th className="px-4 py-3">Làn điều khiển</th>
                <th className="px-4 py-3">Phân loại</th>
                <th className="px-4 py-3">Thời gian ghi nhận</th>
                <th className="px-4 py-3">Trạng thái Barie</th>
              </tr>
            </thead>
            <tbody className="divide-y divide-slate-100">
              {events.map((ev) => (
                <tr key={ev.id} className="hover:bg-slate-50 transition-colors">
                  <td className="px-4 py-3 font-mono text-slate-500">{ev.id}</td>
                  <td className="px-4 py-3">
                    <span className="font-mono font-bold text-slate-900 bg-slate-100 px-2 py-0.5 rounded border border-slate-200">
                      {ev.plate}
                    </span>
                  </td>
                  <td className="px-4 py-3 text-slate-700 font-medium">{ev.lane}</td>
                  <td className="px-4 py-3">
                    <span className={`px-2 py-0.5 rounded text-[10px] font-bold ${
                      ev.type === 'Check-in'
                        ? 'bg-emerald-100 text-emerald-800'
                        : ev.type === 'Check-out'
                          ? 'bg-amber-100 text-amber-800'
                          : 'bg-purple-100 text-purple-800'
                    }`}>
                      {ev.type}
                    </span>
                  </td>
                  <td className="px-4 py-3 font-mono text-slate-500">{ev.time}</td>
                  <td className="px-4 py-3">
                    <span className={`px-2 py-0.5 rounded-md text-[10px] font-bold border ${ev.statusColor}`}>
                      {ev.status}
                    </span>
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      </div>
    </div>
  );
};
