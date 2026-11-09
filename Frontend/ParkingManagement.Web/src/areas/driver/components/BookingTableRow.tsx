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
    // raw dạng: "Hủy lúc 15:57 · Thay đổi lịch trình" hoặc "06/11/2026"
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

  // upcoming / completed: "Giữ chỗ 15 phút (Đến lúc 16:11) - 19:00" hoặc "15:00 - 18:00"
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
      {/* CỘT 1: Mã đặt chỗ & Phương tiện */}
      <td
        className={`py-3.5 px-4 relative transition-colors ${cellBaseClass} ${
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
        <div className="flex flex-col gap-1.5">
          <div className="flex items-center gap-1.5">
            <span
              className={`font-mono font-bold text-[11px] px-2 py-0.5 rounded border whitespace-nowrap ${
                isUpcoming
                  ? 'text-blue-700 bg-blue-50/90 border-blue-200'
                  : isCancelled
                  ? 'text-slate-400 bg-slate-50 border-slate-200 line-through'
                  : 'text-slate-700 bg-slate-50 border-slate-200'
              }`}
            >
              {b.code}
            </span>
            {isUpcoming && (
              <span className="inline-flex items-center gap-0.5 px-1.5 py-0.5 rounded bg-slate-100 text-slate-600 text-[10px] font-bold whitespace-nowrap">
                <span className="material-symbols-outlined text-[12px]">camera</span>
                ANPR
              </span>
            )}
          </div>
          <div className="flex items-center gap-1 text-slate-600 text-[11px] whitespace-nowrap overflow-hidden">
            <span className="material-symbols-outlined text-[14px] text-slate-400 shrink-0">
              directions_car
            </span>
            <span className="font-bold text-slate-900">{b.plate}</span>
            <span className="text-slate-300">·</span>
            <span
              className="text-slate-500 text-[11px] truncate max-w-[90px]"
              title={b.carModel}
            >
              {b.carModel}
            </span>
          </div>
        </div>
      </td>

      {/* CỘT 2: Bãi đỗ & Vị trí đỗ */}
      <td className={`py-3.5 px-4 transition-colors ${cellBaseClass}`}>
        <div className="flex flex-col gap-1">
          <span
            className="font-bold text-slate-900 text-xs hover:text-blue-600 transition-colors cursor-pointer truncate max-w-[190px] block"
            title={b.location}
          >
            {b.location}
          </span>
          <span
            className={`inline-flex items-center gap-1.5 text-[11px] font-bold px-2 py-0.5 rounded-md w-fit whitespace-nowrap ${
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
            <span className="truncate max-w-[160px]">{b.spot}</span>
          </span>
        </div>
      </td>

      {/* CỘT 3: Thời gian */}
      <td className={`py-3.5 px-4 transition-colors ${cellBaseClass}`}>
        <div className="flex flex-col gap-0.5">
          <div
            className={`flex items-center gap-1 font-bold text-xs ${
              timeInfo.isCancel ? 'text-rose-700' : 'text-slate-900'
            }`}
          >
            <span
              className={`material-symbols-outlined text-[13px] shrink-0 ${
                timeInfo.isCancel ? 'text-rose-500' : 'text-blue-600'
              }`}
            >
              {timeInfo.isCancel ? 'event_busy' : 'schedule'}
            </span>
            <span className="truncate max-w-[170px]">{timeInfo.line1}</span>
          </div>

          <div className="flex items-center gap-1.5 mt-0.5 text-[11px]">
            <span className="text-slate-400 font-medium whitespace-nowrap">
              {b.dateLabel}
            </span>
            {timeInfo.line2 && (
              <span
                className={`text-[9.5px] font-bold px-1.5 py-0.2 rounded shrink-0 truncate max-w-[110px] ${
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

      {/* CỘT 4: Dịch vụ */}
      <td className={`py-3.5 px-4 transition-colors ${cellBaseClass}`}>
        <div className="flex flex-col gap-1 max-w-[150px]">
          {b.services.map((srv, idx) => {
            const isEv = srv.includes('EV');
            const isExt = srv.includes('Gia hạn');
            return (
              <span
                key={idx}
                className={`inline-flex items-center gap-1 px-1.5 py-0.5 rounded text-[10px] font-medium border truncate w-fit ${
                  isEv
                    ? 'bg-amber-50 text-amber-800 border-amber-200/80'
                    : isExt
                    ? 'bg-indigo-50 text-indigo-700 border-indigo-200/80'
                    : 'bg-slate-50 text-slate-600 border-slate-200/60'
                }`}
              >
                {isEv ? (
                  <span className="material-symbols-outlined text-[11px] text-amber-600 shrink-0">
                    bolt
                  </span>
                ) : isExt ? (
                  <span className="material-symbols-outlined text-[11px] text-indigo-600 shrink-0">
                    more_time
                  </span>
                ) : (
                  <span className="material-symbols-outlined text-[11px] text-blue-500 shrink-0">
                    check
                  </span>
                )}
                <span className="truncate">{srv}</span>
              </span>
            );
          })}
        </div>
      </td>

      {/* CỘT 5: Tổng tiền */}
      <td className={`py-3.5 px-3 transition-colors ${cellBaseClass}`}>
        <div className="flex flex-col gap-0.5">
          <span
            className={`font-extrabold text-xs ${
              isCancelled ? 'text-slate-500' : 'text-slate-900'
            }`}
          >
            {b.totalPrice > 0 ? `${b.totalPrice.toLocaleString('vi-VN')} đ` : '0 đ'}
          </span>
          <span
            className={`text-[10.5px] truncate max-w-[120px] block ${
              isCancelled ? 'text-emerald-700 font-semibold' : 'text-slate-500'
            }`}
            title={b.paymentMethod}
          >
            {b.paymentMethod}
          </span>
        </div>
      </td>

      {/* CỘT 6: Trạng thái (Luôn nằm trên 1 dòng, không bao giờ rớt dòng) */}
      <td className={`py-3.5 px-3 transition-colors ${cellBaseClass} whitespace-nowrap`}>
        {isUpcoming && (
          <span className="inline-flex items-center gap-1.5 px-2.5 py-1 rounded-full bg-blue-50 text-blue-700 border border-blue-200/70 text-[11px] font-bold whitespace-nowrap">
            <span className="w-1.5 h-1.5 rounded-full bg-blue-600 animate-pulse shrink-0" />
            <span>Sắp diễn ra</span>
          </span>
        )}
        {isCompleted && (
          <span className="inline-flex items-center gap-1.5 px-2.5 py-1 rounded-full bg-emerald-50 text-emerald-800 border border-emerald-200/70 text-[11px] font-bold whitespace-nowrap">
            <span className="w-1.5 h-1.5 rounded-full bg-emerald-600 shrink-0" />
            <span>Đã hoàn thành</span>
          </span>
        )}
        {isCancelled && (
          <span className="inline-flex items-center gap-1.5 px-2.5 py-1 rounded-full bg-rose-50 text-rose-700 border border-rose-200/70 text-[11px] font-bold whitespace-nowrap">
            <span className="w-1.5 h-1.5 rounded-full bg-rose-600 shrink-0" />
            <span>{b.statusText || 'Đã hủy'}</span>
          </span>
        )}
      </td>

      {/* CỘT 7: Thao tác (Hàng ngang chuẩn) */}
      <td
        className={`py-3.5 px-4 text-right transition-colors ${cellBaseClass} ${
          isHighlighted ? 'border-r-2 border-blue-500 rounded-r-xl' : ''
        }`}
      >
        <div className="flex items-center justify-end gap-1.5 whitespace-nowrap">
          {isUpcoming ? (
            <>
              {/* Nút chính: Vé QR */}
              <button
                type="button"
                onClick={() => onViewQr(b)}
                title="Xem vé QR Check-in Barie"
                className="inline-flex items-center gap-1 px-2.5 py-1.5 rounded-lg bg-blue-600 hover:bg-blue-700 text-white text-xs font-bold shadow-xs transition-all cursor-pointer"
              >
                <span className="material-symbols-outlined text-[15px]">qr_code_2</span>
                <span>Vé QR</span>
              </button>

              {/* Nút Gia hạn giờ đỗ */}
              <button
                type="button"
                onClick={() => onExtend(b)}
                title="Gia hạn thêm thời gian đỗ"
                className="inline-flex items-center gap-1 px-2 py-1.5 rounded-lg border border-indigo-200 bg-indigo-50 hover:bg-indigo-100 text-indigo-700 text-xs font-semibold transition-colors cursor-pointer"
              >
                <span className="material-symbols-outlined text-[15px] text-indigo-600">
                  more_time
                </span>
                <span>Gia hạn</span>
              </button>

              {/* Sửa thông tin (Icon button) */}
              <button
                type="button"
                onClick={() => onEdit(b)}
                title="Sửa thông tin đặt chỗ (Xe/Giờ)"
                className="p-1.5 rounded-lg border border-slate-200 bg-white hover:bg-slate-50 text-slate-600 hover:text-blue-600 transition-colors cursor-pointer"
              >
                <span className="material-symbols-outlined text-[16px]">edit_calendar</span>
              </button>

              {/* Hủy đặt chỗ (Icon button) */}
              <button
                type="button"
                onClick={() => onCancel(b)}
                title="Hủy giữ chỗ (Chính sách hoàn tiền)"
                className="p-1.5 rounded-lg border border-slate-200 bg-white hover:bg-rose-50 text-slate-400 hover:text-rose-600 transition-colors cursor-pointer"
              >
                <span className="material-symbols-outlined text-[16px]">close</span>
              </button>
            </>
          ) : isCompleted ? (
            <>
              {/* Hóa đơn điện tử VAT */}
              <button
                type="button"
                onClick={() => onInvoice(b)}
                title="Xem & Tải hóa đơn điện tử VAT"
                className="inline-flex items-center gap-1 px-2.5 py-1.5 rounded-lg border border-slate-200 bg-white hover:bg-slate-50 text-slate-700 text-xs font-semibold transition-colors cursor-pointer"
              >
                <span className="material-symbols-outlined text-[15px] text-blue-600">
                  receipt_long
                </span>
                <span>Hóa đơn</span>
              </button>

              {b.reviewed ? (
                <span className="inline-flex items-center gap-1 px-2 py-1 rounded-lg bg-amber-50 text-amber-700 font-bold text-[11px] border border-amber-200">
                  <span className="material-symbols-outlined text-[13px] text-amber-500">
                    star
                  </span>
                  <span>{b.rating}★</span>
                </span>
              ) : (
                <button
                  type="button"
                  onClick={() => onFeedback(b)}
                  className="inline-flex items-center gap-1 px-2 py-1.5 rounded-lg border border-amber-300 bg-amber-50 hover:bg-amber-100 text-amber-800 font-bold text-xs transition-all cursor-pointer"
                >
                  <span className="material-symbols-outlined text-[14px] text-amber-500">
                    star
                  </span>
                  <span>Đánh giá</span>
                </button>
              )}
              <button
                type="button"
                onClick={() => onViewQr(b)}
                className="p-1.5 rounded-lg border border-slate-200 bg-white hover:bg-slate-100 text-slate-600 text-xs transition-colors cursor-pointer"
                title="Chi tiết vé"
              >
                <span className="material-symbols-outlined text-[16px]">visibility</span>
              </button>
            </>
          ) : (
            <button
              type="button"
              onClick={() => onRefundDetail(b)}
              className="px-3 py-1.5 rounded-lg border border-slate-200 bg-white hover:bg-slate-50 text-slate-700 text-xs font-semibold cursor-pointer transition-colors shadow-2xs"
            >
              Chi tiết hoàn
            </button>
          )}
        </div>
      </td>
    </tr>
  );
};
