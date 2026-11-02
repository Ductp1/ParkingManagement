import React from 'react';
import { Plus, Edit2, Trash2, ArrowDownCircle, ArrowUpCircle, Layers, Grid, Weight, ArrowUpDown } from 'lucide-react';
import type { FloorDto } from '@/types/ownerStructure';

interface FloorTableProps {
  floors: FloorDto[];
  zoneName: string;
  loading?: boolean;
  onAddFloor: () => void;
  onEditFloor: (floor: FloorDto) => void;
  onDeleteFloor: (floor: FloorDto) => void;
}

export const FloorTable: React.FC<FloorTableProps> = ({
  floors,
  zoneName,
  loading = false,
  onAddFloor,
  onEditFloor,
  onDeleteFloor,
}) => {
  // Sắp xếp tầng theo thứ tự cao độ giảm dần (Tầng cao nhất ở trên, tầng hầm sâu nhất ở dưới)
  const sortedFloors = [...floors].sort((a, b) => b.level - a.level);

  return (
    <div className="rounded-3xl bg-white border border-slate-200/90 shadow-xs overflow-hidden">
      {/* Header khu vực tầng */}
      <div className="flex flex-col sm:flex-row sm:items-center justify-between gap-4 p-5 sm:p-6 border-b border-slate-100 bg-slate-50/50">
        <div>
          <div className="flex items-center gap-2">
            <span className="text-[11px] font-extrabold uppercase text-indigo-700 tracking-wider">
              Cấu trúc phân tầng
            </span>
            <span className="px-2 py-0.5 rounded-full bg-indigo-100 text-indigo-800 text-[10px] font-bold">
              {floors.length} tầng
            </span>
          </div>
          <h2 className="text-lg font-bold text-slate-900 mt-0.5">
            Danh sách tầng thuộc: <span className="text-indigo-600">{zoneName}</span>
          </h2>
          <p className="text-xs text-slate-500 mt-0.5">
            Quản lý cao độ, chiều cao tĩnh không, tải trọng sàn và ma trận lưới ô đỗ
          </p>
        </div>

        <button
          type="button"
          onClick={onAddFloor}
          className="px-4 py-2.5 rounded-xl bg-indigo-600 hover:bg-indigo-700 text-white text-xs font-bold shadow-md shadow-indigo-600/20 active:scale-[0.98] transition-all flex items-center gap-1.5 cursor-pointer shrink-0"
        >
          <Plus size={15} />
          Thêm Tầng Mới
        </button>
      </div>

      {/* Loading state */}
      {loading ? (
        <div className="py-16 text-center text-xs font-semibold text-slate-500">
          Đang tải danh sách tầng…
        </div>
      ) : sortedFloors.length === 0 ? (
        /* Empty State */
        <div className="p-10 text-center">
          <div className="w-14 h-14 rounded-2xl bg-indigo-50 text-indigo-600 flex items-center justify-center mx-auto mb-3">
            <Layers size={28} />
          </div>
          <h3 className="text-sm font-bold text-slate-900">
            Khu vực này chưa có tầng nào
          </h3>
          <p className="text-xs text-slate-500 max-w-sm mx-auto mt-1">
            Khai báo các tầng đỗ xe vật lý (hầm, tầng trệt hoặc tầng nổi) để bắt đầu phân bố ô đỗ.
          </p>
          <button
            type="button"
            onClick={onAddFloor}
            className="mt-4 px-4 py-2 rounded-xl bg-indigo-600 hover:bg-indigo-700 text-white text-xs font-bold shadow-xs transition-all inline-flex items-center gap-1.5 cursor-pointer"
          >
            <Plus size={14} />
            Thêm Tầng Đầu Tiên
          </button>
        </div>
      ) : (
        /* Table Content */
        <div className="overflow-x-auto">
          <table className="w-full text-left text-xs">
            <thead className="bg-slate-50/80 text-slate-500 font-bold border-b border-slate-100 uppercase text-[10px] tracking-wider">
              <tr>
                <th className="py-3.5 px-4 sm:px-6">Cao độ (Level)</th>
                <th className="py-3.5 px-4">Tên tầng</th>
                <th className="py-3.5 px-4">Thông số kỹ thuật</th>
                <th className="py-3.5 px-4">Ma trận lưới (Sơ đồ)</th>
                <th className="py-3.5 px-4">Trạng thái</th>
                <th className="py-3.5 px-4 sm:px-6 text-right">Thao tác</th>
              </tr>
            </thead>
            <tbody className="divide-y divide-slate-100">
              {sortedFloors.map((floor) => {
                const isBasement = floor.level < 0;
                const isGround = floor.level === 0;

                return (
                  <tr key={floor.id} className="hover:bg-slate-50/70 transition-colors">
                    {/* Cao độ Level */}
                    <td className="py-4 px-4 sm:px-6 whitespace-nowrap">
                      <div className="flex items-center gap-2">
                        {isBasement ? (
                          <span className="inline-flex items-center gap-1 px-2.5 py-1 rounded-xl bg-purple-50 text-purple-700 font-mono font-extrabold border border-purple-200">
                            <ArrowDownCircle size={13} className="text-purple-600" />
                            B{Math.abs(floor.level)} ({floor.level})
                          </span>
                        ) : isGround ? (
                          <span className="inline-flex items-center gap-1 px-2.5 py-1 rounded-xl bg-emerald-50 text-emerald-700 font-mono font-extrabold border border-emerald-200">
                            <ArrowUpDown size={13} className="text-emerald-600" />
                            G (0)
                          </span>
                        ) : (
                          <span className="inline-flex items-center gap-1 px-2.5 py-1 rounded-xl bg-blue-50 text-blue-700 font-mono font-extrabold border border-blue-200">
                            <ArrowUpCircle size={13} className="text-blue-600" />
                            T{floor.level} (+{floor.level})
                          </span>
                        )}
                      </div>
                    </td>

                    {/* Tên tầng */}
                    <td className="py-4 px-4">
                      <span className="font-bold text-slate-900 block truncate max-w-[200px]" title={floor.name}>
                        {floor.name}
                      </span>
                    </td>

                    {/* Chiều cao & Tải trọng */}
                    <td className="py-4 px-4 whitespace-nowrap">
                      <div className="space-y-0.5">
                        <div className="flex items-center gap-1 text-slate-600 text-[11px]">
                          <span className="text-slate-400">Trần:</span>
                          <span className="font-semibold text-slate-800">
                            {floor.maxHeightCm ? `${floor.maxHeightCm} cm` : 'Không giới hạn'}
                          </span>
                        </div>
                        {floor.maxWeightKg && (
                          <div className="flex items-center gap-1 text-slate-600 text-[11px]">
                            <Weight size={11} className="text-slate-400" />
                            <span className="font-semibold text-slate-800">
                              {(floor.maxWeightKg / 1000).toFixed(1)} tấn
                            </span>
                          </div>
                        )}
                      </div>
                    </td>

                    {/* Ma trận lưới Columns x Rows */}
                    <td className="py-4 px-4 whitespace-nowrap">
                      <div className="inline-flex items-center gap-1.5 px-2.5 py-1 rounded-xl bg-slate-100 font-mono font-bold text-slate-700 border border-slate-200">
                        <Grid size={12} className="text-slate-500" />
                        <span>
                          {floor.gridColumns} × {floor.gridRows}
                        </span>
                        <span className="text-slate-400 text-[10px]">
                          ({floor.gridColumns * floor.gridRows} ô)
                        </span>
                      </div>
                    </td>

                    {/* Trạng thái hoạt động */}
                    <td className="py-4 px-4 whitespace-nowrap">
                      {floor.isClosed ? (
                        <span className="inline-flex items-center gap-1 text-[11px] font-bold px-2.5 py-1 rounded-full bg-rose-50 text-rose-700 border border-rose-200">
                          Tạm đóng
                        </span>
                      ) : (
                        <span className="inline-flex items-center gap-1 text-[11px] font-bold px-2.5 py-1 rounded-full bg-emerald-50 text-emerald-700 border border-emerald-200">
                          Hoạt động
                        </span>
                      )}
                    </td>

                    {/* Thao tác */}
                    <td className="py-4 px-4 sm:px-6 whitespace-nowrap text-right">
                      <div className="flex items-center justify-end gap-1.5">
                        <button
                          type="button"
                          onClick={() => onEditFloor(floor)}
                          className="p-1.5 rounded-lg text-slate-500 hover:text-indigo-600 hover:bg-indigo-50 transition-colors cursor-pointer"
                          title="Chỉnh sửa thông số tầng"
                        >
                          <Edit2 size={14} />
                        </button>
                        <button
                          type="button"
                          onClick={() => onDeleteFloor(floor)}
                          className="p-1.5 rounded-lg text-slate-500 hover:text-rose-600 hover:bg-rose-50 transition-colors cursor-pointer"
                          title="Xóa tầng"
                        >
                          <Trash2 size={14} />
                        </button>
                      </div>
                    </td>
                  </tr>
                );
              })}
            </tbody>
          </table>
        </div>
      )}
    </div>
  );
};
