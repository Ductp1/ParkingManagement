import { useState } from 'react';
import type { ShiftView } from '@/api/shiftApi';
import { fullDate, ictDate, money, timeRange } from '@/features/shifts/format';
import { ReceiveHandoverModal } from '@/features/shifts/ReceiveHandoverModal';
import { Banner, Button, cardClass } from '@/features/shifts/ui';

interface Props {
  incoming: ShiftView[];
  /** Ca đang trực của người dùng; chưa Start thì chưa nhận được. */
  activeShiftId?: number;
  lotName: (id: number) => string;
  who: (userId: number) => string;
  onAccepted: () => void;
}

/** Figma Gate 12, 23, 28, 30–33. */
export function HandoverBanner({ incoming, activeShiftId, lotName, who, onAccepted }: Props) {
  const [receiving, setReceiving] = useState<ShiftView | null>(null);
  const [result, setResult] = useState<ShiftView | null>(null);
  const difference = result && result.cashReceived !== null && result.cashCounted !== null ? result.cashReceived - result.cashCounted : 0;

  return (
    <>
      {result && (
        <Banner tone={difference ? 'warning' : 'success'} title={difference ? 'Tiền mặt đã nhận bàn giao · Lệch chờ xử lý' : 'Đã nhận bàn giao'} onClose={() => setResult(null)}>
          Số bạn đã đếm: {money(result.cashReceived ?? 0)}.
          {difference ? ` Lệch bàn giao: ${money(difference)}. Nhân viên không có quyền xóa chênh lệch; chỉ Chủ bãi được xử lý.` : ' Số tiền này được tính vào tiền đầu ca của bạn.'}
        </Banner>
      )}
      {incoming.map((s) => (
        <section key={s.id} className={cardClass}>
          <h2 className="text-sm font-semibold text-[#10172a]">{who(s.staffUserId)} đang chờ bạn nhận bàn giao</h2>
          <p className="mt-1.5 text-xs text-[#65738c]">
            Ca {timeRange(s.scheduledStartUtc, s.scheduledEndUtc)} ngày {fullDate(ictDate(s.scheduledStartUtc))} · {lotName(s.parkingLotId)} ·{' '}
            {s.cashCounted === null ? 'Ca tự đóng, chưa đếm tiền' : `Người giao đếm ${money(s.cashCounted)}`}. Phải nhận bàn giao trước khi thu tiền hoặc kết ca.
          </p>
          {activeShiftId === s.handoverToShiftId
            ? <Button className="mt-3" onClick={() => setReceiving(s)}>Xem & nhận bàn giao</Button>
            : <p className="mt-3 text-xs font-semibold text-[#d97706]">Hãy bắt đầu ca của bạn trước khi nhận bàn giao.</p>}
        </section>
      ))}
      {receiving && (
        <ReceiveHandoverModal shift={receiving} subtitle={`${who(receiving.staffUserId)} → Bạn · ${lotName(receiving.parkingLotId)}`}
          onClose={() => setReceiving(null)}
          onAccepted={(updated) => { setReceiving(null); setResult(updated); onAccepted(); }} />
      )}
    </>
  );
}
