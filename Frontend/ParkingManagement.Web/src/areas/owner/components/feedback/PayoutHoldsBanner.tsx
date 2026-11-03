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
    <div className="bg-amber-50 border-l-4 border-amber-500 p-4 rounded-r-xl shadow-xs mb-6">
      <div className="flex items-start gap-3">
        <div className="p-2 bg-amber-100 text-amber-700 rounded-lg shrink-0">
          <ShieldAlert className="w-5 h-5" />
        </div>
        <div className="flex-1">
          <div className="flex flex-wrap items-center justify-between gap-2">
            <h4 className="font-semibold text-amber-900 text-base flex items-center gap-2">
              <span>Khoản thanh toán bị phong tỏa</span>
              <span className="text-xs bg-amber-200 text-amber-800 font-bold px-2 py-0.5 rounded-full">
                {holds.length} khoản
              </span>
            </h4>
            <div className="text-right">
              <span className="text-xs text-amber-700 font-medium mr-1.5">Tổng tiền phong tỏa:</span>
              <span className="text-lg font-bold text-amber-900">{formatVnd(totalHeldAmount)}</span>
            </div>
          </div>

          <p className="text-xs text-amber-800/90 mt-1 leading-relaxed">
            Theo chính sách nền tảng, các khoản bồi hoàn đang trong quá trình tranh chấp sẽ được tạm giữ
            từ số dư quyết toán của chủ bãi cho đến khi có kết luận chính thức.
          </p>

          <div className="mt-3 flex flex-wrap gap-2">
            {holds.map((h, idx) => (
              <div
                key={`${h.complaintCode}-${idx}`}
                className="inline-flex items-center gap-2 bg-white/80 border border-amber-200 text-amber-900 text-xs px-2.5 py-1 rounded-md"
              >
                <Lock className="w-3.5 h-3.5 text-amber-600" />
                <span className="font-semibold">{h.complaintCode}</span>
                <span className="text-neutral-400">|</span>
                <span>{h.bookingCode}</span>
                <span className="text-neutral-400">|</span>
                <span className="font-bold text-amber-700">{formatVnd(h.amount)}</span>
                <span className="text-neutral-400">|</span>
                <span className="text-neutral-500">Từ {formatDate(h.heldSinceUtc)}</span>
              </div>
            ))}
          </div>
        </div>
      </div>
    </div>
  );
};
