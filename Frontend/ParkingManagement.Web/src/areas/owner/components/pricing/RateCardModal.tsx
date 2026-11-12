import React, { useState } from 'react';
import {
  X,
  Plus,
  Trash2,
  DollarSign,
  AlertCircle,
  RefreshCw,
  Layers,
  CheckCircle2,
  Sliders,
} from 'lucide-react';
import {
  ownerPricingApi,
  type CreateRateRuleRequest,
} from '@/api/ownerPricingApi';

interface RateCardModalProps {
  isOpen: boolean;
  onClose: () => void;
  parkingLotId: number;
  ownerProfileId: number;
  onSuccess: () => void;
}

export const RateCardModal: React.FC<RateCardModalProps> = ({
  isOpen,
  onClose,
  parkingLotId,
  ownerProfileId,
  onSuccess,
}) => {
  const [name, setName] = useState('Bảng giá tiêu chuẩn');
  const [effectiveFrom, setEffectiveFrom] = useState(
    new Date().toISOString().split('T')[0]
  );
  const [overnightSurcharge, setOvernightSurcharge] = useState<number>(20000);
  const [weekendMultiplier, setWeekendMultiplier] = useState<number>(1.2);
  const [holidayMultiplier, setHolidayMultiplier] = useState<number>(1.5);
  const [oversizedMultiplier, setOversizedMultiplier] = useState<number>(1.3);
  const [overstayMultiplier, setOverstayMultiplier] = useState<number>(1.5);
  const [maxDailyAmount, setMaxDailyAmount] = useState<number>(200000);

  // Progressive Blocks Rules
  const [rules, setRules] = useState<CreateRateRuleRequest[]>([
    {
      vehicleType: 1, // Car
      fromMinute: 0,
      toMinute: 60,
      blockMinutes: 60,
      pricePerBlock: 15000,
      sortOrder: 1,
    },
    {
      vehicleType: 1, // Car
      fromMinute: 60,
      toMinute: 240,
      blockMinutes: 60,
      pricePerBlock: 20000,
      sortOrder: 2,
    },
    {
      vehicleType: 1, // Car
      fromMinute: 240,
      toMinute: null,
      blockMinutes: 60,
      pricePerBlock: 25000,
      sortOrder: 3,
    },
  ]);

  const [submitting, setSubmitting] = useState(false);
  const [error, setError] = useState<string | null>(null);

  if (!isOpen) return null;

  const handleAddRule = () => {
    const lastRule = rules[rules.length - 1];
    const newFrom = lastRule?.toMinute ?? 60;
    setRules([
      ...rules,
      {
        vehicleType: 1,
        fromMinute: newFrom,
        toMinute: newFrom + 60,
        blockMinutes: 60,
        pricePerBlock: 20000,
        sortOrder: rules.length + 1,
      },
    ]);
  };

  const handleRemoveRule = (index: number) => {
    if (rules.length <= 1) return;
    setRules(rules.filter((_, idx) => idx !== index));
  };

  const handleRuleChange = (
    index: number,
    field: keyof CreateRateRuleRequest,
    val: unknown
  ) => {
    const updated = [...rules];
    updated[index] = { ...updated[index], [field]: val };
    setRules(updated);
  };

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    if (!name.trim()) {
      setError('Vui lòng nhập tên bảng giá.');
      return;
    }
    if (rules.length === 0) {
      setError('Bảng giá phải có ít nhất 1 quy tắc tính block giờ.');
      return;
    }

    setSubmitting(true);
    setError(null);

    try {
      const fromUtc = new Date(effectiveFrom).toISOString();
      await ownerPricingApi.createRateCard({
        parkingLotId,
        ownerProfileId,
        name: name.trim(),
        effectiveFromUtc: fromUtc,
        effectiveToUtc: null,
        overnightSurcharge,
        weekendMultiplier,
        holidayMultiplier,
        oversizedMultiplier,
        overstayMultiplier,
        maxDailyAmount: maxDailyAmount > 0 ? maxDailyAmount : null,
        rules,
      });

      onSuccess();
      onClose();
    } catch (err: unknown) {
      const apiErr = err as { response?: { data?: { detail?: string; message?: string } }; message?: string };
      setError(
        apiErr?.response?.data?.detail ||
        apiErr?.response?.data?.message ||
        apiErr?.message ||
        'Không thể tạo bảng giá. Vui lòng kiểm tra lại.'
      );
    } finally {
      setSubmitting(false);
    }
  };

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
              <DollarSign size={20} />
            </div>
            <div>
              <h3 className="text-base font-bold text-slate-900">Thiết lập Bảng giá lũy tiến mới</h3>
              <p className="text-xs text-slate-500">Cấu hình các block giờ và hệ số phụ thu (UI-22 / US-070)</p>
            </div>
          </div>
          <button
            onClick={onClose}
            disabled={submitting}
            className="p-1.5 text-slate-400 hover:text-slate-600 rounded-lg hover:bg-slate-100 transition-colors"
          >
            <X size={18} />
          </button>
        </div>

        {/* Form Body */}
        <form onSubmit={handleSubmit} className="p-6 space-y-6 overflow-y-auto text-xs">
          {error && (
            <div className="p-3.5 rounded-xl bg-rose-50 border border-rose-200 text-rose-700 text-xs flex items-start gap-2.5">
              <AlertCircle size={16} className="shrink-0 mt-0.5" />
              <span>{error}</span>
            </div>
          )}

          {/* Thông tin cơ bản */}
          <div className="grid grid-cols-1 sm:grid-cols-2 gap-4">
            <div>
              <label className="block font-semibold text-slate-700 mb-1">
                Tên bảng giá <span className="text-rose-500">*</span>
              </label>
              <input
                type="text"
                required
                disabled={submitting}
                value={name}
                onChange={(e) => setName(e.target.value)}
                placeholder="Ví dụ: Bảng giá tiêu chuẩn 2026"
                className="w-full px-3 py-2 text-xs rounded-xl border border-slate-200 focus:outline-none focus:ring-2 focus:ring-blue-500/20 focus:border-blue-600 text-slate-900"
              />
            </div>

            <div>
              <label className="block font-semibold text-slate-700 mb-1">
                Ngày bắt đầu hiệu lực <span className="text-rose-500">*</span>
              </label>
              <input
                type="date"
                required
                disabled={submitting}
                value={effectiveFrom}
                onChange={(e) => setEffectiveFrom(e.target.value)}
                className="w-full px-3 py-2 text-xs rounded-xl border border-slate-200 focus:outline-none focus:ring-2 focus:ring-blue-500/20 focus:border-blue-600 text-slate-900"
              />
            </div>
          </div>

          {/* KHỐI 1: CÁC BLOCK GIÁ LŨY TIẾN */}
          <div className="space-y-3">
            <div className="flex items-center justify-between border-b border-slate-200 pb-2">
              <span className="font-bold text-slate-900 uppercase tracking-wider text-[11px] flex items-center gap-1.5">
                <Layers size={14} className="text-blue-600" />
                Quy tắc tính phí theo bậc thang (Block rules)
              </span>
              <button
                type="button"
                onClick={handleAddRule}
                disabled={submitting}
                className="inline-flex items-center gap-1 px-2.5 py-1 rounded-lg text-xs font-bold text-blue-600 hover:text-blue-800 hover:bg-blue-50 transition-colors"
              >
                <Plus size={13} />
                Thêm bậc giá
              </button>
            </div>

            <div className="space-y-2.5">
              {rules.map((rule, idx) => (
                <div
                  key={idx}
                  className="p-3.5 rounded-2xl bg-slate-50 border border-slate-200/80 flex flex-wrap sm:flex-nowrap items-center gap-2.5"
                >
                  <span className="w-6 h-6 rounded-full bg-blue-100 text-blue-700 font-bold flex items-center justify-center text-[10px] shrink-0">
                    {idx + 1}
                  </span>

                  <div className="flex-1 min-w-[120px]">
                    <span className="text-[10px] text-slate-400 block">Từ phút:</span>
                    <input
                      type="number"
                      min={0}
                      required
                      value={rule.fromMinute}
                      onChange={(e) =>
                        handleRuleChange(idx, 'fromMinute', parseInt(e.target.value, 10) || 0)
                      }
                      className="w-full px-2 py-1 text-xs rounded-lg border border-slate-200 bg-white"
                    />
                  </div>

                  <div className="flex-1 min-w-[120px]">
                    <span className="text-[10px] text-slate-400 block">Đến phút (trống = vô hạn):</span>
                    <input
                      type="number"
                      min={rule.fromMinute + 1}
                      value={rule.toMinute ?? ''}
                      onChange={(e) =>
                        handleRuleChange(
                          idx,
                          'toMinute',
                          e.target.value ? parseInt(e.target.value, 10) : null
                        )
                      }
                      placeholder="Không giới hạn"
                      className="w-full px-2 py-1 text-xs rounded-lg border border-slate-200 bg-white"
                    />
                  </div>

                  <div className="flex-1 min-w-[100px]">
                    <span className="text-[10px] text-slate-400 block">Block (phút):</span>
                    <input
                      type="number"
                      min={1}
                      required
                      value={rule.blockMinutes}
                      onChange={(e) =>
                        handleRuleChange(idx, 'blockMinutes', parseInt(e.target.value, 10) || 60)
                      }
                      className="w-full px-2 py-1 text-xs rounded-lg border border-slate-200 bg-white"
                    />
                  </div>

                  <div className="flex-1 min-w-[130px]">
                    <span className="text-[10px] text-slate-400 block">Giá mỗi block (VNĐ):</span>
                    <input
                      type="number"
                      min={0}
                      step={1000}
                      required
                      value={rule.pricePerBlock}
                      onChange={(e) =>
                        handleRuleChange(idx, 'pricePerBlock', parseFloat(e.target.value) || 0)
                      }
                      className="w-full px-2 py-1 text-xs font-bold text-blue-600 rounded-lg border border-slate-200 bg-white"
                    />
                  </div>

                  {rules.length > 1 && (
                    <button
                      type="button"
                      onClick={() => handleRemoveRule(idx)}
                      className="p-1.5 text-slate-400 hover:text-rose-600 rounded-lg hover:bg-rose-50 transition-colors mt-3 sm:mt-0"
                    >
                      <Trash2 size={15} />
                    </button>
                  )}
                </div>
              ))}
            </div>
          </div>

          {/* KHỐI 2: HỆ SỐ PHỤ THU & GIÁ TRẦN (US-071) */}
          <div className="space-y-3">
            <span className="font-bold text-slate-900 uppercase tracking-wider text-[11px] flex items-center gap-1.5 border-b border-slate-200 pb-2">
              <Sliders size={14} className="text-blue-600" />
              Cấu hình hệ số phụ thu & Giới hạn trần ngày (US-071)
            </span>

            <div className="grid grid-cols-1 sm:grid-cols-2 gap-3.5">
              <div className="p-3.5 rounded-2xl bg-slate-50 border border-slate-200/80">
                <label className="font-semibold text-slate-700 block mb-1">
                  Phụ thu qua đêm (VNĐ)
                </label>
                <input
                  type="number"
                  min={0}
                  step={5000}
                  value={overnightSurcharge}
                  onChange={(e) => setOvernightSurcharge(parseFloat(e.target.value) || 0)}
                  className="w-full px-2.5 py-1.5 text-xs font-bold rounded-lg border border-slate-200 bg-white"
                />
              </div>

              <div className="p-3.5 rounded-2xl bg-slate-50 border border-slate-200/80">
                <label className="font-semibold text-slate-700 block mb-1">
                  Giá trần tối đa / ngày (VNĐ)
                </label>
                <input
                  type="number"
                  min={0}
                  step={10000}
                  value={maxDailyAmount}
                  onChange={(e) => setMaxDailyAmount(parseFloat(e.target.value) || 0)}
                  className="w-full px-2.5 py-1.5 text-xs font-bold text-emerald-700 rounded-lg border border-slate-200 bg-white"
                />
              </div>

              <div className="p-3.5 rounded-2xl bg-slate-50 border border-slate-200/80">
                <label className="font-semibold text-slate-700 block mb-1">
                  Hệ số ngày cuối tuần (T7, CN)
                </label>
                <input
                  type="number"
                  min={1.0}
                  max={3.0}
                  step={0.1}
                  value={weekendMultiplier}
                  onChange={(e) => setWeekendMultiplier(parseFloat(e.target.value) || 1.0)}
                  className="w-full px-2.5 py-1.5 text-xs font-bold text-blue-600 rounded-lg border border-slate-200 bg-white"
                />
              </div>

              <div className="p-3.5 rounded-2xl bg-slate-50 border border-slate-200/80">
                <label className="font-semibold text-slate-700 block mb-1">
                  Hệ số ngày lễ quốc gia
                </label>
                <input
                  type="number"
                  min={1.0}
                  max={3.0}
                  step={0.1}
                  value={holidayMultiplier}
                  onChange={(e) => setHolidayMultiplier(parseFloat(e.target.value) || 1.0)}
                  className="w-full px-2.5 py-1.5 text-xs font-bold text-amber-600 rounded-lg border border-slate-200 bg-white"
                />
              </div>

              <div className="p-3.5 rounded-2xl bg-slate-50 border border-slate-200/80">
                <label className="font-semibold text-slate-700 block mb-1">
                  Hệ số xe quá khổ (Oversized)
                </label>
                <input
                  type="number"
                  min={1.0}
                  max={3.0}
                  step={0.1}
                  value={oversizedMultiplier}
                  onChange={(e) => setOversizedMultiplier(parseFloat(e.target.value) || 1.0)}
                  className="w-full px-2.5 py-1.5 text-xs font-bold text-indigo-600 rounded-lg border border-slate-200 bg-white"
                />
              </div>

              <div className="p-3.5 rounded-2xl bg-slate-50 border border-slate-200/80">
                <label className="font-semibold text-slate-700 block mb-1">
                  Hệ số phạt quá giờ (Overstay)
                </label>
                <input
                  type="number"
                  min={1.0}
                  max={5.0}
                  step={0.1}
                  value={overstayMultiplier}
                  onChange={(e) => setOverstayMultiplier(parseFloat(e.target.value) || 1.0)}
                  className="w-full px-2.5 py-1.5 text-xs font-bold text-rose-600 rounded-lg border border-slate-200 bg-white"
                />
              </div>
            </div>
          </div>

          {/* Actions */}
          <div className="flex items-center justify-end gap-3 pt-3 border-t border-slate-100">
            <button
              type="button"
              onClick={onClose}
              disabled={submitting}
              className="px-4 py-2 font-medium text-slate-600 hover:text-slate-800 hover:bg-slate-100 rounded-xl transition-colors"
            >
              Hủy bỏ
            </button>
            <button
              type="submit"
              disabled={submitting}
              className="inline-flex items-center gap-1.5 px-5 py-2.5 font-bold text-white bg-blue-600 hover:bg-blue-700 active:bg-blue-800 rounded-xl shadow-sm transition-all disabled:opacity-50"
            >
              {submitting ? (
                <>
                  <RefreshCw size={14} className="animate-spin" />
                  Đang lưu...
                </>
              ) : (
                <>
                  <CheckCircle2 size={15} />
                  Kích hoạt bảng giá
                </>
              )}
            </button>
          </div>
        </form>
      </div>
    </div>
  );
};
