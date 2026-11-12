import React, { useCallback, useEffect, useState } from 'react';
import { useAppSelector } from '@/store';
import { gateApi } from '@/api/gateApi';
import type { GateMethod, ParkingSessionDto, ParkingSessionStatus } from '@/types/gate';
import { CameraScannerModal } from '@/areas/gate/components/CameraScannerModal';

const SESSION_STATUS_LABEL: Record<ParkingSessionStatus, string> = {
  Active: 'Đang đỗ',
  Completed: 'Đã ra bãi',
  Abnormal: 'Bất thường',
};

interface GateEvent {
  id: string;
  plate: string;
  type: 'Check-in' | 'Check-out';
  time: string;
  status: string;
  statusColor: string;
}

const DEFAULT_LOT_ID = Number(import.meta.env.VITE_DEFAULT_GATE_LOT_ID) || 1;

const CHECKIN_STATUS: Record<GateMethod, string> = {
  Qr: 'Khớp vé QR',
  BookingCode: 'Khớp mã Booking',
  Manual: 'Nhập tay',
  Ocr: 'Nhận diện ANPR',
};

const toEvent = (session: ParkingSessionDto, type: GateEvent['type'], method: GateMethod): GateEvent => ({
  id: session.code,
  plate: session.plateDisplay,
  type,
  time: new Date(type === 'Check-in' ? session.entryAtUtc : session.exitAtUtc ?? session.entryAtUtc).toLocaleTimeString('vi-VN'),
  status: type === 'Check-in' ? CHECKIN_STATUS[method] : 'Đã thanh toán / Mở Barie',
  statusColor:
    type === 'Check-in'
      ? 'bg-emerald-50 text-emerald-700 border-emerald-200'
      : 'bg-blue-50 text-blue-700 border-blue-200',
});

