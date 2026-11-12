import React, { useEffect, useState, useMemo, useCallback } from 'react';
import {
  DollarSign,
  Plus,
  Building2,
  RefreshCw,
  CheckCircle2,
  Layers,
} from 'lucide-react';
import { useAppSelector } from '@/store';
import { ownerLotsApi } from '@/api/ownerLotsApi';
import {
  ownerPricingApi,
  type RateCardDetailDto,
} from '@/api/ownerPricingApi';
import type { OwnerParkingLotItemDto } from '@/types/ownerKyb';
import { RateCardModal } from '../components/pricing/RateCardModal';

export const OwnerPricingPage: React.FC = () => {
  const { user } = useAppSelector((s) => s.auth);
  const ownerProfileId = user?.id || 2;

  // Danh sách bãi đỗ
  const [lots, setLots] = useState<OwnerParkingLotItemDto[]>([]);
  const [selectedLotId, setSelectedLotId] = useState<number | null>(null);
  const [loadingLots, setLoadingLots] = useState(true);

  // Danh sách bảng giá của bãi
  const [rateCards, setRateCards] = useState<RateCardDetailDto[]>([]);
  const [loadingRateCards, setLoadingRateCards] = useState(false);

  // Modals state
  const [isModalOpen, setIsModalOpen] = useState(false);
  const [toastMessage, setToastMessage] = useState<{ type: 'success' | 'error'; text: string } | null>(null);

  // 1. Tải danh sách bãi
  useEffect(() => {
    let isMounted = true;
    setLoadingLots(true);
    ownerLotsApi
      .getMyLots(ownerProfileId)
      .then((data) => {
        if (!isMounted) return;
        setLots(data || []);
        if (data && data.length > 0) {
          setSelectedLotId(data[0].id);
        }
      })
      .catch(() => {
        if (isMounted) setLots([]);
      })
      .finally(() => {
        if (isMounted) setLoadingLots(false);
      });

    return () => {
      isMounted = false;
    };
  }, [ownerProfileId]);

  // 2. Tải danh sách RateCards
  const loadRateCards = useCallback(async () => {
    if (!selectedLotId) return;
    setLoadingRateCards(true);
    try {
      const data = await ownerPricingApi.getRateCardsByLot(selectedLotId);
      setRateCards(data || []);
    } catch {
      setRateCards([]);
    } finally {
      setLoadingRateCards(false);
    }
  }, [selectedLotId]);

  useEffect(() => {
    void loadRateCards();
  }, [selectedLotId, loadRateCards]);

  const activeRateCard = useMemo(
    () => rateCards.find((r) => r.isActive) || rateCards[0] || null,
    [rateCards]
  );

  const formatVnd = (val?: number | null) => {
    if (val === undefined || val === null) return '0 ₫';
    return new Intl.NumberFormat('vi-VN', {
      style: 'currency',
      currency: 'VND',
    }).format(val);
  };

  const formatDate = (isoString?: string | null) => {
    if (!isoString) return '--';
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
    <div className="max-w-7xl mx-auto px-4 sm:px-6 lg:px-8 py-10 flex flex-col gap-8">
      {/* =====================================================================
          1. HEADER TRANG & BỘ CHỌN BÃI ĐỖ XE (UI-22 / US-070, US-071)
      ====================================================================== */}
      <div className="flex flex-col lg:flex-row lg:items-end justify-between gap-6 pb-6 border-b border-slate-200">
        <div className="flex flex-col gap-2 max-w-3xl">
          <div className="inline-flex items-center gap-2 px-3 py-1 rounded-full bg-blue-100 text-blue-800 w-fit text-xs font-bold tracking-wider uppercase">
            <span className="w-2 h-2 rounded-full bg-blue-600 animate-pulse"></span>
            <DollarSign size={13} />
            BIỂU PHÍ & BẢNG GIÁ LŨY TIẾN (UI-22)
          </div>
          <h1 className="text-3xl sm:text-4xl font-extrabold text-slate-900 tracking-tight">
            Bảng giá lũy tiến & Phụ thu
          </h1>
          <p className="text-sm sm:text-base text-slate-500 leading-relaxed">
            Thiết lập biểu phí theo bậc thang thời gian (US-070), phụ thu ngày lễ, cuối tuần, qua đêm (US-071) và theo dõi toàn bộ lịch sử các phiên bản đổi giá của bãi xe (US-072).
          </p>
        </div>

        {/* Dropdown bãi & Action button */}
        <div className="flex flex-wrap items-center gap-3 self-start lg:self-end">
          <div className="flex items-center gap-2 bg-white px-3.5 py-2.5 rounded-2xl border border-slate-200 shadow-sm">
            <Building2 size={16} className="text-blue-600 shrink-0" />
            <select
              value={selectedLotId || ''}
              onChange={(e) => setSelectedLotId(Number(e.target.value))}
              disabled={loadingLots || lots.length === 0}
              className="text-xs font-bold text-slate-800 bg-transparent focus:outline-none cursor-pointer pr-2"
            >
              {lots.map((lot) => (
                <option key={lot.id} value={lot.id}>
                  {lot.name} (#{lot.id})
                </option>
              ))}
            </select>
          </div>

          <button
            type="button"
            onClick={() => setIsModalOpen(true)}
            disabled={!selectedLotId}
            className="inline-flex items-center gap-2 px-4 py-2.5 text-xs font-bold text-white bg-blue-600 hover:bg-blue-700 active:bg-blue-800 rounded-2xl shadow-md shadow-blue-500/10 transition-all disabled:opacity-50"
          >
            <Plus size={16} />
            Thiết lập bảng giá mới
          </button>
        </div>
      </div>

      {/* Thông báo Toast */}
      {toastMessage && (
        <div
          className={`p-4 rounded-2xl text-sm flex items-start justify-between gap-3 shadow-sm animate-fade-in ${
            toastMessage.type === 'success'
              ? 'bg-emerald-50 border border-emerald-200 text-emerald-800'
              : 'bg-rose-50 border border-rose-200 text-rose-800'
          }`}
        >
          <div className="flex items-start gap-2.5">
            <CheckCircle2 size={18} className="text-emerald-600 shrink-0 mt-0.5" />
            <span>{toastMessage.text}</span>
          </div>
          <button
            onClick={() => setToastMessage(null)}
            className="text-xs font-bold opacity-70 hover:opacity-100"
          >
            Đóng
          </button>
        </div>
      )}

      {/* =====================================================================
          2. BẢNG GIÁ ĐANG HIỆU LỰC (ACTIVE RATE CARD)
      ====================================================================== */}
      {loadingRateCards ? (
        <div className="py-20 flex flex-col items-center justify-center gap-3 text-slate-500">
          <RefreshCw size={24} className="animate-spin text-blue-600" />
          <p className="text-xs font-medium">Đang tải bảng giá hiện hành...</p>
        </div>
      ) : activeRateCard ? (
        <div className="space-y-6">
          <div className="bg-white rounded-3xl p-6 sm:p-8 border border-slate-200 shadow-sm space-y-6">
            <div className="flex flex-wrap items-center justify-between gap-4 border-b border-slate-100 pb-5">
              <div className="flex items-center gap-3">
                <div className="w-12 h-12 rounded-2xl bg-blue-50 text-blue-700 border border-blue-200 flex items-center justify-center font-bold">
                  <DollarSign size={24} />
                </div>
                <div>
                  <div className="flex items-center gap-2">
                    <h2 className="text-lg font-bold text-slate-900">{activeRateCard.name}</h2>
                    <span className="inline-flex items-center gap-1 px-2.5 py-0.5 rounded-full text-[11px] font-bold bg-emerald-50 text-emerald-700 border border-emerald-200">
                      <span className="w-1.5 h-1.5 rounded-full bg-emerald-500" />
                      Đang áp dụng (Active)
                    </span>
                  </div>
                  <p className="text-xs text-slate-500 mt-0.5">
                    Hiệu lực từ ngày <strong>{formatDate(activeRateCard.effectiveFromUtc)}</strong>
                    {activeRateCard.effectiveToUtc && ` đến ${formatDate(activeRateCard.effectiveToUtc)}`}
                  </p>
                </div>
              </div>

              <div className="flex items-center gap-2">
                <button
                  type="button"
                  onClick={() => setIsModalOpen(true)}
                  className="inline-flex items-center gap-1.5 px-3.5 py-2 text-xs font-bold text-blue-700 bg-blue-50 hover:bg-blue-100 border border-blue-200 rounded-xl transition-all"
                >
                  <Plus size={14} />
                  Cập nhật phiên bản mới
                </button>
              </div>
            </div>

            {/* 6 Thẻ Hệ số Phụ thu & Giới hạn trần */}
            <div className="grid grid-cols-2 sm:grid-cols-3 lg:grid-cols-6 gap-3">
              <div className="p-3.5 rounded-2xl bg-slate-50 border border-slate-200/80">
                <span className="text-[10px] text-slate-400 block font-semibold uppercase tracking-wider">
                  Phụ thu qua đêm
                </span>
                <strong className="text-sm font-black text-slate-900 block mt-0.5">
                  +{formatVnd(activeRateCard.overnightSurcharge)}
                </strong>
                <span className="text-[10px] text-slate-400">Từ 22h – 06h</span>
              </div>

              <div className="p-3.5 rounded-2xl bg-slate-50 border border-slate-200/80">
                <span className="text-[10px] text-slate-400 block font-semibold uppercase tracking-wider">
                  Hệ số cuối tuần
                </span>
                <strong className="text-sm font-black text-blue-600 block mt-0.5">
                  x{activeRateCard.weekendMultiplier.toFixed(1)}
                </strong>
                <span className="text-[10px] text-slate-400">Thứ 7 & Chủ nhật</span>
              </div>

              <div className="p-3.5 rounded-2xl bg-slate-50 border border-slate-200/80">
                <span className="text-[10px] text-slate-400 block font-semibold uppercase tracking-wider">
                  Hệ số ngày lễ
                </span>
                <strong className="text-sm font-black text-amber-600 block mt-0.5">
                  x{activeRateCard.holidayMultiplier.toFixed(1)}
                </strong>
                <span className="text-[10px] text-slate-400">Lịch lễ quốc gia</span>
              </div>

              <div className="p-3.5 rounded-2xl bg-slate-50 border border-slate-200/80">
                <span className="text-[10px] text-slate-400 block font-semibold uppercase tracking-wider">
                  Hệ số quá khổ
                </span>
                <strong className="text-sm font-black text-indigo-600 block mt-0.5">
                  x{activeRateCard.oversizedMultiplier.toFixed(1)}
                </strong>
                <span className="text-[10px] text-slate-400">Xe &gt; 7 chỗ / tải</span>
              </div>

              <div className="p-3.5 rounded-2xl bg-slate-50 border border-slate-200/80">
                <span className="text-[10px] text-slate-400 block font-semibold uppercase tracking-wider">
                  Phạt quá giờ
                </span>
                <strong className="text-sm font-black text-rose-600 block mt-0.5">
                  x{activeRateCard.overstayMultiplier.toFixed(1)}
                </strong>
                <span className="text-[10px] text-slate-400">Sau ân hạn 15'</span>
              </div>

              <div className="p-3.5 rounded-2xl bg-slate-50 border border-slate-200/80">
                <span className="text-[10px] text-slate-400 block font-semibold uppercase tracking-wider">
                  Giá trần ngày
                </span>
                <strong className="text-sm font-black text-emerald-600 block mt-0.5">
                  {activeRateCard.maxDailyAmount ? formatVnd(activeRateCard.maxDailyAmount) : 'Không giới hạn'}
                </strong>
                <span className="text-[10px] text-slate-400">Tối đa 24h</span>
              </div>
            </div>

            {/* Bảng các block giá lũy tiến */}
            <div className="space-y-3">
              <span className="font-bold text-slate-900 uppercase tracking-wider text-xs flex items-center gap-1.5">
                <Layers size={14} className="text-blue-600" />
                Biểu phí bậc thang chi tiết theo thời gian đỗ xe (Block Rules)
              </span>

              <div className="overflow-x-auto rounded-2xl border border-slate-200">
                <table className="w-full text-left border-collapse text-xs">
                  <thead className="bg-slate-50/80 border-b border-slate-200 text-slate-500 font-semibold uppercase tracking-wider text-[11px]">
                    <tr>
                      <th className="py-3 px-6">Bậc</th>
                      <th className="py-3 px-6">Khoảng thời gian</th>
                      <th className="py-3 px-6">Thời lượng block</th>
                      <th className="py-3 px-6">Đơn giá / block</th>
                      <th className="py-3 px-6">Quy đổi ước tính</th>
                    </tr>
                  </thead>
                  <tbody className="divide-y divide-slate-100">
                    {activeRateCard.rules && activeRateCard.rules.length > 0 ? (
                      activeRateCard.rules.map((rule, idx) => {
                        const fromHours = (rule.fromMinute / 60).toFixed(rule.fromMinute % 60 === 0 ? 0 : 1);
                        const toHours = rule.toMinute
                          ? (rule.toMinute / 60).toFixed(rule.toMinute % 60 === 0 ? 0 : 1)
                          : 'vô hạn';

                        return (
                          <tr key={idx} className="hover:bg-slate-50/60 transition-colors">
                            <td className="py-3.5 px-6 font-bold text-slate-800">
                              Bậc {idx + 1}
                            </td>
                            <td className="py-3.5 px-6 font-medium text-slate-900">
                              Từ {rule.fromMinute} phút ({fromHours}h) →{' '}
                              {rule.toMinute ? `${rule.toMinute} phút (${toHours}h)` : 'trở đi'}
                            </td>
                            <td className="py-3.5 px-6 text-slate-600 font-mono">
                              Mỗi {rule.blockMinutes} phút
                            </td>
                            <td className="py-3.5 px-6 font-black text-blue-600 text-sm">
                              {formatVnd(rule.pricePerBlock)}
                            </td>
                            <td className="py-3.5 px-6 text-slate-500">
                              ~{formatVnd(Math.round((rule.pricePerBlock / rule.blockMinutes) * 60))}/giờ
                            </td>
                          </tr>
                        );
                      })
                    ) : (
                      <tr>
                        <td colSpan={5} className="py-6 text-center text-slate-400">
                          Chưa có quy tắc block nào được định nghĩa.
                        </td>
                      </tr>
                    )}
                  </tbody>
                </table>
              </div>
            </div>
          </div>

          {/* =====================================================================
              3. LỊCH SỬ ĐỔI GIÁ (RATE CARD VERSIONS - US-072)
          ====================================================================== */}
          <div className="bg-white rounded-3xl border border-slate-200 shadow-sm overflow-hidden">
            <div className="px-6 py-4 border-b border-slate-100 flex items-center justify-between">
              <div>
                <h3 className="text-sm font-bold text-slate-900">Lịch sử các phiên bản bảng giá (US-072)</h3>
                <p className="text-xs text-slate-500">Danh sách các biểu phí từng được áp dụng tại bãi xe</p>
              </div>
            </div>

            <div className="overflow-x-auto">
              <table className="w-full text-left border-collapse text-xs">
                <thead>
                  <tr className="bg-slate-50/80 border-b border-slate-200 text-slate-500 font-semibold uppercase tracking-wider text-[11px]">
                    <th className="py-3.5 px-6">Mã phiên bản</th>
                    <th className="py-3.5 px-6">Tên biểu phí</th>
                    <th className="py-3.5 px-6">Ngày bắt đầu</th>
                    <th className="py-3.5 px-6">Ngày kết thúc</th>
                    <th className="py-3.5 px-6">Phụ thu đêm / lễ</th>
                    <th className="py-3.5 px-6">Trạng thái</th>
                  </tr>
                </thead>
                <tbody className="divide-y divide-slate-100">
                  {rateCards.map((rc) => (
                    <tr key={rc.id} className="hover:bg-slate-50/60 transition-colors">
                      <td className="py-3.5 px-6 font-mono font-bold text-slate-900">
                        RC-#{rc.id}
                      </td>
                      <td className="py-3.5 px-6 font-semibold text-slate-800">
                        {rc.name}
                      </td>
                      <td className="py-3.5 px-6 text-slate-600">
                        {formatDate(rc.effectiveFromUtc)}
                      </td>
                      <td className="py-3.5 px-6 text-slate-600">
                        {formatDate(rc.effectiveToUtc)}
                      </td>
                      <td className="py-3.5 px-6 text-slate-700">
                        +{formatVnd(rc.overnightSurcharge)} / x{rc.holidayMultiplier}
                      </td>
                      <td className="py-3.5 px-6">
                        <span
                          className={`inline-flex items-center px-2.5 py-0.5 rounded-full text-[11px] font-bold border ${
                            rc.isActive
                              ? 'bg-emerald-50 text-emerald-700 border-emerald-200'
                              : 'bg-slate-100 text-slate-600 border-slate-200'
                          }`}
                        >
                          {rc.isActive ? 'Đang hiệu lực' : 'Đã thay thế'}
                        </span>
                      </td>
                    </tr>
                  ))}
                </tbody>
              </table>
            </div>
          </div>
        </div>
      ) : (
        <div className="bg-white rounded-3xl p-16 text-center border border-slate-200 shadow-sm space-y-3">
          <div className="w-14 h-14 rounded-2xl bg-blue-50 text-blue-600 border border-blue-200 flex items-center justify-center mx-auto">
            <DollarSign size={28} />
          </div>
          <h3 className="text-base font-bold text-slate-900">Bãi xe chưa có biểu phí</h3>
          <p className="text-xs text-slate-500 max-w-sm mx-auto">
            Bắt đầu tạo bảng giá lũy tiến đầu tiên để mở bán chỗ đỗ và cho phép tài xế đặt chỗ trực tuyến.
          </p>
          <button
            type="button"
            onClick={() => setIsModalOpen(true)}
            className="inline-flex items-center gap-2 px-5 py-2.5 text-xs font-bold text-white bg-blue-600 hover:bg-blue-700 rounded-2xl shadow-sm transition-all"
          >
            <Plus size={15} />
            Thiết lập bảng giá ngay
          </button>
        </div>
      )}

      {/* Modal tạo RateCard */}
      {selectedLotId && (
        <RateCardModal
          isOpen={isModalOpen}
          onClose={() => setIsModalOpen(false)}
          parkingLotId={selectedLotId}
          ownerProfileId={ownerProfileId}
          onSuccess={() => {
            setToastMessage({
              type: 'success',
              text: 'Đã kích hoạt bảng giá mới cho bãi đỗ xe thành công!',
            });
            void loadRateCards();
          }}
        />
      )}
    </div>
  );
};
