import React, { useState, useEffect } from 'react';
import {
  X,
  FileSpreadsheet,
  Download,
  AlertCircle,
  RefreshCw,
  Scale,
  Receipt,
} from 'lucide-react';
import {
  ownerRevenueApi,
  type OwnerSettlementDetailDto,
} from '@/api/ownerRevenueApi';

interface SettlementDetailModalProps {
  isOpen: boolean;
  onClose: () => void;
  settlementId: number | null;
}

export const SettlementDetailModal: React.FC<SettlementDetailModalProps> = ({
  isOpen,
  onClose,
  settlementId,
}) => {
  const [detail, setDetail] = useState<OwnerSettlementDetailDto | null>(null);
  const [loading, setLoading] = useState(false);
  const [downloadingCsv, setDownloadingCsv] = useState(false);
  const [error, setError] = useState<string | null>(null);

  useEffect(() => {
    if (!isOpen || !settlementId) {
      setDetail(null);
      setError(null);
      return;
    }

    let isMounted = true;
    setLoading(true);
    setError(null);

    ownerRevenueApi
      .getSettlementDetail(settlementId)
      .then((data) => {
        if (isMounted) setDetail(data);
      })
      .catch((err: unknown) => {
        if (!isMounted) return;
        const msg = err instanceof Error ? err.message : 'Không thể tải chi tiết kỳ quyết toán.';
        setError(msg);
      })
      .finally(() => {
        if (isMounted) setLoading(false);
      });

    return () => {
      isMounted = false;
    };
  }, [isOpen, settlementId]);

  if (!isOpen) return null;

  const formatVnd = (val?: number) => {
    if (val === undefined || val === null) return '0 ₫';
    return new Intl.NumberFormat('vi-VN', {
      style: 'currency',
      currency: 'VND',
    }).format(val);
  };

  const handleDownloadCsv = async () => {
    if (!settlementId || !detail) return;
    setDownloadingCsv(true);
    try {
      const blob = await ownerRevenueApi.exportSettlementCsv(settlementId);
      const url = window.URL.createObjectURL(blob);
      const link = document.createElement('a');
      link.href = url;
      link.download = `Settlement_${detail.settlement.code}_Lines.csv`;
      document.body.appendChild(link);
      link.click();
      link.remove();
      window.URL.revokeObjectURL(url);
    } catch {
      setError('Không thể tải file CSV đối soát.');
    } finally {
      setDownloadingCsv(false);
    }
  };

  const settlement = detail?.settlement;

  return (
    <div className="fixed inset-0 z-50 flex items-center justify-center p-4 bg-slate-900/60 backdrop-blur-sm animate-fade-in">
      <div
        className="bg-white rounded-3xl shadow-2xl border border-slate-100 w-full max-w-2xl overflow-hidden flex flex-col max-h-[92vh]"
        onClick={(e) => e.stopPropagation()}
      >
        {/* Header */}
        <div className="flex items-center justify-between px-6 py-4 border-b border-slate-100 bg-slate-50/70">
          <div className="flex items-center gap-3">
            <div className="w-10 h-10 rounded-2xl bg-blue-100 text-blue-700 flex items-center justify-center font-bold">
              <FileSpreadsheet size={20} />
            </div>
            <div>
              <div className="flex items-center gap-2">
                <h3 className="text-base font-bold text-slate-900 font-mono">
                  {settlement?.code || 'Chi tiết quyết toán'}
                </h3>
                {settlement && (
                  <span
                    className={`inline-flex items-center px-2.5 py-0.5 rounded-full text-[11px] font-bold border ${
                      settlement.status === 'Paid'
                        ? 'bg-emerald-50 text-emerald-700 border-emerald-200'
                        : 'bg-blue-50 text-blue-700 border-blue-200'
                    }`}
                  >
                    {settlement.status === 'Paid' ? 'Đã thanh toán (Paid)' : 'Đã duyệt (Approved)'}
                  </span>
                )}
              </div>
              <p className="text-xs text-slate-500 mt-0.5">
                Kỳ từ {settlement?.periodStart} đến {settlement?.periodEnd} (US-080)
              </p>
            </div>
          </div>
          <button
            onClick={onClose}
            className="p-1.5 text-slate-400 hover:text-slate-600 rounded-lg hover:bg-slate-100 transition-colors"
          >
            <X size={18} />
          </button>
        </div>

        {/* Body */}
        <div className="p-6 space-y-6 overflow-y-auto">
          {error && (
            <div className="p-3.5 rounded-xl bg-rose-50 border border-rose-200 text-rose-700 text-xs flex items-start gap-2.5">
              <AlertCircle size={16} className="shrink-0 mt-0.5" />
              <span>{error}</span>
            </div>
          )}

          {loading ? (
            <div className="py-16 flex flex-col items-center justify-center gap-2 text-slate-500">
              <RefreshCw size={24} className="animate-spin text-blue-600" />
              <p className="text-xs font-medium">Đang tải chi tiết quyết toán...</p>
            </div>
          ) : detail && settlement ? (
            <>
              {/* Card Thực nhận lớn */}
              <div className="bg-gradient-to-br from-blue-600 to-indigo-700 rounded-2xl p-6 text-white shadow-md space-y-1">
                <span className="text-xs text-blue-100 uppercase tracking-wider font-semibold">
                  Số tiền thực nhận về tài khoản (Net Payout)
                </span>
                <div className="text-3xl sm:text-4xl font-black tracking-tight">
                  {formatVnd(settlement.netPayout)}
                </div>
                {settlement.payoutReference && (
                  <p className="text-xs text-blue-200 pt-1">
                    Mã chuẩn chi chuyển khoản: <strong className="font-mono text-white">{settlement.payoutReference}</strong>
                  </p>
                )}
              </div>

              {/* Bảng phân rã công thức quyết toán */}
              <div className="bg-slate-50 p-4 rounded-2xl border border-slate-200/80 space-y-3 text-xs">
                <div className="flex items-center justify-between border-b border-slate-200 pb-2">
                  <span className="font-bold text-slate-800 uppercase tracking-wider text-[11px] flex items-center gap-1.5">
                    <Scale size={14} className="text-blue-600" />
                    Bảng đối soát doanh thu & khấu trừ
                  </span>
                  <span className="text-[11px] text-slate-500">Đơn vị: VNĐ</span>
                </div>

                <div className="space-y-2 text-slate-600">
                  <div className="flex justify-between items-center">
                    <span>Tổng doanh thu gộp phát sinh:</span>
                    <strong className="text-slate-900 font-semibold">{formatVnd(settlement.grossAmount)}</strong>
                  </div>

                  <div className="flex justify-between items-center text-rose-600">
                    <span>(-) Tiền đã hoàn lại cho khách:</span>
                    <strong className="font-semibold">-{formatVnd(settlement.refundAmount)}</strong>
                  </div>

                  <div className="flex justify-between items-center text-slate-700">
                    <span>(-) Tiền mặt chủ bãi đã thu trực tiếp tại cổng:</span>
                    <strong className="font-semibold">-{formatVnd(settlement.ownerCollectedAmount)}</strong>
                  </div>

                  <div className="flex justify-between items-center text-amber-700">
                    <span>(-) Phí dịch vụ hoa hồng nền tảng:</span>
                    <strong className="font-semibold">-{formatVnd(settlement.commissionAmount)}</strong>
                  </div>

                  {settlement.heldAmount > 0 && (
                    <div className="flex justify-between items-center text-purple-700">
                      <span>(-) Số tiền tạm giữ khiếu nại (Dispute Hold):</span>
                      <strong className="font-semibold">-{formatVnd(settlement.heldAmount)}</strong>
                    </div>
                  )}

                  {settlement.adjustmentAmount !== 0 && (
                    <div className="flex justify-between items-center text-blue-700">
                      <span>(+/-) Điều chỉnh tài chính phát sinh:</span>
                      <strong className="font-semibold">
                        {settlement.adjustmentAmount > 0 ? '+' : ''}
                        {formatVnd(settlement.adjustmentAmount)}
                      </strong>
                    </div>
                  )}

                  {settlement.carryOverAmount !== 0 && (
                    <div className="flex justify-between items-center text-slate-700">
                      <span>(+) Số dư tồn đọng kỳ trước chuyển sang:</span>
                      <strong className="font-semibold">{formatVnd(settlement.carryOverAmount)}</strong>
                    </div>
                  )}

                  <div className="pt-2 border-t border-slate-200 flex justify-between items-center font-bold text-slate-900 text-sm">
                    <span>Số tiền thực nhận quyết toán:</span>
                    <span className="text-blue-600">{formatVnd(settlement.netPayout)}</span>
                  </div>
                </div>
              </div>

              {/* Tổng hợp theo loại dòng (Line Totals) */}
              {detail.lineTotals && detail.lineTotals.length > 0 && (
                <div className="space-y-2">
                  <span className="font-bold text-slate-700 uppercase tracking-wider text-[11px] flex items-center gap-1.5">
                    <Receipt size={14} className="text-indigo-600" />
                    Phân loại nguồn thu ({detail.lineTotals.length} loại)
                  </span>
                  <div className="overflow-x-auto rounded-2xl border border-slate-200">
                    <table className="w-full text-left border-collapse text-xs">
                      <thead className="bg-slate-50/80 border-b border-slate-200 text-slate-500 font-semibold uppercase tracking-wider text-[10px]">
                        <tr>
                          <th className="py-2.5 px-4">Loại giao dịch</th>
                          <th className="py-2.5 px-4 text-center">Số lượt</th>
                          <th className="py-2.5 px-4 text-right">Doanh thu gộp</th>
                          <th className="py-2.5 px-4 text-right">Hoa hồng</th>
                        </tr>
                      </thead>
                      <tbody className="divide-y divide-slate-100">
                        {detail.lineTotals.map((line, idx) => (
                          <tr key={idx} className="hover:bg-slate-50/60">
                            <td className="py-2 px-4 font-semibold text-slate-800">
                              {line.lineType}
                            </td>
                            <td className="py-2 px-4 text-center text-slate-600 font-mono">
                              {line.lineCount}
                            </td>
                            <td className="py-2 px-4 text-right font-medium text-slate-900">
                              {formatVnd(line.grossAmount)}
                            </td>
                            <td className="py-2 px-4 text-right font-medium text-amber-700">
                              {formatVnd(line.commissionAmount)}
                            </td>
                          </tr>
                        ))}
                      </tbody>
                    </table>
                  </div>
                </div>
              )}

              {/* Điều chỉnh nếu có */}
              {detail.adjustments && detail.adjustments.length > 0 && (
                <div className="p-4 rounded-2xl bg-amber-50/80 border border-amber-200 text-xs space-y-2">
                  <span className="font-bold text-amber-900 uppercase tracking-wider text-[11px] block">
                    Các khoản điều chỉnh tài chính ({detail.adjustments.length})
                  </span>
                  {detail.adjustments.map((adj) => (
                    <div key={adj.id} className="flex justify-between items-center text-amber-800">
                      <span>{adj.reason} ({adj.sourceType})</span>
                      <strong className="font-bold">
                        {adj.amount > 0 ? '+' : ''}
                        {formatVnd(adj.amount)}
                      </strong>
                    </div>
                  ))}
                </div>
              )}

              {/* Actions Footer */}
              <div className="pt-4 border-t border-slate-100 flex items-center justify-between gap-3">
                <button
                  type="button"
                  onClick={handleDownloadCsv}
                  disabled={downloadingCsv}
                  className="inline-flex items-center gap-1.5 px-4 py-2 text-xs font-bold text-white bg-blue-600 hover:bg-blue-700 active:bg-blue-800 rounded-xl shadow-xs transition-all disabled:opacity-50"
                >
                  {downloadingCsv ? (
                    <>
                      <RefreshCw size={14} className="animate-spin" />
                      Đang chuẩn bị file...
                    </>
                  ) : (
                    <>
                      <Download size={14} />
                      Tải file đối soát CSV (US-080)
                    </>
                  )}
                </button>

                <button
                  type="button"
                  onClick={onClose}
                  className="px-4 py-2 text-xs font-semibold text-slate-600 hover:text-slate-800 hover:bg-slate-100 rounded-xl transition-colors"
                >
                  Đóng
                </button>
              </div>
            </>
          ) : null}
        </div>
      </div>
    </div>
  );
};
