import React from 'react';

export interface BookingItem {
  id: string;
  code: string;
  plate: string;
  carModel: string;
  location: string;
  spot: string;
  timeRange: string;
  dateLabel: string;
  services: string[];
  totalPrice: number;
  paymentMethod: string;
  status: 'upcoming' | 'completed' | 'cancelled';
  statusText: string;
  rating?: number;
  reviewed?: boolean;
  notes?: string;
}

export interface BookingTableRowProps {
  booking: BookingItem;
  isHighlighted: boolean;
  onViewQr: (b: BookingItem) => void;
  onExtend: (b: BookingItem) => void;
  onEdit: (b: BookingItem) => void;
  onCancel: (b: BookingItem) => void;
  onInvoice: (b: BookingItem) => void;
  onFeedback: (b: BookingItem) => void;
  onRefundDetail: (b: BookingItem) => void;
}

/** Chuẩn hóa hiển thị thời gian theo từng trạng thái để không làm vỡ layout */
const parseTimeDisplay = (status: BookingItem['status'], raw: string) => {
  if (!raw) return { line1: '--:--', line2: '', isCancel: false };

  if (status === 'cancelled') {
    if (raw.includes('Hủy lúc')) {
      const parts = raw.split('·').map((s) => s.trim());
      return {
        line1: parts[0] || 'Đã hủy',
        line2: parts[1] ? `Lý do: ${parts[1]}` : '',
        isCancel: true,
      };
    }
    return { line1: raw, line2: 'Đã hủy', isCancel: true };
  }

  const holdMatch = raw.match(/Đến lúc\s*([0-9:]+)\)\s*-\s*([0-9:]+)/i);
  if (holdMatch) {
    return {
      line1: `${holdMatch[1]} – ${holdMatch[2]}`,
      line2: 'Giữ chỗ 15p',
      isCancel: false,
    };
  }

  return {
    line1: raw.replace(/-/g, '–'),
    line2: '',
    isCancel: false,
  };
};

/** Tối ưu nhãn dịch vụ để không chiếm chiều ngang quá lớn */
const formatServiceLabel = (srv: string) => {
  if (srv.includes('Bảo hiểm')) return 'Bảo hiểm Care';
  if (srv.includes('tiêu chuẩn') && srv.includes('Đỗ')) return 'Đỗ tiêu chuẩn';
  return srv;
};

