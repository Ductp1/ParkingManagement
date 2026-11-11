import React, { useState, useEffect, useCallback, useMemo } from 'react';
import {
  X,
  Car,
  Search,
  AlertTriangle,
  Loader2,
  Zap,
  Crown,
  Power,
  Grid,
} from 'lucide-react';
import { ownerStructureApi } from '@/api/ownerStructureApi';
import type { FloorDto, SlotDto, UpdateSlotRequest } from '@/types/ownerStructure';

interface FloorSlotsDrawerProps {
  isOpen: boolean;
  onClose: () => void;
  floor: FloorDto | null;
  zoneName: string;
  onSlotsUpdated?: () => void;
}

export const FloorSlotsDrawer: React.FC<FloorSlotsDrawerProps> = ({
  isOpen,
  onClose,
  floor,
  zoneName,
  onSlotsUpdated,
}) => {
  const [slots, setSlots] = useState<SlotDto[]>([]);
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState<string | null>(null);

  // Bộ lọc & Tìm kiếm
  const [searchQuery, setSearchQuery] = useState('');
  const [selectedType, setSelectedType] = useState<string>('ALL');
  const [selectedStatus, setSelectedStatus] = useState<string>('ALL'); // 'ALL' | 'ACTIVE' | 'INACTIVE'

  // Trạng thái thao tác từng ô
  const [updatingSlotId, setUpdatingSlotId] = useState<number | null>(null);
  const [actionError, setActionError] = useState<string | null>(null);

  const fetchSlots = useCallback(async () => {
    if (!floor) return;
    setLoading(true);
    setError(null);
    try {
      const data = await ownerStructureApi.getSlots(floor.id);
      const sorted = [...data].sort((a, b) =>
        a.code.localeCompare(b.code, undefined, { numeric: true, sensitivity: 'base' })
      );
      setSlots(sorted);
    } catch (err: unknown) {
      if (err instanceof Error) {
        setError(err.message);
      } else {
        setError('Không thể tải danh sách ô đỗ của tầng.');
      }
    } finally {
      setLoading(false);
    }
  }, [floor]);

  useEffect(() => {
    if (isOpen && floor) {
      fetchSlots();
      setSearchQuery('');
      setSelectedType('ALL');
      setSelectedStatus('ALL');
      setActionError(null);
    }
  }, [isOpen, floor, fetchSlots]);

  // Lọc danh sách ô đỗ
  const filteredSlots = useMemo(() => {
    return slots.filter((slot) => {
      // Tìm theo code
      if (searchQuery.trim() && !slot.code.toLowerCase().includes(searchQuery.trim().toLowerCase())) {
        return false;
      }
      // Lọc theo loại ô
      if (selectedType !== 'ALL' && slot.slotType !== selectedType) {
        return false;
      }
      // Lọc theo kích hoạt isActive
      if (selectedStatus === 'ACTIVE' && !slot.isActive) {
        return false;
      }
      if (selectedStatus === 'INACTIVE' && slot.isActive) {
        return false;
      }
      return true;
    });
  }, [slots, searchQuery, selectedType, selectedStatus]);

  // Thống kê nhanh
  const activeCount = useMemo(() => slots.filter((s) => s.isActive).length, [slots]);
  const inactiveCount = slots.length - activeCount;

  // Toggle kích hoạt ô đỗ (isActive)
  const handleToggleSlotActive = async (slot: SlotDto) => {
    setUpdatingSlotId(slot.id);
    setActionError(null);
    try {
      const payload: UpdateSlotRequest = {
        code: slot.code,
        slotType: slot.slotType,
        maxVehicleType: slot.maxVehicleType,
        gridX: slot.gridX,
        gridY: slot.gridY,
        widthCells: slot.widthCells,
        heightCells: slot.heightCells,
        isActive: !slot.isActive,
      };

      const updated = await ownerStructureApi.updateSlot(slot.id, payload);
      setSlots((prev) => prev.map((s) => (s.id === slot.id ? updated : s)));
      if (onSlotsUpdated) {
        onSlotsUpdated();
      }
    } catch (err: unknown) {
      if (err instanceof Error) {
        setActionError(err.message);
      } else {
        setActionError('Không thể cập nhật trạng thái ô đỗ.');
      }
    } finally {
      setUpdatingSlotId(null);
    }
  };

  if (!isOpen || !floor) return null;

  return (
    <div className="fixed inset-0 z-50 overflow-hidden bg-slate-900/50 backdrop-blur-2xs">
      <div className="absolute inset-y-0 right-0 max-w-full flex pl-10">
        <div className="w-screen max-w-2xl bg-white shadow-2xl border-l border-slate-200 flex flex-col">
          {/* Header */}
          <div className="p-5 sm:p-6 border-b border-slate-100 bg-slate-50/70 flex items-center justify-between">
            <div className="flex items-center gap-3">
              <div className="w-11 h-11 rounded-2xl bg-indigo-50 border border-indigo-100 text-indigo-600 flex items-center justify-center shrink-0">
                <Car size={22} />
              </div>
              <div>
                <div className="flex items-center gap-2">
                  <span className="text-[11px] font-extrabold uppercase text-indigo-700 tracking-wider">
                    Danh Sách & Quản Lý Ô Đỗ (US-058 & US-063)
                  </span>
                  <span className="px-2 py-0.5 rounded-full bg-indigo-100 text-indigo-800 text-[10px] font-bold">
                    {slots.length} ô
                  </span>
                </div>
                <h2 className="text-lg font-bold text-slate-900 mt-0.5">
                  {floor.name} <span className="text-slate-400 font-normal">({zoneName})</span>
                </h2>
              </div>
            </div>

            <button
              type="button"
              onClick={onClose}
              className="w-9 h-9 rounded-xl flex items-center justify-center text-slate-400 hover:text-slate-700 hover:bg-slate-200/60 transition-colors cursor-pointer"
            >
              <X size={18} />
            </button>
          </div>

          {/* Action Error if any */}
          {actionError && (
            <div className="mx-6 mt-4 p-3 rounded-xl bg-rose-50 border border-rose-200 text-rose-700 text-xs flex items-center gap-2">
              <AlertTriangle size={15} className="text-rose-500 shrink-0" />
              <span>{actionError}</span>
            </div>
          )}

          {/* Thanh số liệu tổng quan */}
          <div className="grid grid-cols-3 gap-3 p-4 mx-6 mt-4 rounded-2xl bg-slate-50 border border-slate-200/80">
            <div>
              <span className="text-[10px] font-bold uppercase text-slate-400 block">Tổng ô đỗ</span>
              <span className="text-base font-extrabold text-slate-900">{slots.length} ô</span>
            </div>
            <div>
              <span className="text-[10px] font-bold uppercase text-emerald-600 block">Đang hoạt động</span>
              <span className="text-base font-extrabold text-emerald-700">{activeCount} ô</span>
            </div>
            <div>
              <span className="text-[10px] font-bold uppercase text-rose-600 block">Tạm khóa bảo trì</span>
              <span className="text-base font-extrabold text-rose-700">{inactiveCount} ô</span>
            </div>
          </div>

          {/* Bộ lọc và Tìm kiếm */}
          <div className="p-6 pb-2 space-y-3">
            <div className="flex flex-col sm:flex-row gap-2.5">
              <div className="relative flex-1">
                <Search size={15} className="absolute left-3 top-2.5 text-slate-400" />
                <input
                  type="text"
                  value={searchQuery}
                  onChange={(e) => setSearchQuery(e.target.value)}
                  placeholder="Tìm theo mã ô (VD: A-01, B-12)..."
                  className="w-full pl-9 pr-3 py-2 rounded-xl border border-slate-200 text-xs text-slate-800 placeholder-slate-400 focus:outline-hidden focus:ring-2 focus:ring-indigo-500"
                />
              </div>

              <select
                value={selectedType}
                onChange={(e) => setSelectedType(e.target.value)}
                className="px-3 py-2 rounded-xl border border-slate-200 bg-white text-xs font-semibold text-slate-700 focus:outline-hidden focus:ring-2 focus:ring-indigo-500"
              >
                <option value="ALL">Tất cả loại ô</option>
                <option value="Standard">Tiêu chuẩn (Standard)</option>
                <option value="EvCharging">Trụ sạc điện (EV)</option>
                <option value="Vip">VIP / Cố định</option>
                <option value="Handicapped">Người khuyết tật</option>
                <option value="Compact">Xe nhỏ (Compact)</option>
                <option value="Oversized">Quá khổ (Oversized)</option>
              </select>

              <select
                value={selectedStatus}
                onChange={(e) => setSelectedStatus(e.target.value)}
                className="px-3 py-2 rounded-xl border border-slate-200 bg-white text-xs font-semibold text-slate-700 focus:outline-hidden focus:ring-2 focus:ring-indigo-500"
              >
                <option value="ALL">Tất cả trạng thái</option>
                <option value="ACTIVE">Đang hoạt động</option>
                <option value="INACTIVE">Tạm khóa bảo trì</option>
              </select>
            </div>
          </div>

          {/* Danh sách ô đỗ */}
          <div className="flex-1 overflow-y-auto px-6 py-2">
            {loading ? (
              <div className="py-20 text-center text-xs font-semibold text-slate-500 flex flex-col items-center gap-2">
                <Loader2 size={24} className="animate-spin text-indigo-600" />
                <span>Đang tải danh sách ô đỗ…</span>
              </div>
            ) : error ? (
              <div className="p-4 rounded-2xl bg-rose-50 border border-rose-200 text-rose-700 text-xs flex items-center gap-2">
                <AlertTriangle size={16} className="text-rose-500 shrink-0" />
                <span>{error}</span>
              </div>
            ) : filteredSlots.length === 0 ? (
              <div className="py-16 text-center text-xs text-slate-400">
                {slots.length === 0
                  ? 'Tầng này chưa có ô đỗ nào được khởi tạo.'
                  : 'Không tìm thấy ô đỗ nào phù hợp với bộ lọc.'}
              </div>
            ) : (
              <div className="divide-y divide-slate-100">
                {filteredSlots.map((slot) => {
                  const isUpdating = updatingSlotId === slot.id;

                  return (
                    <div
                      key={slot.id}
                      className="py-3.5 flex items-center justify-between gap-4 hover:bg-slate-50/70 transition-colors px-2 rounded-xl"
                    >
                      <div className="flex items-center gap-3">
                        {/* Icon loại ô */}
                        <div
                          className={`w-9 h-9 rounded-xl flex items-center justify-center shrink-0 ${
                            slot.slotType === 'EvCharging'
                              ? 'bg-amber-50 text-amber-600 border border-amber-200'
                              : slot.slotType === 'Vip'
                              ? 'bg-yellow-50 text-yellow-600 border border-yellow-200'
                              : 'bg-indigo-50 text-indigo-600 border border-indigo-100'
                          }`}
                        >
                          {slot.slotType === 'EvCharging' ? (
                            <Zap size={16} />
                          ) : slot.slotType === 'Vip' ? (
                            <Crown size={16} />
                          ) : (
                            <Car size={16} />
                          )}
                        </div>

                        <div>
                          <div className="flex items-center gap-2">
                            <span className="font-mono text-sm font-extrabold text-slate-900">
                              {slot.code}
                            </span>
                            <span className="text-[10px] font-bold px-2 py-0.5 rounded-md bg-slate-100 text-slate-700">
                              {slot.slotType}
                            </span>
                            <span className="text-[10px] text-slate-500">
                              Tối đa: {slot.maxVehicleType}
                            </span>
                          </div>

                          <div className="flex items-center gap-3 mt-1 text-[11px] text-slate-500">
                            <span className="flex items-center gap-1">
                              <Grid size={11} className="text-slate-400" />
                              Lưới: ({slot.gridX}, {slot.gridY})
                            </span>
                            <span>
                              Cỡ: {slot.widthCells}×{slot.heightCells}
                            </span>
                            {slot.state && (
                              <span
                                className={`font-semibold ${
                                  slot.state === 'Occupied'
                                    ? 'text-rose-600'
                                    : slot.state === 'Reserved'
                                    ? 'text-amber-600'
                                    : 'text-emerald-600'
                                }`}
                              >
                                ● {slot.state}
                              </span>
                            )}
                          </div>
                        </div>
                      </div>

                      {/* Nút Toggle isActive (Tạm đóng / Mở lại ô đỗ) */}
                      <button
                        type="button"
                        disabled={isUpdating}
                        onClick={() => handleToggleSlotActive(slot)}
                        className={`px-3 py-1.5 rounded-xl text-xs font-bold transition-all flex items-center gap-1.5 cursor-pointer shadow-2xs active:scale-[0.98] disabled:opacity-50 ${
                          slot.isActive
                            ? 'bg-emerald-50 text-emerald-700 hover:bg-rose-50 hover:text-rose-700 border border-emerald-200 hover:border-rose-200'
                            : 'bg-rose-50 text-rose-700 hover:bg-emerald-50 hover:text-emerald-700 border border-rose-200 hover:border-emerald-200'
                        }`}
                        title={
                          slot.isActive
                            ? 'Bấm để tạm khóa ô đỗ (bảo trì / hỏng sạc)'
                            : 'Bấm để mở lại ô đỗ'
                        }
                      >
                        {isUpdating ? (
                          <Loader2 size={13} className="animate-spin" />
                        ) : (
                          <Power size={13} />
                        )}
                        <span>{slot.isActive ? 'Hoạt động' : 'Tạm khóa'}</span>
                      </button>
                    </div>
                  );
                })}
              </div>
            )}
          </div>

          {/* Footer */}
          <div className="p-4 sm:p-5 border-t border-slate-100 bg-slate-50 flex items-center justify-between">
            <span className="text-xs text-slate-500">
              Khóa ô đỗ sẽ ngăn tài xế đặt chỗ vào ô này ngay lập tức
            </span>
            <button
              type="button"
              onClick={onClose}
              className="px-4 py-2 rounded-xl bg-slate-200 hover:bg-slate-300 text-slate-700 text-xs font-bold transition-all cursor-pointer"
            >
              Đóng
            </button>
          </div>
        </div>
      </div>
    </div>
  );
};
