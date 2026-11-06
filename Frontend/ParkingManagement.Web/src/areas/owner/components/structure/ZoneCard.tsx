import React from 'react';
import { Building2, Sun, Edit2, Trash2, Layers, AlertCircle } from 'lucide-react';
import type { ZoneDto } from '@/types/ownerStructure';

interface ZoneCardProps {
  zone: ZoneDto;
  isSelected: boolean;
  floorCount?: number;
  onSelect: (zone: ZoneDto) => void;
  onEdit: (zone: ZoneDto) => void;
  onDelete: (zone: ZoneDto) => void;
}

export const ZoneCard: React.FC<ZoneCardProps> = ({
  zone,
  isSelected,
  floorCount = 0,
  onSelect,
  onEdit,
  onDelete,
}) => {
  return (
    <div
      onClick={() => onSelect(zone)}
      className={`group relative rounded-2xl p-4 sm:p-5 transition-all cursor-pointer border ${
        isSelected
          ? 'bg-indigo-50/70 border-indigo-500 shadow-md shadow-indigo-500/10 ring-2 ring-indigo-500/30'
          : 'bg-white hover:bg-slate-50 border-slate-200/90 shadow-2xs hover:border-slate-300'
      }`}
    >
      <div className="flex items-start justify-between gap-3">
        <div className="flex items-center gap-2.5">
          {/* Badge Mã Khu Vực (Mono font) */}
          <span
            className={`font-mono text-xs font-black px-2.5 py-1 rounded-xl uppercase tracking-wider ${
              isSelected
                ? 'bg-indigo-600 text-white'
                : 'bg-slate-900 text-slate-100 group-hover:bg-indigo-600 group-hover:text-white transition-colors'
            }`}
          >
            {zone.code}
          </span>

          {/* Phân loại ngoài trời / trong nhà */}
          {zone.isOutdoor ? (
            <span className="inline-flex items-center gap-1 text-[11px] font-bold px-2 py-0.5 rounded-lg bg-amber-50 text-amber-700 border border-amber-200">
              <Sun size={12} className="text-amber-500" />
              Ngoài trời
            </span>
          ) : (
            <span className="inline-flex items-center gap-1 text-[11px] font-bold px-2 py-0.5 rounded-lg bg-blue-50 text-blue-700 border border-blue-200">
              <Building2 size={12} className="text-blue-500" />
              Trong nhà
            </span>
          )}
        </div>

        {/* Nút tác vụ (Sửa / Xóa) */}
        <div
          className="flex items-center gap-1 opacity-80 group-hover:opacity-100 transition-opacity"
          onClick={(e) => e.stopPropagation()}
        >
          <button
            type="button"
            onClick={() => onEdit(zone)}
            className="p-1.5 rounded-lg text-slate-400 hover:text-indigo-600 hover:bg-indigo-50 transition-colors cursor-pointer"
            title="Chỉnh sửa khu vực"
          >
            <Edit2 size={14} />
          </button>
          <button
            type="button"
            onClick={() => onDelete(zone)}
            className="p-1.5 rounded-lg text-slate-400 hover:text-rose-600 hover:bg-rose-50 transition-colors cursor-pointer"
            title="Xóa khu vực"
          >
            <Trash2 size={14} />
          </button>
        </div>
      </div>

      {/* Tên khu vực */}
      <h3 className="text-sm font-extrabold text-slate-900 mt-2.5 truncate" title={zone.name}>
        {zone.name}
      </h3>

      {/* Thông tin số lượng tầng & Trạng thái */}
      <div className="flex items-center justify-between mt-3 pt-3 border-t border-slate-100 text-xs">
        <div className="flex items-center gap-1.5 text-slate-500 font-semibold">
          <Layers size={13} className="text-slate-400" />
          <span>{floorCount} tầng trực thuộc</span>
        </div>

        {zone.isClosed ? (
          <span className="inline-flex items-center gap-1 text-[11px] font-bold px-2 py-0.5 rounded-md bg-rose-50 text-rose-700 border border-rose-200">
            <AlertCircle size={11} />
            Tạm đóng
          </span>
        ) : (
          <span className="inline-flex items-center gap-1 text-[11px] font-bold px-2 py-0.5 rounded-md bg-emerald-50 text-emerald-700">
            <span className="w-1.5 h-1.5 rounded-full bg-emerald-500"></span>
            Hoạt động
          </span>
        )}
      </div>

      {zone.isClosed && zone.closedReason && (
        <p className="mt-2 text-[11px] text-rose-600 italic truncate" title={zone.closedReason}>
          Lý do: {zone.closedReason}
        </p>
      )}
    </div>
  );
};
