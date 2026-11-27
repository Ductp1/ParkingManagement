import { useState } from 'react';
import { gateApi, type CheckOutResultDto, type ExitFeeQuoteDto } from '@/api/gateApi';
import type { ShiftView } from '@/api/shiftApi';
import { errorCode, errorStatus, errorText, ictTime, money, timeRange } from '@/features/shifts/format';
import { Banner, Button, Facts, Modal, ModalActions } from '@/features/shifts/ui';

interface Props {
  quote: ExitFeeQuoteDto;
  plate: string;
  shift: ShiftView;
  operator: string;
  onClose: () => void;
  onDone: (result: CheckOutResultDto) => void;
  onError: (error: unknown) => void;
}

/** Figma Gate 24–29: thu tiền mặt rồi cho xe ra. Chỉ báo "đã thu" khi API xác nhận. */
export function CheckoutModal({ quote, plate, shift, operator, onClose, onDone, onError }: Props) {
  const [busy, setBusy] = useState(false);
  const [failure, setFailure] = useState<unknown>(null);
  const due = quote.additionalDue;
  const notHolder = due > 0 && !shift.isCashHolder;
  const exists = errorCode(failure) === 'GATE_PAYMENT_EXISTS';
  const unavailable = errorStatus(failure) === 503;

  async function confirm() {
    setBusy(true);
    setFailure(null);
    try {
      onDone(await gateApi.checkOut({ parkingLotId: shift.parkingLotId, plate, method: 'Manual', confirmFee: true }));
    } catch (error) {
      setFailure(error);
      setBusy(false);
      onError(error);
    }
  }

  return (
    <Modal title="Thanh toán tại cổng ra" subtitle={`Phiên gửi xe ${quote.sessionCode} · ${quote.plateDisplay}`} busy={busy} onClose={onClose}>
      <Facts items={[
        ['Vào lúc', `${ictTime(quote.entryAtUtc)} · ${quote.minutesParked} phút${quote.bookingCode ? ` · ${quote.bookingCode}` : ''}`],
        ['Tổng phí phiên', money(quote.sessionFee)],
        ['Đã trả trước', money(quote.prepaidAmount)],
        ...(quote.overstayMinutes > 0 ? ([[`Quá giờ ${quote.overstayMinutes} phút`, money(quote.overstayAmount)]] as Array<[string, string]>) : []),
        ['Cần thu thêm (tiền mặt)', <span className="text-[#d97706]">{money(due)}</span>],
        ['Người thao tác / Ca', `${operator} · ${timeRange(shift.scheduledStartUtc, shift.scheduledEndUtc)}`],
      ]} />
      {quote.lines.length > 0 && (
        <ul className="flex flex-col gap-1 text-xs text-[#65738c]">
          {quote.lines.map((line, i) => <li key={i} className="flex justify-between gap-3"><span>{line.description}</span><span className="font-mono">{money(line.amount)}</span></li>)}
        </ul>
      )}
      {quote.isWithinBillingGrace && <Banner tone="success" title="Không còn phí phải thu">Xe ra trong thời gian ân hạn.</Banner>}
      {notHolder && <Banner tone="warning" title="Bạn không giữ két">Tiền mặt của ca này do người giữ két thu. Chuyển xe cho người giữ két để hoàn tất.</Banner>}
      {failure != null && (
        <Banner tone="danger" title={exists ? 'Đã có khoản thanh toán khác cho lượt xe' : unavailable ? 'Dịch vụ thanh toán chưa phản hồi' : 'Chưa cho xe ra được'}>
          {errorText(failure)}{unavailable && ' Chưa xác nhận thu tiền. Giữ xe ở trạng thái đang gửi; báo giá được giữ để kiểm tra khi thử lại.'}
        </Banner>
      )}
      <ModalActions>
        <Button variant="secondary" disabled={busy} onClick={onClose}>{exists ? 'Đóng để kiểm tra' : 'Hủy'}</Button>
        {!exists && !notHolder && (
          <Button busy={busy} onClick={() => void confirm()}>
            {unavailable ? 'Thử lại thanh toán' : due > 0 ? `Đã thu ${money(due)} · Mở barie ra` : 'Mở barie ra'}
          </Button>
        )}
      </ModalActions>
    </Modal>
  );
}