export const GateConsolePage: React.FC = () => {
  const staffUserId = useAppSelector((state) => state.auth.user?.id);
  const [parkingLotId, setParkingLotId] = useState(DEFAULT_LOT_ID);

  const [inboundMethod, setInboundMethod] = useState<GateMethod>('Manual');
  const [inboundPlate, setInboundPlate] = useState('');
  const [inboundBookingCode, setInboundBookingCode] = useState('');
  const [inboundQrToken, setInboundQrToken] = useState('');
  const [inboundBusy, setInboundBusy] = useState(false);

  const [outboundPlate, setOutboundPlate] = useState('');
  const [outboundSession, setOutboundSession] = useState<ParkingSessionDto | null>(null);
  const [outboundLookupDone, setOutboundLookupDone] = useState(false);
  const [outboundBusy, setOutboundBusy] = useState(false);

  const [scanner, setScanner] = useState<{ mode: 'qr' | 'plate'; target: 'inbound' | 'outbound' } | null>(null);
  const [events, setEvents] = useState<GateEvent[]>([]);
  const [alertNotice, setAlertNotice] = useState<{ text: string; isError: boolean } | null>(null);

  const [sessionStatus, setSessionStatus] = useState<ParkingSessionStatus>('Active');
  const [sessionList, setSessionList] = useState<ParkingSessionDto[]>([]);
  const [sessionLoading, setSessionLoading] = useState(false);

  const showAlert = (text: string, isError = false) => {
    setAlertNotice({ text, isError });
    window.setTimeout(() => setAlertNotice(null), 4500);
  };

  const loadSessions = useCallback(async (status: ParkingSessionStatus) => {
    setSessionLoading(true);
    try {
      const sessions = await gateApi.listSessions(parkingLotId, status);
      setSessionList(sessions);
    } catch (err) {
      showAlert(err instanceof Error ? err.message : 'Không tải được danh sách lượt xe.', true);
    } finally {
      setSessionLoading(false);
    }
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [parkingLotId]);

  useEffect(() => {
    void loadSessions(sessionStatus);
  }, [loadSessions, sessionStatus]);

  const pickSessionForCheckOut = (session: ParkingSessionDto) => {
    setOutboundPlate(session.plateDisplay);
    setOutboundSession(session);
    setOutboundLookupDone(true);
  };

  const closeScanner = () => setScanner(null);

  const handleScanResult = (value: string) => {
    if (!scanner) return;
    if (scanner.mode === 'qr' && scanner.target === 'inbound') {
      setInboundQrToken(value);
      setInboundMethod('Qr');
    } else if (scanner.mode === 'plate' && scanner.target === 'inbound') {
      setInboundPlate(value);
    } else if (scanner.mode === 'plate' && scanner.target === 'outbound') {
      setOutboundPlate(value);
      setOutboundSession(null);
      setOutboundLookupDone(false);
    }
    closeScanner();
  };

  const handleInboundCheckIn = async () => {
    if (!inboundPlate.trim()) {
      showAlert('Vui lòng nhập hoặc quét biển số xe trước khi mở Barie.', true);
      return;
    }
    if (inboundMethod === 'Qr' && !inboundQrToken.trim()) {
      showAlert('Check-in bằng QR cần quét mã QR vé đặt chỗ trước.', true);
      return;
    }
    if (inboundMethod === 'BookingCode' && !inboundBookingCode.trim()) {
      showAlert('Vui lòng nhập mã Booking.', true);
      return;
    }

    setInboundBusy(true);
    try {
      const session = await gateApi.checkIn({
        parkingLotId,
        method: inboundMethod,
        plate: inboundPlate,
        bookingCode: inboundMethod === 'BookingCode' ? inboundBookingCode : undefined,
        qrToken: inboundMethod === 'Qr' ? inboundQrToken : undefined,
        staffUserId,
      });
      setEvents((prev) => [toEvent(session, 'Check-in', inboundMethod), ...prev]);
      showAlert(`Làn vào: Đã xác thực xe ${session.plateDisplay} và kích hoạt nâng cần Barie!`);
      setInboundPlate('');
      setInboundBookingCode('');
      setInboundQrToken('');
      if (sessionStatus === 'Active') void loadSessions('Active');
    } catch (err) {
      showAlert(err instanceof Error ? err.message : 'Check-in thất bại.', true);
    } finally {
      setInboundBusy(false);
    }
  };

  const handleLookup = async () => {
    if (!outboundPlate.trim()) {
      showAlert('Vui lòng nhập hoặc quét biển số xe trước khi tra cứu.', true);
      return;
    }
    setOutboundBusy(true);
    try {
      const session = await gateApi.lookup(parkingLotId, outboundPlate);
      setOutboundSession(session);
      setOutboundLookupDone(true);
      if (!session) showAlert(`Xe ${outboundPlate.toUpperCase()} chưa check-in tại bãi này.`, true);
    } catch (err) {
      showAlert(err instanceof Error ? err.message : 'Tra cứu thất bại.', true);
    } finally {
      setOutboundBusy(false);
    }
  };

  const handleOutboundCheckOut = async () => {
    if (!outboundSession) return;
    setOutboundBusy(true);
    try {
      const session = await gateApi.checkOut({ parkingLotId, plate: outboundPlate, method: 'Manual', staffUserId });
      setEvents((prev) => [toEvent(session, 'Check-out', 'Manual'), ...prev]);
      showAlert(`Làn ra: Đã đối soát và mở cần Barie xuất bãi cho xe ${session.plateDisplay}!`);
      setOutboundPlate('');
      setOutboundSession(null);
      setOutboundLookupDone(false);
      if (sessionStatus === 'Active' || sessionStatus === 'Completed') void loadSessions(sessionStatus);
    } catch (err) {
      showAlert(err instanceof Error ? err.message : 'Check-out thất bại.', true);
    } finally {
      setOutboundBusy(false);
    }
  };

  return (
    <div className="max-w-7xl mx-auto px-4 sm:px-6 lg:px-8 py-8 space-y-6">
      {scanner && (
        <CameraScannerModal
          mode={scanner.mode}
          title={scanner.mode === 'qr' ? 'Quét mã QR vé đặt chỗ' : 'Quét biển số xe (OCR)'}
          onDetected={handleScanResult}
          onClose={closeScanner}
        />
      )}

      {alertNotice && (
        <div
          className={`fixed bottom-6 right-6 z-50 flex items-center gap-2 px-4 py-3 rounded-xl shadow-xl text-sm font-semibold ${
            alertNotice.isError ? 'bg-red-600 text-white' : 'bg-slate-900 text-white'
          }`}
        >
          <span className="material-symbols-outlined text-[20px]">
            {alertNotice.isError ? 'error' : 'check_circle'}
          </span>
          <span>{alertNotice.text}</span>
        </div>
      )}

      {/* Header */}
      <div className="flex flex-col md:flex-row md:items-center justify-between gap-4 pb-6 border-b border-slate-200">
        <div>
          <div className="inline-flex items-center gap-2 px-3 py-1 rounded-full bg-amber-100 text-amber-800 text-xs font-semibold uppercase tracking-wider mb-2">
            🚧 Bàn Điều Khiển Cổng & Barie (Gate Console)
          </div>
          <h1 className="text-2xl md:text-3xl font-extrabold text-slate-900 tracking-tight">
            Điều khiển Cổng Barie & Giám sát ANPR
          </h1>
          <p className="text-slate-500 text-sm mt-1">
            Quét mã QR đặt chỗ, chụp biển số bằng camera (OCR hỗ trợ, nhân viên xác nhận) và điều khiển Barie theo thời gian thực.
          </p>
        </div>
        <label className="flex items-center gap-2 text-xs font-semibold text-slate-600">
          Mã bãi xe
          <input
            type="number"
            min={1}
            value={parkingLotId}
            onChange={(e) => setParkingLotId(Number(e.target.value) || 1)}
            className="w-20 px-2 py-1.5 border border-slate-300 rounded-lg font-mono text-sm"
          />
        </label>
      </div>

      {/* 2-Column Console Layout */}
      <div className="grid grid-cols-1 lg:grid-cols-2 gap-6">
        {/* Left: Check-in */}
        <div className="bg-white p-6 rounded-2xl border border-slate-200 shadow-xs space-y-4">
          <h2 className="text-base font-bold text-slate-900 flex items-center gap-2">
            <span className="material-symbols-outlined text-blue-600">login</span>
            LÀN VÀO (INBOUND CHECK-IN)
          </h2>

          <div className="flex gap-2">
            {(['Manual', 'BookingCode', 'Qr'] as GateMethod[]).map((m) => (
              <button
                key={m}
                onClick={() => setInboundMethod(m)}
                className={`px-3 py-1.5 rounded-lg text-xs font-bold border transition-colors cursor-pointer ${
                  inboundMethod === m
                    ? 'bg-blue-600 text-white border-blue-600'
                    : 'bg-white text-slate-600 border-slate-300 hover:bg-slate-50'
                }`}
              >
                {m === 'Manual' ? 'Khách vãng lai' : m === 'BookingCode' ? 'Mã Booking' : 'Quét QR'}
              </button>
            ))}
          </div>

          {inboundMethod === 'BookingCode' && (
            <input
              type="text"
              value={inboundBookingCode}
              onChange={(e) => setInboundBookingCode(e.target.value)}
              placeholder="Nhập mã Booking (VD: BK-123456)..."
              className="w-full px-4 py-2.5 text-xs border border-slate-300 rounded-xl focus:outline-hidden focus:border-blue-500 font-mono uppercase"
            />
          )}

          {inboundMethod === 'Qr' && (
            <div className="flex gap-2">
              <input
                type="text"
                value={inboundQrToken}
                readOnly
                placeholder="Chưa quét mã QR..."
                className="flex-1 px-4 py-2.5 text-xs border border-slate-300 rounded-xl bg-slate-50 font-mono truncate"
              />
              <button
                onClick={() => setScanner({ mode: 'qr', target: 'inbound' })}
                className="px-4 py-2.5 bg-slate-800 text-white rounded-xl text-xs font-bold hover:bg-slate-900 shrink-0 flex items-center gap-1.5 cursor-pointer"
              >
                <span className="material-symbols-outlined text-[18px]">qr_code_scanner</span>
                QUÉT QR
              </button>
            </div>
          )}

          <div className="flex gap-2">
            <input
              type="text"
              value={inboundPlate}
              onChange={(e) => setInboundPlate(e.target.value)}
              placeholder="Nhập hoặc quét biển số (VD: 51H-123.45)..."
              className="flex-1 px-4 py-2.5 text-xs border border-slate-300 rounded-xl focus:outline-hidden focus:border-blue-500 font-mono uppercase"
            />
            <button
              onClick={() => setScanner({ mode: 'plate', target: 'inbound' })}
              className="px-4 py-2.5 bg-slate-100 text-slate-700 rounded-xl text-xs font-bold hover:bg-slate-200 shrink-0 flex items-center gap-1.5 cursor-pointer"
            >
              <span className="material-symbols-outlined text-[18px]">photo_camera</span>
              QUÉT
            </button>
          </div>

          <button
            onClick={handleInboundCheckIn}
            disabled={inboundBusy}
            className="w-full px-5 py-2.5 bg-emerald-600 text-white rounded-xl text-xs font-bold hover:bg-emerald-700 disabled:opacity-50 shadow-xs transition-colors flex items-center justify-center gap-1.5 cursor-pointer"
          >
            <span className="material-symbols-outlined text-[18px]">lock_open</span>
            {inboundBusy ? 'ĐANG XỬ LÝ…' : 'MỞ BARIE VÀO'}
          </button>
        </div>

        {/* Right: Check-out */}
        <div className="bg-white p-6 rounded-2xl border border-slate-200 shadow-xs space-y-4">
          <h2 className="text-base font-bold text-slate-900 flex items-center gap-2">
            <span className="material-symbols-outlined text-amber-600">logout</span>
            LÀN RA (OUTBOUND CHECK-OUT)
          </h2>

          <div className="flex gap-2">
            <input
              type="text"
              value={outboundPlate}
              onChange={(e) => {
                setOutboundPlate(e.target.value);
                setOutboundSession(null);
                setOutboundLookupDone(false);
              }}
              placeholder="Nhập hoặc quét biển số xe cần ra..."
              className="flex-1 px-4 py-2.5 text-xs border border-slate-300 rounded-xl focus:outline-hidden focus:border-amber-500 font-mono uppercase"
            />
            <button
              onClick={() => setScanner({ mode: 'plate', target: 'outbound' })}
              className="px-4 py-2.5 bg-slate-100 text-slate-700 rounded-xl text-xs font-bold hover:bg-slate-200 shrink-0 flex items-center gap-1.5 cursor-pointer"
            >
              <span className="material-symbols-outlined text-[18px]">photo_camera</span>
              QUÉT
            </button>
          </div>

          {outboundLookupDone && outboundSession && (
            <div className="bg-blue-50 border border-blue-200 rounded-xl px-4 py-3 text-xs text-blue-800 space-y-0.5">
              <p className="font-bold">{outboundSession.plateDisplay} đang đỗ trong bãi</p>
              <p>Vào lúc {new Date(outboundSession.entryAtUtc).toLocaleTimeString('vi-VN')} • Đã đỗ {outboundSession.minutesParked} phút</p>
              {outboundSession.slotCode && <p>Vị trí: {outboundSession.slotCode}</p>}
            </div>
          )}
          {outboundLookupDone && !outboundSession && (
            <div className="bg-red-50 border border-red-200 rounded-xl px-4 py-3 text-xs text-red-700">
              Xe chưa check-in tại bãi này.
            </div>
          )}

          {!outboundSession && (
            <button
              onClick={handleLookup}
              disabled={outboundBusy}
              className="w-full px-5 py-2.5 bg-slate-800 text-white rounded-xl text-xs font-bold hover:bg-slate-900 disabled:opacity-50 shadow-xs transition-colors flex items-center justify-center gap-1.5 cursor-pointer"
            >
              <span className="material-symbols-outlined text-[18px]">search</span>
              {outboundBusy ? 'ĐANG TRA CỨU…' : 'TRA CỨU XE TRONG BÃI'}
            </button>
          )}
          {outboundSession && (
            <button
              onClick={handleOutboundCheckOut}
              disabled={outboundBusy}
              className="w-full px-5 py-2.5 bg-amber-600 text-white rounded-xl text-xs font-bold hover:bg-amber-700 disabled:opacity-50 shadow-xs transition-colors flex items-center justify-center gap-1.5 cursor-pointer"
            >
              <span className="material-symbols-outlined text-[18px]">receipt_long</span>
              {outboundBusy ? 'ĐANG XỬ LÝ…' : 'MỞ BARIE RA'}
            </button>
          )}
        </div>
      </div>

      {/* Danh sách lượt xe trong bãi (UC-20) – GET /api/v1/parking-sessions */}
      <div className="p-6 bg-white rounded-2xl border border-slate-200 shadow-xs space-y-4">
        <div className="flex flex-col sm:flex-row sm:items-center justify-between gap-3">
          <div>
            <h3 className="font-bold text-slate-900 text-base flex items-center gap-2">
              <span className="material-symbols-outlined text-blue-600">local_parking</span>
              Danh sách Lượt xe trong Bãi
            </h3>
            <p className="text-xs text-slate-500 mt-0.5">Tra cứu nhanh xe đang đỗ, chọn 1 dòng để điền sẵn Check-out.</p>
          </div>
          <div className="flex items-center gap-2">
            {(['Active', 'Completed', 'Abnormal'] as ParkingSessionStatus[]).map((s) => (
              <button
                key={s}
                onClick={() => setSessionStatus(s)}
                className={`px-3 py-1.5 rounded-lg text-xs font-bold border transition-colors cursor-pointer ${
                  sessionStatus === s
                    ? 'bg-blue-600 text-white border-blue-600'
                    : 'bg-white text-slate-600 border-slate-300 hover:bg-slate-50'
                }`}
              >
                {SESSION_STATUS_LABEL[s]}
              </button>
            ))}
            <button
              onClick={() => void loadSessions(sessionStatus)}
              disabled={sessionLoading}
              className="px-3 py-1.5 rounded-lg text-xs font-bold text-slate-600 bg-slate-100 hover:bg-slate-200 disabled:opacity-50 cursor-pointer flex items-center gap-1"
            >
              <span className={`material-symbols-outlined text-[16px] ${sessionLoading ? 'animate-spin' : ''}`}>refresh</span>
              Tải lại
            </button>
          </div>
        </div>

        <div className="overflow-x-auto border border-slate-200 rounded-xl">
          <table className="w-full text-left text-xs">
            <thead className="bg-slate-50 border-b border-slate-200 text-slate-600 font-bold uppercase">
              <tr>
                <th className="px-4 py-3">Mã lượt</th>
                <th className="px-4 py-3">Biển số</th>
                <th className="px-4 py-3">Phương thức vào</th>
                <th className="px-4 py-3">Giờ vào</th>
                <th className="px-4 py-3">{sessionStatus === 'Active' ? 'Đã đỗ (phút)' : 'Giờ ra'}</th>
                <th className="px-4 py-3">Vị trí</th>
                {sessionStatus === 'Active' && <th className="px-4 py-3">Hành động</th>}
              </tr>
            </thead>
            <tbody className="divide-y divide-slate-100">
              {sessionList.length === 0 && (
                <tr>
                  <td colSpan={sessionStatus === 'Active' ? 7 : 6} className="px-4 py-6 text-center text-slate-400">
                    {sessionLoading ? 'Đang tải…' : 'Không có lượt xe nào ở trạng thái này.'}
                  </td>
                </tr>
              )}
              {sessionList.map((s) => (
                <tr key={s.id} className="hover:bg-slate-50 transition-colors">
                  <td className="px-4 py-3 font-mono text-slate-500">{s.code}</td>
                  <td className="px-4 py-3">
                    <span className="font-mono font-bold text-slate-900 bg-slate-100 px-2 py-0.5 rounded border border-slate-200">
                      {s.plateDisplay}
                    </span>
                  </td>
                  <td className="px-4 py-3 text-slate-700">{s.isWalkIn ? 'Vãng lai' : `Booking ${s.bookingCode ?? ''}`.trim()}</td>
                  <td className="px-4 py-3 font-mono text-slate-500">{new Date(s.entryAtUtc).toLocaleString('vi-VN')}</td>
                  <td className="px-4 py-3 font-mono text-slate-500">
                    {sessionStatus === 'Active'
                      ? s.minutesParked
                      : s.exitAtUtc
                        ? new Date(s.exitAtUtc).toLocaleString('vi-VN')
                        : '—'}
                  </td>
                  <td className="px-4 py-3 text-slate-700">{s.slotCode ?? '—'}</td>
                  {sessionStatus === 'Active' && (
                    <td className="px-4 py-3">
                      <button
                        onClick={() => pickSessionForCheckOut(s)}
                        className="px-2.5 py-1 rounded-md text-[10px] font-bold bg-amber-100 text-amber-800 hover:bg-amber-200 cursor-pointer"
                      >
                        Chọn Check-out
                      </button>
                    </td>
                  )}
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      </div>

      {/* Live Event Passage Feed */}
      <div className="p-6 bg-white rounded-2xl border border-slate-200 shadow-xs space-y-4">
        <div className="flex items-center justify-between">
          <div>
            <h3 className="font-bold text-slate-900 text-base flex items-center gap-2">
              <span className="material-symbols-outlined text-blue-600">history</span>
              Nhật ký Lượt xe Qua Cổng (phiên làm việc này)
            </h3>
            <p className="text-xs text-slate-500 mt-0.5">Ghi nhận các lượt check-in/check-out đã xử lý thành công tại bàn này.</p>
          </div>
        </div>

        <div className="overflow-x-auto border border-slate-200 rounded-xl">
          <table className="w-full text-left text-xs">
            <thead className="bg-slate-50 border-b border-slate-200 text-slate-600 font-bold uppercase">
              <tr>
                <th className="px-4 py-3">Mã lượt gửi xe</th>
                <th className="px-4 py-3">Biển số</th>
                <th className="px-4 py-3">Phân loại</th>
                <th className="px-4 py-3">Thời gian ghi nhận</th>
                <th className="px-4 py-3">Trạng thái Barie</th>
              </tr>
            </thead>
            <tbody className="divide-y divide-slate-100">
              {events.length === 0 && (
                <tr>
                  <td colSpan={5} className="px-4 py-6 text-center text-slate-400">
                    Chưa có lượt xe nào được ghi nhận trong phiên làm việc này.
                  </td>
                </tr>
              )}
              {events.map((ev, idx) => (
                <tr key={`${ev.id}-${idx}`} className="hover:bg-slate-50 transition-colors">
                  <td className="px-4 py-3 font-mono text-slate-500">{ev.id}</td>
                  <td className="px-4 py-3">
                    <span className="font-mono font-bold text-slate-900 bg-slate-100 px-2 py-0.5 rounded border border-slate-200">
                      {ev.plate}
                    </span>
                  </td>
                  <td className="px-4 py-3">
                    <span className={`px-2 py-0.5 rounded text-[10px] font-bold ${
                      ev.type === 'Check-in' ? 'bg-emerald-100 text-emerald-800' : 'bg-amber-100 text-amber-800'
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
