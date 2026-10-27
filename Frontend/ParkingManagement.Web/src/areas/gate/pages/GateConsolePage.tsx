import React, { useCallback, useEffect, useState } from 'react';
import { gateApi, type ExitFeeQuoteDto, type ShiftBookingItemDto } from '@/api/gateApi';

interface GateEvent {
  id: string;
  plate: string;
  lane: 'Làn vào #1' | 'Làn ra #2';
  type: 'Check-in' | 'Check-out';
  time: string;
  status: string;
  statusColor: string;
}

const LOTS = [
  { id: 1, name: 'Bãi xe Vincom Đồng Khởi' },
  { id: 2, name: 'Bãi xe Sân bay Tân Sơn Nhất (P2)' },
];

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
  const [outboundPlate, setOutboundPlate] = useState('');
  const [quote, setQuote] = useState<ExitFeeQuoteDto | null>(null);
  const [events, setEvents] = useState<GateEvent[]>([]);
  const [alertNotice, setAlertNotice] = useState<string | null>(null);
  const [error, setError] = useState<string | null>(null);
  const [busy, setBusy] = useState(false);

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
      const session = await gateApi.checkIn({
        parkingLotId: lotId,
        method: bookingCode ? 'BookingCode' : 'Manual',
        plate,
        bookingCode,
        staffUserId: Number(staffUserId) || undefined,
      });
      pushEvent(session.plateDisplay, 'Làn vào #1', 'Check-in',
        bookingCode ? `Booking ${bookingCode}` : 'Vãng lai',
        bookingCode ? 'bg-emerald-50 text-emerald-700 border-emerald-200' : 'bg-slate-50 text-slate-600 border-slate-200');
      showAlert(`Làn vào #1: Đã check-in xe ${session.plateDisplay}${bookingCode ? ` (booking ${bookingCode})` : ''} và nâng cần Barie!`);
      setInboundPlate('');
      setInboundBookingCode('');
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
                      {b.status !== 'NoShow' && (
                        <button
                          onClick={() => { setOutboundPlate(b.plateDisplay); setQuote(null); }}
                          className="px-3 py-1.5 text-[10px] font-bold text-amber-700 bg-amber-50 border border-amber-200 rounded-lg hover:bg-amber-100 cursor-pointer"
                        >
                          CHỐT RA
                        </button>
                      )}
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

          <div className="h-48 bg-slate-900 rounded-xl flex flex-col items-center justify-center text-slate-400 relative overflow-hidden border border-slate-800">
            <span className="material-symbols-outlined text-[48px] mb-2 text-slate-500">videocam</span>
            <span className="text-xs font-medium">Khung hình Camera nhận diện ANPR (Optical Video Feed)</span>
            <div className="absolute top-2 left-2 px-2 py-0.5 rounded bg-red-600 text-[10px] text-white font-bold tracking-wider">
              LIVE REC
            </div>
            <div className="absolute bottom-2 right-2 text-[10px] text-slate-400 font-mono">
              FPS: 30 • 1080p
            </div>
          </div>

          <div className="flex gap-2">
            <input
              type="text"
              value={inboundPlate}
              onChange={(e) => setInboundPlate(e.target.value)}
              placeholder="Nhập biển số hoặc quét QR (VD: 51F-123.45)..."
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

          <div className="h-48 bg-slate-900 rounded-xl flex flex-col items-center justify-center text-slate-400 relative overflow-hidden border border-slate-800">
            <span className="material-symbols-outlined text-[48px] mb-2 text-slate-500">videocam</span>
            <span className="text-xs font-medium">Khung hình Camera nhận diện ANPR (Optical Video Feed)</span>
            <div className="absolute top-2 left-2 px-2 py-0.5 rounded bg-red-600 text-[10px] text-white font-bold tracking-wider">
              LIVE REC
            </div>
            <div className="absolute bottom-2 right-2 text-[10px] text-slate-400 font-mono">
              FPS: 30 • 1080p
            </div>
          </div>

          <div className="flex gap-2">
            <input
              type="text"
              value={outboundPlate}
              onChange={(e) => { setOutboundPlate(e.target.value); setQuote(null); }}
              placeholder="Nhập biển số hoặc quét mã vé xuất..."
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
