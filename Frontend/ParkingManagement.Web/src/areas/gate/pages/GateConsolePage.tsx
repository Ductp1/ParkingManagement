import React, { useCallback, useEffect, useState } from 'react';
import { Link } from 'react-router-dom';
import { gateApi, type CheckOutResultDto, type ExitFeeQuoteDto, type ShiftBookingItemDto } from '@/api/gateApi';
import { ownerStaffApi } from '@/api/ownerStaffApi';
import { parkingApi } from '@/api/parkingApi';
import { shiftApi } from '@/api/shiftApi';
import { CheckoutModal } from '@/areas/gate/components/CheckoutModal';
import { EndShiftModal } from '@/areas/gate/components/EndShiftModal';
import { HandoverBanner } from '@/areas/gate/components/HandoverBanner';
import { LaneCameraIdle, LaneCameraScanner } from '@/areas/gate/components/LaneCameraScanner';
import { MyShiftsPanel, type LotInfo } from '@/areas/gate/components/MyShiftsPanel';
import { dayLabel, errorCode, errorText, ictDate, ictTime, money, staffName, timeRange, tokenLotIds } from '@/features/shifts/format';
import { Banner, Button, LoadError, Page, Placeholder, cardClass, inputClass, useLoad } from '@/features/shifts/ui';
import { useAppSelector } from '@/store';

interface GateEvent {
  id: string;
  plate: string;
  lane: 'Làn vào #1' | 'Làn ra #2';
  type: 'Check-in' | 'Check-out';
  time: string;
  status: string;
  statusColor: string;
}

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