export const BookingTableRow: React.FC<BookingTableRowProps> = ({
  booking: b,
  isHighlighted,
  onViewQr,
  onExtend,
  onEdit,
  onCancel,
  onInvoice,
  onFeedback,
  onRefundDetail,
}) => {
  const isUpcoming = b.status === 'upcoming';
  const isCompleted = b.status === 'completed';
  const isCancelled = b.status === 'cancelled';

  const timeInfo = parseTimeDisplay(b.status, b.timeRange);

  const cellBaseClass = isHighlighted
    ? 'bg-blue-50/75 border-y-2 border-blue-500'
    : `border-b border-slate-100 ${
        isUpcoming
          ? 'bg-blue-50/20 group-hover:bg-blue-50/40'
          : 'bg-white group-hover:bg-slate-50/80'
      }`;

  return (
    <tr
      id={`booking-row-${b.id}`}
      className="transition-colors group"
    >
      {/* CỘT 1: Mã đặt chỗ & Phương tiện (15%) */}
      <td
        className={`py-3 px-3 relative transition-colors ${cellBaseClass} ${
          isHighlighted ? 'border-l-2 border-blue-500 rounded-l-xl' : ''
        }`}
      >
        {isUpcoming && (
          <div
            className={`absolute top-2.5 bottom-2.5 w-1 bg-blue-600 rounded-full ${
              isHighlighted ? 'left-1.5' : 'left-0.5'
            }`}
          />
        )}
        <div className="flex flex-col gap-1">
          <div className="flex items-center gap-1">
            <span
              className={`font-mono font-bold text-[10.5px] px-1.5 py-0.2 rounded border whitespace-nowrap ${
                isUpcoming
                  ? 'text-blue-700 bg-blue-50 border-blue-200'
                  : isCancelled
                  ? 'text-slate-400 bg-slate-50 border-slate-200 line-through'
                  : 'text-slate-700 bg-slate-50 border-slate-200'
              }`}
            >
              {b.code}
            </span>
            {isUpcoming && (
              <span className="inline-flex items-center gap-0.5 px-1 py-0.2 rounded bg-slate-100 text-slate-600 text-[9.5px] font-bold whitespace-nowrap">
                <span className="material-symbols-outlined text-[10px]">camera</span>
                ANPR
              </span>
            )}
          </div>
          <div className="flex items-center gap-1 text-slate-600 text-[11px] whitespace-nowrap overflow-hidden">
            <span className="material-symbols-outlined text-[13px] text-slate-400 shrink-0">
              directions_car
            </span>
            <span className="font-bold text-slate-900">{b.plate}</span>
            <span className="text-slate-300">·</span>
            <span
              className="text-slate-500 text-[10px] truncate max-w-[70px]"
              title={b.carModel}
            >
              {b.carModel}
            </span>
          </div>
        </div>
      </td>

      {/* CỘT 2: Bãi đỗ & Vị trí đỗ (17%) */}
      <td className={`py-3 px-3 transition-colors ${cellBaseClass}`}>
        <div className="flex flex-col gap-0.5">
          <span
            className="font-bold text-slate-900 text-[11.5px] hover:text-blue-600 transition-colors cursor-pointer truncate max-w-[160px] block"
            title={b.location}
          >
            {b.location}
          </span>
          <span
            className={`inline-flex items-center gap-1 text-[10.5px] font-bold px-1.5 py-0.2 rounded-md w-fit whitespace-nowrap ${
              isCancelled
                ? 'text-slate-500 bg-slate-100 border border-slate-200'
                : 'text-emerald-800 bg-emerald-50 border border-emerald-200/80'
            }`}
          >
            <span
              className={`w-1.5 h-1.5 rounded-full shrink-0 ${
                isCancelled ? 'bg-slate-400' : 'bg-emerald-500'
              }`}
            />
            <span className="truncate max-w-[130px]">{b.spot}</span>
          </span>
        </div>
      </td>

      {/* CỘT 3: Thời gian (14%) */}
      <td className={`py-3 px-3 transition-colors ${cellBaseClass}`}>
        <div className="flex flex-col gap-0.5">
          <div
            className={`flex items-center gap-1 font-bold text-[11.5px] ${
              timeInfo.isCancel ? 'text-rose-700' : 'text-slate-900'
            }`}
          >
            <span
              className={`material-symbols-outlined text-[12px] shrink-0 ${
                timeInfo.isCancel ? 'text-rose-500' : 'text-blue-600'
              }`}
            >
              {timeInfo.isCancel ? 'event_busy' : 'schedule'}
            </span>
            <span className="truncate max-w-[130px]">{timeInfo.line1}</span>
          </div>

          <div className="flex items-center gap-1 text-[10px] flex-wrap">
            <span className="text-slate-400 font-medium whitespace-nowrap">
              {b.dateLabel}
            </span>
            {timeInfo.line2 && (
              <span
                className={`text-[9px] font-bold px-1 py-0.2 rounded shrink-0 truncate max-w-[90px] ${
                  timeInfo.isCancel
                    ? 'text-slate-500 bg-slate-100 border border-slate-200'
                    : 'text-amber-700 bg-amber-50 border border-amber-200/80'
                }`}
                title={timeInfo.line2}
              >
                {timeInfo.line2}
              </span>
            )}
          </div>
        </div>
      </td>

      {/* CỘT 4: Dịch vụ (16%) */}
      <td className={`py-3 px-3 transition-colors ${cellBaseClass}`}>
        <div className="flex flex-col gap-1 max-w-[150px]">
          {b.services.map((srv, idx) => {
            const isEv = srv.includes('EV');
            const isExt = srv.includes('Gia hạn');
            const label = formatServiceLabel(srv);
            return (
              <span
                key={idx}
                className={`inline-flex items-center gap-1 px-1.5 py-0.2 rounded text-[10px] font-medium border truncate w-fit ${
                  isEv
                    ? 'bg-amber-50 text-amber-800 border-amber-200/80'
                    : isExt
                    ? 'bg-indigo-50 text-indigo-700 border-indigo-200/80'
                    : 'bg-slate-50 text-slate-600 border-slate-200/60'
                }`}
                title={srv}
              >
                {isEv ? (
                  <span className="material-symbols-outlined text-[10px] text-amber-600 shrink-0">
                    bolt
                  </span>
                ) : isExt ? (
                  <span className="material-symbols-outlined text-[10px] text-indigo-600 shrink-0">
                    more_time
                  </span>
                ) : (
                  <span className="material-symbols-outlined text-[10px] text-blue-500 shrink-0">
                    check
                  </span>
                )}
                <span className="truncate">{label}</span>
              </span>
            );
          })}
        </div>
      </td>

      {/* CỘT 5: Tổng tiền (11%) */}
      <td className={`py-3 px-3 transition-colors ${cellBaseClass}`}>
        <div className="flex flex-col gap-0.5">
          <span
            className={`font-extrabold text-[11.5px] ${
              isCancelled ? 'text-slate-500' : 'text-slate-900'
            }`}
          >
            {b.totalPrice > 0 ? `${b.totalPrice.toLocaleString('vi-VN')} đ` : '0 đ'}
          </span>
          <span
            className={`text-[10px] leading-tight break-words max-w-[105px] ${
              isCancelled ? 'text-emerald-700 font-semibold' : 'text-slate-500'
            }`}
            title={b.paymentMethod}
          >
            {b.paymentMethod.replace('Ví ParkMaster', 'Ví PKM').replace('về tài khoản gốc', 'về thẻ gốc')}
          </span>
        </div>
      </td>

      {/* CỘT 6: Trạng thái (11%) */}
      <td className={`py-3 px-2 text-center transition-colors ${cellBaseClass} whitespace-nowrap overflow-hidden`}>
        {isUpcoming && (
          <span className="inline-flex items-center gap-1 px-2 py-0.5 rounded-full bg-blue-50 text-blue-700 border border-blue-200 text-[10px] font-bold whitespace-nowrap">
            <span className="w-1.5 h-1.5 rounded-full bg-blue-600 animate-pulse shrink-0" />
            <span>Sắp tới</span>
          </span>
        )}
        {isCompleted && (
          <span className="inline-flex items-center gap-1 px-2 py-0.5 rounded-full bg-emerald-50 text-emerald-800 border border-emerald-200 text-[10px] font-bold whitespace-nowrap">
            <span className="w-1.5 h-1.5 rounded-full bg-emerald-600 shrink-0" />
            <span>Đã đỗ</span>
          </span>
        )}
        {isCancelled && (
          <span className="inline-flex items-center gap-1 px-2 py-0.5 rounded-full bg-rose-50 text-rose-700 border border-rose-200 text-[10px] font-bold whitespace-nowrap">
            <span className="w-1.5 h-1.5 rounded-full bg-rose-600 shrink-0" />
            <span>Đã hủy</span>
          </span>
        )}
      </td>

      {/* CỘT 7: Thao tác (16%) */}
      <td
        className={`py-3 px-3 text-right transition-colors ${cellBaseClass} ${
          isHighlighted ? 'border-r-2 border-blue-500 rounded-r-xl' : ''
        }`}
      >
        <div className="flex items-center justify-end gap-1 whitespace-nowrap">
          {isUpcoming ? (
            <>
              {/* Nút chính: Vé QR */}
              <button
                type="button"
                onClick={() => onViewQr(b)}
                title="Xem vé QR Check-in Barie"
                className="inline-flex items-center gap-0.5 px-2 py-1.5 rounded-lg bg-blue-600 hover:bg-blue-700 text-white text-[11px] font-bold shadow-xs transition-all cursor-pointer shrink-0"
              >
                <span className="material-symbols-outlined text-[13px]">qr_code_2</span>
                <span>Vé QR</span>
              </button>

              {/* Nút Gia hạn giờ đỗ (Icon button tinh gọn) */}
              <button
                type="button"
                onClick={() => onExtend(b)}
                title="Gia hạn thêm giờ đỗ"
                className="p-1.5 rounded-lg border border-indigo-200 bg-indigo-50 hover:bg-indigo-100 text-indigo-700 transition-colors cursor-pointer shrink-0"
              >
                <span className="material-symbols-outlined text-[14px]">more_time</span>
              </button>

              {/* Sửa thông tin (Icon button) */}
              <button
                type="button"
                onClick={() => onEdit(b)}
                title="Sửa thông tin đặt chỗ (Xe/Giờ)"
                className="p-1.5 rounded-lg border border-slate-200 bg-white hover:bg-slate-50 text-slate-600 hover:text-blue-600 transition-colors cursor-pointer shrink-0"
              >
                <span className="material-symbols-outlined text-[14px]">edit_calendar</span>
              </button>

              {/* Hủy đặt chỗ (Icon button) */}
              <button
                type="button"
                onClick={() => onCancel(b)}
                title="Hủy giữ chỗ (Chính sách hoàn tiền)"
                className="p-1.5 rounded-lg border border-slate-200 bg-white hover:bg-rose-50 text-slate-400 hover:text-rose-600 transition-colors cursor-pointer shrink-0"
              >
                <span className="material-symbols-outlined text-[14px]">close</span>
              </button>
            </>
          ) : isCompleted ? (
            <>
              {/* Hóa đơn điện tử VAT */}
              <button
                type="button"
                onClick={() => onInvoice(b)}
                title="Xem & Tải hóa đơn VAT"
                className="inline-flex items-center gap-0.5 px-2 py-1.5 rounded-lg border border-slate-200 bg-white hover:bg-slate-50 text-slate-700 text-[11px] font-semibold transition-colors cursor-pointer shrink-0"
              >
                <span className="material-symbols-outlined text-[13px] text-blue-600">
                  receipt_long
                </span>
                <span>Hóa đơn</span>
              </button>

              {b.reviewed ? (
                <span className="inline-flex items-center gap-0.5 px-1.5 py-1 rounded-lg bg-amber-50 text-amber-700 font-bold text-[10px] border border-amber-200 shrink-0">
                  <span className="material-symbols-outlined text-[12px] text-amber-500">
                    star
                  </span>
                  <span>{b.rating}★</span>
                </span>
              ) : (
                <button
                  type="button"
                  onClick={() => onFeedback(b)}
                  className="inline-flex items-center gap-0.5 px-2 py-1.5 rounded-lg border border-amber-300 bg-amber-50 hover:bg-amber-100 text-amber-800 font-bold text-[11px] transition-all cursor-pointer shrink-0"
                >
                  <span className="material-symbols-outlined text-[13px] text-amber-500">
                    star
                  </span>
                  <span>Đánh giá</span>
                </button>
              )}

              <button
                type="button"
                onClick={() => onViewQr(b)}
                className="p-1.5 rounded-lg border border-slate-200 bg-white hover:bg-slate-100 text-slate-600 transition-colors cursor-pointer shrink-0"
                title="Chi tiết vé"
              >
                <span className="material-symbols-outlined text-[14px]">visibility</span>
              </button>
            </>
          ) : (
            <button
              type="button"
              onClick={() => onRefundDetail(b)}
              className="px-2.5 py-1.5 rounded-lg border border-slate-200 bg-white hover:bg-slate-50 text-slate-700 text-[11px] font-semibold cursor-pointer transition-colors shadow-2xs shrink-0"
            >
              Chi tiết hoàn
            </button>
          )}
        </div>
      </td>
    </tr>
  );
};
