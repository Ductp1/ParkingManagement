import React from 'react';
import { Lock, ShieldAlert } from 'lucide-react';
import type { PayoutHoldItem } from '@/types/feedback';

interface PayoutHoldsBannerProps {
  holds: PayoutHoldItem[];
}

export const PayoutHoldsBanner: React.FC<PayoutHoldsBannerProps> = ({ holds }) => {
  if (!holds || holds.length === 0) return null;

  const totalHeldAmount = holds.reduce((acc, h) => acc + (Number(h.amount) || 0), 0);

  const formatVnd = (amount: number) => {
    return new Intl.NumberFormat('vi-VN', {
      style: 'currency',
      currency: 'VND',
    }).format(amount);
  };

  const formatDate = (isoString: string) => {
    try {
      return new Date(isoString).toLocaleDateString('vi-VN', {
        day: '2-digit',
        month: '2-digit',
        year: 'numeric',
      });
    } catch {
      return isoString;
    }
  };

  return (
    <div className="bg-amber-50/90 border border-amber-200/90 p-5 rounded-2xl shadow-xs">
      <div className="flex items-start gap-4">
        <div className="w-10 h-10 bg-amber-100 text-amber-700 rounded-xl flex items-center justify-center shrink-0">
          <ShieldAlert className="w-5 h-5" />
        </div>
        <div className="flex-1">
          <div className="flex flex-wrap items-center justify-between gap-2">
            <h4 className="font-extrabold text-amber-950 text-base tracking-tight flex items-center gap-2">
              <span>Khoản thanh toán bị phong tỏa</span>
              <span className="text-xs bg-amber-200/80 text-amber-900 font-bold px-2.5 py-0.5 rounded-full border border-amber-300/60">
                {holds.length} khoản
              </span>
            </h4>
            <div className="text-right">
              <span className="text-xs text-amber-800 font-medium mr-1.5">Tổng tiền phong tỏa:</span>
              <span className="text-xl font-extrabold text-amber-950">{formatVnd(totalHeldAmount)}</span>
            </div>
          </div>

          <p className="text-xs text-amber-900/80 mt-1 leading-relaxed">
            Theo chính sách nền tảng ParkMaster, các khoản bồi hoàn đang trong quá trình tranh chấp sẽ được tạm giữ
            từ số dư quyết toán của chủ bãi cho đến khi có kết luận chính thức.
          </p>

          <div className="mt-3 flex flex-wrap gap-2">
            {holds.map((h, idx) => (
              <div
                key={`${h.complaintCode}-${idx}`}
                className="inline-flex items-center gap-2 bg-white border border-amber-200/80 text-slate-800 text-xs px-3 py-1.5 rounded-xl shadow-2xs font-medium"
              >
                <Lock className="w-3.5 h-3.5 text-amber-600" />
                <span className="font-bold text-slate-900">{h.complaintCode}</span>
                <span className="text-slate-300">|</span>
                <span className="text-slate-600">{h.bookingCode}</span>
                <span className="text-slate-300">|</span>
                <span className="font-extrabold text-amber-700">{formatVnd(h.amount)}</span>
                <span className="text-slate-300">|</span>
                <span className="text-slate-500 text-[11px]">Từ {formatDate(h.heldSinceUtc)}</span>
              </div>
            ))}
          </div>
        </div>
      </div>
    </div>
  );
};