export const GateConsolePage: React.FC<{ view?: 'console' | 'shifts' }> = ({ view = 'console' }) => {
  const user = useAppSelector((s) => s.auth.user);
  const token = useAppSelector((s) => s.auth.token);
  const roles = user?.roles?.length ? user.roles : user ? [user.role] : [];
  const isOwner = roles.includes('Owner');
  const canHaveShift = isOwner || roles.includes('Staff');

  const mine = useLoad(`mine:${user?.id}`, async () => (canHaveShift ? shiftApi.mine() : { shifts: [], incomingHandovers: [] }));
  const ownerLots = useLoad(`ownerLots:${isOwner}`, async () => (isOwner ? ownerStaffApi.getOwnerLots() : []));
  const active = mine.data?.shifts.find((s) => s.status === 'Open');
  const lotIds = [...new Set([
    ...(active ? [active.parkingLotId] : []),
    ...(mine.data?.shifts.map((s) => s.parkingLotId) ?? []),
    ...tokenLotIds(token),
    ...(ownerLots.data?.map((l) => l.id) ?? []),
  ])];
  const lotDetails = useLoad(`lots:${lotIds.join()}`, async () => new Map(await Promise.all(lotIds.map(async (id) => {
    const info: LotInfo = await parkingApi.getById(id).then((l) => ({ name: l.name, status: l.status }), () => ({ name: `Bãi #${id}` }));
    return [id, info] as const;
  }))));
  const lot = (id: number): LotInfo => lotDetails.data?.get(id) ?? { name: `Bãi #${id}` };
  const [pickedLot, setPickedLot] = useState(0);
  // Đang trực thì khóa theo bãi của ca; chưa trực thì chọn trong các bãi được phép để xem.
  const lotId = active?.parkingLotId ?? (pickedLot || lotIds[0] || 0);
  const duty = useLoad(`duty:${active?.id ?? 0}`, async () => (active ? shiftApi.summary(active.id) : null));
  const canOperate = !!active;
  const who = (id: number) => (id === user?.id ? user.fullName : staffName(id, new Map()));
  const [ending, setEnding] = useState(false);
  const [now, setNow] = useState(() => Date.now());
  useEffect(() => {
    const timer = setInterval(() => setNow(Date.now()), 30_000);
    return () => clearInterval(timer);
  }, []);

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

  /** Lỗi thao tác cổng: hiện tiếng Việt; ca không còn mở (bị đóng ở nơi khác) thì tải lại ca. */
  const fail = (e: unknown) => {
    setError(errorText(e));
    if (errorCode(e) === 'SHIFT_NOT_ACTIVE') mine.reload();
  };

  // UC-20 (US-076): booking trong ca trực của bãi được gán.
  const loadBookings = useCallback(async () => {
    if (!lotId) return;
    try {
      const data = await gateApi.shiftBookings({ parkingLotId: lotId, search: search.trim() || undefined });
      setBookings(data);
    } catch (e) {
      setError(errorText(e));
    }
  }, [lotId, search]);

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
      const qrToken = inboundQrToken.trim() || undefined;
      const session = await gateApi.checkIn({
        parkingLotId: lotId,
        method: qrToken ? 'Qr' : bookingCode ? 'BookingCode' : 'Manual',
        plate,
        bookingCode,
        qrToken,
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
      duty.reload();
    } catch (e) {
      fail(e);
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
      setQuote(await gateApi.exitQuote(lotId, plate));
    } catch (e) {
      fail(e);
    } finally {
      setBusy(false);
    }
  };

  const handleCheckedOut = (result: CheckOutResultDto) => {
    const due = result.fee?.additionalDue ?? 0;
    pushEvent(result.session.plateDisplay, 'Làn ra #2', 'Check-out',
      due > 0 ? `Đã thu ${money(due)}` : 'Không thu thêm',
      due > 0 ? 'bg-blue-50 text-blue-700 border-blue-200' : 'bg-emerald-50 text-emerald-700 border-emerald-200');
    showAlert(due > 0
      ? `Đã thu ${money(due)} · Xe ${result.session.plateDisplay} đã check-out.`
      : `Xe ${result.session.plateDisplay} đã check-out, không còn phí phải thu.`);
    if (result.warning) setError(result.warning);
    setQuote(null);
    setOutboundPlate('');
    void loadBookings();
    duty.reload();
  };

  const subtitle = `${lotId ? `${lot(lotId).name} · ` : ''}${dayLabel(ictDate(now))} · ${ictTime(now)} ICT`;

  if (!canHaveShift) {
    return (
      <Page title="Trạm vận hành cổng" subtitle={subtitle}>
        <Banner tone="info" title="Admin chỉ xem">
          Vận hành cổng dành cho nhân viên và chủ bãi đang có ca. Xem lượt xe và ca trực tại{' '}
          <Link to="/admin/operations/gate-activity" className="font-semibold text-[#2c5cf2] underline">Admin Portal › Gate Activity</Link>.
        </Banner>
      </Page>
    );
  }
  if (mine.error != null && !mine.data) {
    return <Page title="Trạm vận hành cổng" subtitle={subtitle}><LoadError title="Không tải được ca trực" error={mine.error} onRetry={mine.reload} /></Page>;
  }
  if (!mine.data) {
    return <Page title="Trạm vận hành cổng" subtitle={subtitle}><Placeholder kind="loading" title="Đang tải ca trực" /></Page>;
  }

  const handovers = (
    <HandoverBanner incoming={mine.data.incomingHandovers} activeShiftId={active?.id} lotName={(id) => lot(id).name} who={who}
      onAccepted={() => { mine.reload(); duty.reload(); }} />
  );

  if (view === 'shifts') {
    return (
      <Page title="Ca của tôi" subtitle={`${user?.fullName ?? ''} · ${dayLabel(ictDate(now))} · ${ictTime(now)} ICT`}>
        {handovers}
        <MyShiftsPanel shifts={mine.data.shifts} now={now} fullName={user?.fullName ?? ''} isOwner={isOwner} lot={lot} onStarted={mine.reload} />
      </Page>
    );
  }

  return (
    <div className="max-w-7xl mx-auto px-4 sm:px-6 lg:px-8 py-8 space-y-6">
      {/* Toast Notice */}
      {alertNotice && (
        <div role="status" className="fixed bottom-6 right-6 z-50 flex items-center gap-2 px-4 py-3 bg-slate-900 text-white rounded-xl shadow-xl text-sm font-semibold">
          <span className="material-symbols-outlined text-emerald-400 text-[20px]">check_circle</span>
          <span>{alertNotice}</span>
        </div>
      )}

      <header>
        <h1 className="text-[28px] sm:text-[32px] font-bold text-[#10172a]">Trạm vận hành cổng</h1>
        <p className="mt-2 text-sm text-[#65738c]">{subtitle}</p>
      </header>

      {error && <Banner tone="danger" title="Chưa thực hiện được" onClose={() => setError(null)}>{error}</Banner>}

      {active ? (
        <section className={`${cardClass} flex flex-wrap items-center justify-between gap-4`}>
          <div className="text-[13px] text-[#65738c]">
            <p className="font-bold text-[#15803d]">ĐANG TRỰC · {user?.fullName}</p>
            <p className="mt-1">
              Ca {timeRange(active.scheduledStartUtc, active.scheduledEndUtc)}
              {active.startedAtUtc ? ` · Bắt đầu thực tế ${ictTime(active.startedAtUtc)}` : ''}{active.isCashHolder ? ' · Giữ két' : ' · Không giữ két'}
            </p>
            {duty.data && <p className="mt-1">{duty.data.vehiclesInLot} xe đang trong bãi · {duty.data.entries} xe vào · {duty.data.exits} xe ra</p>}
          </div>
          <div className="flex gap-2.5">
            <Link to="/gate/shifts" className="inline-flex min-h-11 items-center rounded-[10px] border border-[#e3e8f0] px-4 text-sm font-semibold text-[#10172a] hover:bg-[#f2f5f9]">Ca của tôi</Link>
            <Button onClick={() => setEnding(true)}>Kết thúc ca</Button>
          </div>
        </section>
      ) : (
        <>
          <Banner tone="warning" title="Chưa có ca đang hoạt động · Chỉ xem">
            Bạn có thể xem booking và lượt xe. <Link to="/gate/shifts" className="font-semibold text-[#2c5cf2] underline">Bắt đầu ca</Link> để check-in/out, thu tiền và vận hành cổng.
          </Banner>
          {lotIds.length > 1 && (
            <label className="flex max-w-md flex-col gap-1.5 text-[13px] font-semibold">
              Bãi đang xem
              <select className={inputClass} value={lotId} onChange={(e) => { setPickedLot(Number(e.target.value)); setBookings([]); }}>
                {lotIds.map((id) => <option key={id} value={id}>{lot(id).name}</option>)}
              </select>
            </label>
          )}
        </>
      )}
      {active && lot(lotId).status && lot(lotId).status !== 'Active' && (
        <Banner tone="danger" title="Bãi hiện không hoạt động">Không nhận xe vào. Vẫn cho xe ra, thu tiền và bàn giao như bình thường.</Banner>
      )}
      {handovers}

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
                          disabled={!canOperate}
                          onClick={() => { setOutboundPlate(b.plateDisplay); setQuote(null); }}
                          className="px-3 py-1.5 text-[10px] font-bold text-amber-700 bg-amber-50 border border-amber-200 rounded-lg hover:bg-amber-100 cursor-pointer disabled:cursor-not-allowed disabled:opacity-45"
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

      {/* 2-Column Console Layout – fieldset khóa toàn bộ thao tác ghi khi chưa có ca (API cũng trả 409 SHIFT_NOT_ACTIVE). */}
      <fieldset disabled={!canOperate} aria-label="Làn vào và làn ra" className="grid min-w-0 grid-cols-1 lg:grid-cols-2 gap-6 disabled:opacity-55">
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

        </div>
      </fieldset>

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
          <span className="text-xs font-semibold text-slate-400 font-mono">{lotId ? lot(lotId).name : ''}</span>
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

      {quote && active && (
        <CheckoutModal quote={quote} plate={outboundPlate.trim()} shift={active} operator={user?.fullName ?? ''}
          onClose={() => setQuote(null)} onDone={handleCheckedOut}
          onError={(e) => { if (errorCode(e) === 'SHIFT_NOT_ACTIVE') mine.reload(); }} />
      )}
      {ending && active && (
        <EndShiftModal shift={active} subtitle={`${user?.fullName ?? ''} · ${lot(active.parkingLotId).name}`} who={who} onClose={() => setEnding(false)}
          onEnded={() => { setEnding(false); showAlert('Đã kết thúc ca.'); mine.reload(); }} />
      )}
    </div>
  );
};
