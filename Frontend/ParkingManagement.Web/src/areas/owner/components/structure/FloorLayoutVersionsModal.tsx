import React, { useState, useEffect, useCallback } from 'react';
import {
  X,
  History,
  Grid,
  GitCompare,
  RotateCcw,
  CheckCircle2,
  AlertTriangle,
  Clock,
  Info,
  Car,
  Zap,
  Crown,
  Bike,
  ShieldAlert,
  Loader2,
  Plus,
  Minus,
  Edit3,
} from 'lucide-react';
import { ownerLayoutApi } from '@/api/ownerLayoutApi';
import type { FloorDto } from '@/types/ownerStructure';
import type {
  LayoutVersionSummaryDto,
  FloorLayoutDetailDto,
  FloorLayoutDiffDto,
  LayoutCellDto,
} from '@/types/ownerLayout';

interface FloorLayoutVersionsModalProps {
  isOpen: boolean;
  onClose: () => void;
  floor: FloorDto | null;
  zoneName: string;
  onRollbackSuccess?: () => void;
}

type TabType = 'timeline' | 'preview' | 'diff';

export const FloorLayoutVersionsModal: React.FC<FloorLayoutVersionsModalProps> = ({
  isOpen,
  onClose,
  floor,
  zoneName,
  onRollbackSuccess,
}) => {
  const [activeTab, setActiveTab] = useState<TabType>('timeline');

  // Dữ liệu versions
  const [versions, setVersions] = useState<LayoutVersionSummaryDto[]>([]);
  const [loadingVersions, setLoadingVersions] = useState(false);
  const [versionsError, setVersionsError] = useState<string | null>(null);

  // Dữ liệu preview sơ đồ cụ thể
  const [selectedVersionNo, setSelectedVersionNo] = useState<number | null>(null);
  const [layoutDetail, setLayoutDetail] = useState<FloorLayoutDetailDto | null>(null);
  const [loadingLayout, setLoadingLayout] = useState(false);
  const [layoutError, setLayoutError] = useState<string | null>(null);
  const [hoveredCell, setHoveredCell] = useState<LayoutCellDto | null>(null);

  // Dữ liệu Diff
  const [diffFromVersion, setDiffFromVersion] = useState<number | null>(null);
  const [diffToVersion, setDiffToVersion] = useState<number | null>(null);
  const [diffData, setDiffData] = useState<FloorLayoutDiffDto | null>(null);
  const [loadingDiff, setLoadingDiff] = useState(false);
  const [diffError, setDiffError] = useState<string | null>(null);

  // Rollback dialog state
  const [rollbackTarget, setRollbackTarget] = useState<LayoutVersionSummaryDto | null>(null);
  const [rollbackNote, setRollbackNote] = useState('');
  const [rollbackBusy, setRollbackBusy] = useState(false);
  const [rollbackError, setRollbackError] = useState<string | null>(null);
  const [rollbackSuccessMsg, setRollbackSuccessMsg] = useState<string | null>(null);

  // Tải danh sách versions khi mở modal
  const fetchVersions = useCallback(async () => {
    if (!floor) return;
    setLoadingVersions(true);
    setVersionsError(null);
    try {
      const data = await ownerLayoutApi.getVersions(floor.id);
      // Sắp xếp phiên bản mới nhất lên đầu
      const sorted = [...data].sort((a, b) => b.versionNo - a.versionNo);
      setVersions(sorted);
      if (sorted.length > 0) {
        // Mặc định chọn phiên bản mới nhất cho preview
        setSelectedVersionNo(sorted[0].versionNo);
        if (sorted.length >= 2) {
          setDiffFromVersion(sorted[1].versionNo);
          setDiffToVersion(sorted[0].versionNo);
        } else {
          setDiffFromVersion(sorted[0].versionNo);
          setDiffToVersion(sorted[0].versionNo);
        }
      }
    } catch (err: unknown) {
      if (err instanceof Error) {
        setVersionsError(err.message);
      } else {
        setVersionsError('Không thể tải danh sách phiên bản sơ đồ.');
      }
    } finally {
      setLoadingVersions(false);
    }
  }, [floor]);

  useEffect(() => {
    if (isOpen && floor) {
      fetchVersions();
      setActiveTab('timeline');
      setRollbackTarget(null);
      setRollbackSuccessMsg(null);
      setRollbackError(null);
    }
  }, [isOpen, floor, fetchVersions]);

  // Tải chi tiết sơ đồ khi đổi selectedVersionNo hoặc chuyển sang tab preview
  useEffect(() => {
    if (!floor || selectedVersionNo === null) return;
    let isCancelled = false;

    const fetchDetail = async () => {
      setLoadingLayout(true);
      setLayoutError(null);
      try {
        const data = await ownerLayoutApi.getVersionByNo(floor.id, selectedVersionNo);
        if (!isCancelled) {
          setLayoutDetail(data);
        }
      } catch (err: unknown) {
        if (!isCancelled) {
          if (err instanceof Error) {
            setLayoutError(err.message);
          } else {
            setLayoutError('Không thể tải chi tiết sơ đồ phiên bản.');
          }
        }
      } finally {
        if (!isCancelled) {
          setLoadingLayout(false);
        }
      }
    };

    fetchDetail();
    return () => {
      isCancelled = true;
    };
  }, [floor, selectedVersionNo]);

  // Tải kết quả so sánh Diff khi thay đổi diffFromVersion hoặc diffToVersion
  useEffect(() => {
    if (!floor || diffFromVersion === null || activeTab !== 'diff') return;
    let isCancelled = false;

    const fetchDiff = async () => {
      setLoadingDiff(true);
      setDiffError(null);
      try {
        const data = await ownerLayoutApi.getDiff(
          floor.id,
          diffFromVersion,
          diffToVersion !== null && diffToVersion !== diffFromVersion ? diffToVersion : undefined
        );
        if (!isCancelled) {
          setDiffData(data);
        }
      } catch (err: unknown) {
        if (!isCancelled) {
          if (err instanceof Error) {
            setDiffError(err.message);
          } else {
            setDiffError('Không thể tính toán so sánh khác biệt giữa 2 phiên bản.');
          }
        }
      } finally {
        if (!isCancelled) {
          setLoadingDiff(false);
        }
      }
    };

    fetchDiff();
    return () => {
      isCancelled = true;
    };
  }, [floor, diffFromVersion, diffToVersion, activeTab]);

  if (!isOpen || !floor) return null;

  // Thực hiện Rollback
  const handleExecuteRollback = async () => {
    if (!floor || !rollbackTarget) return;
    setRollbackBusy(true);
    setRollbackError(null);
    try {
      await ownerLayoutApi.rollback(floor.id, rollbackTarget.versionNo, rollbackNote);
      setRollbackSuccessMsg(`Khôi phục sơ đồ tầng về Phiên bản v${rollbackTarget.versionNo} thành công!`);
      setRollbackTarget(null);
      setRollbackNote('');
      // Làm mới danh sách phiên bản
      await fetchVersions();
      if (onRollbackSuccess) {
        onRollbackSuccess();
      }
    } catch (err: unknown) {
      if (err instanceof Error) {
        setRollbackError(err.message);
      } else {
        setRollbackError('Đã có lỗi xảy ra khi khôi phục phiên bản sơ đồ.');
      }
    } finally {
      setRollbackBusy(false);
    }
  };

  // Helper render loại cell
  const renderCellBadge = (cell: LayoutCellDto) => {
    switch (cell.type) {
      case 'slot':
        return (
          <div className="flex flex-col items-center justify-center p-1 rounded-lg bg-emerald-50 border border-emerald-200 text-emerald-800 text-[10px] font-bold h-full">
            <div className="flex items-center gap-0.5">
              {cell.slotType === 'EvCharging' ? (
                <Zap size={10} className="text-amber-600" />
              ) : cell.slotType === 'Vip' ? (
                <Crown size={10} className="text-yellow-600" />
              ) : cell.slotType === 'Motorbike' ? (
                <Bike size={10} className="text-blue-600" />
              ) : (
                <Car size={10} className="text-emerald-600" />
              )}
              <span className="truncate max-w-[45px]">{cell.code || 'Slot'}</span>
            </div>
            {cell.state && (
              <span className="text-[8px] font-medium text-emerald-600 truncate">{cell.state}</span>
            )}
          </div>
        );
      case 'road':
        return (
          <div className="flex items-center justify-center p-1 rounded-lg bg-slate-100 border border-slate-200/60 text-slate-400 text-[9px] font-medium h-full">
            <span>Đường</span>
          </div>
        );
      case 'entry':
        return (
          <div className="flex items-center justify-center p-1 rounded-lg bg-teal-100 border border-teal-300 text-teal-800 text-[9px] font-extrabold uppercase h-full">
            Vào
          </div>
        );
      case 'exit':
        return (
          <div className="flex items-center justify-center p-1 rounded-lg bg-rose-100 border border-rose-300 text-rose-800 text-[9px] font-extrabold uppercase h-full">
            Ra
          </div>
        );
      case 'pillar':
      case 'wall':
        return (
          <div className="flex items-center justify-center p-1 rounded-lg bg-slate-300/80 border border-slate-400/80 text-slate-700 text-[9px] font-bold h-full">
            {cell.type === 'pillar' ? 'Cột' : 'Tường'}
          </div>
        );
      default:
        return (
          <div className="rounded-lg bg-slate-50/50 border border-dashed border-slate-200/50 h-full" />
        );
    }
  };

  const latestVersionNo = versions.length > 0 ? versions[0].versionNo : null;

  return (
    <div className="fixed inset-0 z-50 flex items-center justify-center p-3 sm:p-4 bg-slate-900/60 backdrop-blur-xs overflow-y-auto">
      <div className="relative w-full max-w-5xl rounded-3xl bg-white border border-slate-200 shadow-2xl overflow-hidden my-auto flex flex-col max-h-[92vh]">
        {/* Modal Header */}
        <div className="flex items-center justify-between p-5 sm:p-6 border-b border-slate-100 bg-slate-50/70">
          <div className="flex items-center gap-3">
            <div className="w-11 h-11 rounded-2xl bg-blue-50 border border-blue-100 text-blue-600 flex items-center justify-center shrink-0">
              <History size={22} />
            </div>
            <div>
              <div className="flex items-center gap-2">
                <span className="text-[11px] font-extrabold uppercase text-blue-700 tracking-wider">
                  Quản lý Sơ đồ Tầng (US-059 & US-060)
                </span>
                <span className="px-2 py-0.5 rounded-full bg-blue-100 text-blue-800 text-[10px] font-bold">
                  {versions.length} phiên bản
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

        {/* Thông báo thành công Rollback nếu có */}
        {rollbackSuccessMsg && (
          <div className="mx-6 mt-4 p-3.5 rounded-2xl bg-emerald-50 border border-emerald-200 text-emerald-800 text-xs font-semibold flex items-center gap-2.5">
            <CheckCircle2 size={16} className="text-emerald-600 shrink-0" />
            <span>{rollbackSuccessMsg}</span>
          </div>
        )}

        {/* Tabs Bar */}
        <div className="flex items-center gap-2 px-6 pt-4 border-b border-slate-100 bg-white">
          <button
            type="button"
            onClick={() => setActiveTab('timeline')}
            className={`px-4 py-2.5 rounded-t-xl text-xs font-bold flex items-center gap-2 transition-all cursor-pointer border-b-2 ${
              activeTab === 'timeline'
                ? 'border-blue-600 text-blue-700 bg-blue-50/50'
                : 'border-transparent text-slate-500 hover:text-slate-800 hover:bg-slate-50'
            }`}
          >
            <History size={14} />
            Lịch sử phiên bản
          </button>

          <button
            type="button"
            onClick={() => setActiveTab('preview')}
            className={`px-4 py-2.5 rounded-t-xl text-xs font-bold flex items-center gap-2 transition-all cursor-pointer border-b-2 ${
              activeTab === 'preview'
                ? 'border-blue-600 text-blue-700 bg-blue-50/50'
                : 'border-transparent text-slate-500 hover:text-slate-800 hover:bg-slate-50'
            }`}
          >
            <Grid size={14} />
            Mặt bằng lưới 2D (Visualizer)
          </button>

          <button
            type="button"
            onClick={() => setActiveTab('diff')}
            className={`px-4 py-2.5 rounded-t-xl text-xs font-bold flex items-center gap-2 transition-all cursor-pointer border-b-2 ${
              activeTab === 'diff'
                ? 'border-blue-600 text-blue-700 bg-blue-50/50'
                : 'border-transparent text-slate-500 hover:text-slate-800 hover:bg-slate-50'
            }`}
          >
            <GitCompare size={14} />
            So sánh khác biệt (Diff Inspector)
          </button>
        </div>

        {/* Modal Body */}
        <div className="p-6 overflow-y-auto flex-1">
          {/* ============================================================== */}
          {/* TAB 1: TIMELINE LỊCH SỬ PHIÊN BẢN                              */}
          {/* ============================================================== */}
          {activeTab === 'timeline' && (
            <div>
              {loadingVersions ? (
                <div className="py-20 text-center text-xs font-semibold text-slate-500 flex flex-col items-center gap-2">
                  <Loader2 size={24} className="animate-spin text-blue-600" />
                  <span>Đang tải lịch sử các phiên bản sơ đồ…</span>
                </div>
              ) : versionsError ? (
                <div className="p-4 rounded-2xl bg-rose-50 border border-rose-200 text-rose-700 text-xs font-medium flex items-center gap-2">
                  <AlertTriangle size={16} className="text-rose-500 shrink-0" />
                  <span>{versionsError}</span>
                </div>
              ) : versions.length === 0 ? (
                <div className="py-16 text-center">
                  <div className="w-12 h-12 rounded-2xl bg-slate-100 text-slate-400 flex items-center justify-center mx-auto mb-3">
                    <History size={24} />
                  </div>
                  <h3 className="text-sm font-bold text-slate-800">Chưa có phiên bản xuất bản nào</h3>
                  <p className="text-xs text-slate-500 max-w-sm mx-auto mt-1">
                    Tầng này hiện đang dùng ma trận tự động mặc định hoặc chưa từng xuất bản sơ đồ qua Layout Configurator.
                  </p>
                </div>
              ) : (
                <div className="space-y-4">
                  <div className="flex items-center justify-between text-xs text-slate-500 pb-2 border-b border-slate-100">
                    <span>Tổng cộng {versions.length} lần xuất bản layout</span>
                    <span>Phiên bản v{latestVersionNo} đang hoạt động</span>
                  </div>

                  <div className="relative border-l-2 border-blue-100 ml-4 pl-6 space-y-6">
                    {versions.map((ver) => {
                      const isCurrent = ver.versionNo === latestVersionNo;
                      const pubDate = ver.publishedAtUtc
                        ? new Date(ver.publishedAtUtc).toLocaleString('vi-VN', {
                            dateStyle: 'medium',
                            timeStyle: 'short',
                          })
                        : 'Chưa xác định';

                      return (
                        <div key={ver.id} className="relative group">
                          {/* Dot indicator */}
                          <div
                            className={`absolute -left-[31px] top-1.5 w-4 h-4 rounded-full border-2 transition-all ${
                              isCurrent
                                ? 'bg-blue-600 border-white ring-4 ring-blue-100'
                                : 'bg-white border-slate-300 group-hover:border-blue-400'
                            }`}
                          />

                          <div className="rounded-2xl border border-slate-200/90 bg-white p-4 sm:p-5 shadow-2xs hover:border-slate-300 hover:shadow-xs transition-all">
                            <div className="flex flex-col sm:flex-row sm:items-center justify-between gap-3">
                              <div className="space-y-1">
                                <div className="flex items-center gap-2">
                                  <span className="font-mono text-sm font-extrabold text-slate-900">
                                    Phiên bản v{ver.versionNo}
                                  </span>
                                  {isCurrent ? (
                                    <span className="inline-flex items-center gap-1 text-[10px] font-extrabold px-2 py-0.5 rounded-full bg-emerald-50 text-emerald-700 border border-emerald-200">
                                      <CheckCircle2 size={11} />
                                      Hiện tại (Active)
                                    </span>
                                  ) : (
                                    <span className="text-[10px] font-bold px-2 py-0.5 rounded-full bg-slate-100 text-slate-600">
                                      Lịch sử
                                    </span>
                                  )}
                                </div>

                                <div className="flex flex-wrap items-center gap-x-4 gap-y-1 text-xs text-slate-500">
                                  <div className="flex items-center gap-1">
                                    <Clock size={12} className="text-slate-400" />
                                    <span>Xuất bản lúc: {pubDate}</span>
                                  </div>
                                  <div>
                                    <span>Tác giả: ID #{ver.createdByUserId}</span>
                                  </div>
                                </div>
                              </div>

                              {/* Action buttons */}
                              <div className="flex items-center gap-2 shrink-0">
                                <button
                                  type="button"
                                  onClick={() => {
                                    setSelectedVersionNo(ver.versionNo);
                                    setActiveTab('preview');
                                  }}
                                  className="px-3 py-1.5 rounded-xl border border-slate-200 hover:border-blue-300 hover:bg-blue-50 text-slate-700 hover:text-blue-700 text-xs font-bold transition-all flex items-center gap-1.5 cursor-pointer"
                                >
                                  <Grid size={13} />
                                  Xem sơ đồ
                                </button>

                                {!isCurrent && (
                                  <button
                                    type="button"
                                    onClick={() => {
                                      setRollbackTarget(ver);
                                      setRollbackNote(`Khôi phục về phiên bản v${ver.versionNo}`);
                                      setRollbackError(null);
                                    }}
                                    className="px-3 py-1.5 rounded-xl bg-amber-500 hover:bg-amber-600 text-white text-xs font-bold transition-all shadow-xs flex items-center gap-1.5 cursor-pointer active:scale-[0.98]"
                                  >
                                    <RotateCcw size={13} />
                                    Khôi phục (Rollback)
                                  </button>
                                )}
                              </div>
                            </div>

                            {/* Ghi chú thay đổi */}
                            {ver.changeNote ? (
                              <div className="mt-3 pt-3 border-t border-slate-100 text-xs text-slate-700">
                                <span className="font-semibold text-slate-500 mr-1.5">Ghi chú:</span>
                                <span>{ver.changeNote}</span>
                              </div>
                            ) : (
                              <div className="mt-2 text-xs text-slate-400 italic">
                                Không có ghi chú thay đổi.
                              </div>
                            )}
                          </div>
                        </div>
                      );
                    })}
                  </div>
                </div>
              )}
            </div>
          )}

          {/* ============================================================== */}
          {/* TAB 2: XEM MA TRẬN LƯỚI SƠ ĐỒ 2D                               */}
          {/* ============================================================== */}
          {activeTab === 'preview' && (
            <div className="space-y-4">
              {/* Thanh chọn phiên bản muốn xem */}
              <div className="flex flex-col sm:flex-row sm:items-center justify-between gap-3 p-4 rounded-2xl bg-slate-50 border border-slate-200/80">
                <div className="flex items-center gap-2">
                  <span className="text-xs font-bold text-slate-700">Đang xem:</span>
                  <select
                    value={selectedVersionNo ?? ''}
                    onChange={(e) => setSelectedVersionNo(Number(e.target.value))}
                    className="px-3 py-1.5 rounded-xl border border-slate-200 bg-white text-xs font-extrabold text-blue-700 focus:outline-hidden focus:ring-2 focus:ring-blue-500"
                  >
                    {versions.map((v) => (
                      <option key={v.versionNo} value={v.versionNo}>
                        Phiên bản v{v.versionNo} {v.versionNo === latestVersionNo ? '(Bản hiện tại)' : ''}
                      </option>
                    ))}
                  </select>
                </div>

                {layoutDetail && (
                  <div className="flex items-center gap-4 text-xs text-slate-500 font-semibold">
                    <span>
                      Kích thước:{' '}
                      <strong className="text-slate-800">
                        {layoutDetail.columns} × {layoutDetail.rows}
                      </strong>
                    </span>
                    <span>
                      Tổng cells:{' '}
                      <strong className="text-slate-800">
                        {layoutDetail.cells.length}
                      </strong>
                    </span>
                    <span>
                      Ô đỗ:{' '}
                      <strong className="text-emerald-700">
                        {layoutDetail.cells.filter((c) => c.type === 'slot').length} ô
                      </strong>
                    </span>
                  </div>
                )}
              </div>

              {/* Chú giải loại ô */}
              <div className="flex flex-wrap items-center gap-2 text-[11px] font-semibold text-slate-600 bg-white p-2.5 rounded-xl border border-slate-100">
                <span className="text-slate-400 mr-1">Chú giải:</span>
                <span className="inline-flex items-center gap-1 px-2 py-0.5 rounded-md bg-emerald-50 text-emerald-700 border border-emerald-200">
                  <Car size={11} /> Ô đỗ
                </span>
                <span className="inline-flex items-center gap-1 px-2 py-0.5 rounded-md bg-amber-50 text-amber-700 border border-amber-200">
                  <Zap size={11} /> Trụ sạc EV
                </span>
                <span className="inline-flex items-center gap-1 px-2 py-0.5 rounded-md bg-slate-100 text-slate-600">
                  Đường đi
                </span>
                <span className="inline-flex items-center gap-1 px-2 py-0.5 rounded-md bg-teal-50 text-teal-700 border border-teal-200">
                  Cổng vào
                </span>
                <span className="inline-flex items-center gap-1 px-2 py-0.5 rounded-md bg-rose-50 text-rose-700 border border-rose-200">
                  Cổng ra
                </span>
                <span className="inline-flex items-center gap-1 px-2 py-0.5 rounded-md bg-slate-200 text-slate-700">
                  Cột / Tường
                </span>
              </div>

              {loadingLayout ? (
                <div className="py-24 text-center text-xs font-semibold text-slate-500 flex flex-col items-center gap-2">
                  <Loader2 size={24} className="animate-spin text-blue-600" />
                  <span>Đang tải ma trận sơ đồ v{selectedVersionNo}…</span>
                </div>
              ) : layoutError ? (
                <div className="p-4 rounded-2xl bg-rose-50 border border-rose-200 text-rose-700 text-xs font-medium flex items-center gap-2">
                  <AlertTriangle size={16} className="text-rose-500 shrink-0" />
                  <span>{layoutError}</span>
                </div>
              ) : !layoutDetail || layoutDetail.cells.length === 0 ? (
                <div className="py-16 text-center text-xs text-slate-400">
                  Phiên bản này không có dữ liệu cell ma trận.
                </div>
              ) : (
                <div className="relative overflow-x-auto p-4 rounded-2xl bg-slate-100/60 border border-slate-200/90 shadow-inner">
                  {/* Grid Renderer */}
                  <div
                    className="grid gap-1.5 mx-auto"
                    style={{
                      gridTemplateColumns: `repeat(${layoutDetail.columns}, minmax(56px, 1fr))`,
                      width: 'max-content',
                      minWidth: '100%',
                    }}
                  >
                    {/* Render theo từng tọa độ Y từ 0 đến Rows-1 và X từ 0 đến Columns-1 */}
                    {Array.from({ length: layoutDetail.rows }).map((_, y) =>
                      Array.from({ length: layoutDetail.columns }).map((_, x) => {
                        const cell =
                          layoutDetail.cells.find((c) => c.x === x && c.y === y) || {
                            x,
                            y,
                            type: 'empty',
                            widthCells: 1,
                            heightCells: 1,
                          };

                        return (
                          <div
                            key={`${x}-${y}`}
                            onMouseEnter={() => setHoveredCell(cell)}
                            onMouseLeave={() => setHoveredCell(null)}
                            className="aspect-square min-w-[56px] min-h-[56px] transition-transform hover:scale-105 hover:z-10 cursor-pointer"
                          >
                            {renderCellBadge(cell)}
                          </div>
                        );
                      })
                    )}
                  </div>
                </div>
              )}

              {/* Tooltip hiển thị thông tin ô đang trỏ chuột */}
              {hoveredCell && hoveredCell.type === 'slot' && (
                <div className="p-3 rounded-xl bg-slate-900 text-white text-xs flex items-center justify-between shadow-lg">
                  <div className="flex items-center gap-3">
                    <span className="font-extrabold text-amber-400 font-mono">
                      {hoveredCell.code || 'Ô đỗ'}
                    </span>
                    <span>Tọa độ: ({hoveredCell.x}, {hoveredCell.y})</span>
                    <span>Loại: {hoveredCell.slotType || 'Tiêu chuẩn'}</span>
                    <span>Xe: {hoveredCell.maxVehicleType || 'SUV'}</span>
                  </div>
                  {hoveredCell.state && (
                    <span className="px-2 py-0.5 rounded-full bg-slate-800 text-[10px] text-slate-300">
                      Trạng thái: {hoveredCell.state}
                    </span>
                  )}
                </div>
              )}
            </div>
          )}

          {/* ============================================================== */}
          {/* TAB 3: SO SÁNH KHÁC BIỆT DIFF                                  */}
          {/* ============================================================== */}
          {activeTab === 'diff' && (
            <div className="space-y-4">
              {/* Bộ chọn 2 phiên bản để so sánh */}
              <div className="grid grid-cols-1 sm:grid-cols-2 gap-4 p-4 rounded-2xl bg-slate-50 border border-slate-200">
                <div>
                  <label className="block text-xs font-bold text-slate-700 mb-1">
                    Phiên bản gốc (From Version):
                  </label>
                  <select
                    value={diffFromVersion ?? ''}
                    onChange={(e) => setDiffFromVersion(Number(e.target.value))}
                    className="w-full px-3 py-2 rounded-xl border border-slate-200 bg-white text-xs font-semibold text-slate-800"
                  >
                    {versions.map((v) => (
                      <option key={v.versionNo} value={v.versionNo}>
                        Phiên bản v{v.versionNo} ({v.changeNote || 'Không có ghi chú'})
                      </option>
                    ))}
                  </select>
                </div>

                <div>
                  <label className="block text-xs font-bold text-slate-700 mb-1">
                    Phiên bản so sánh (To Version):
                  </label>
                  <select
                    value={diffToVersion ?? ''}
                    onChange={(e) => setDiffToVersion(Number(e.target.value))}
                    className="w-full px-3 py-2 rounded-xl border border-slate-200 bg-white text-xs font-semibold text-slate-800"
                  >
                    {versions.map((v) => (
                      <option key={v.versionNo} value={v.versionNo}>
                        Phiên bản v{v.versionNo} {v.versionNo === latestVersionNo ? '(Bản hiện tại)' : ''}
                      </option>
                    ))}
                  </select>
                </div>
              </div>

              {loadingDiff ? (
                <div className="py-20 text-center text-xs font-semibold text-slate-500 flex flex-col items-center gap-2">
                  <Loader2 size={24} className="animate-spin text-blue-600" />
                  <span>Đang tính toán so sánh khác biệt giữa 2 phiên bản…</span>
                </div>
              ) : diffError ? (
                <div className="p-4 rounded-2xl bg-rose-50 border border-rose-200 text-rose-700 text-xs font-medium flex items-center gap-2">
                  <AlertTriangle size={16} className="text-rose-500 shrink-0" />
                  <span>{diffError}</span>
                </div>
              ) : diffData ? (
                <div className="space-y-4">
                  {/* Thẻ tóm tắt kết quả Diff */}
                  <div className="grid grid-cols-2 sm:grid-cols-4 gap-3">
                    <div className="p-3.5 rounded-2xl bg-white border border-slate-200 shadow-2xs">
                      <span className="text-[10px] font-bold text-slate-400 uppercase tracking-wider block">
                        Kích thước ma trận
                      </span>
                      <div className="text-sm font-extrabold text-slate-800 mt-1">
                        {diffData.oldColumns}×{diffData.oldRows} ➔ {diffData.newColumns}×{diffData.newRows}
                      </div>
                      <span className="text-[10px] text-slate-500">
                        {diffData.gridSizeChanged ? 'Đã đổi cỡ lưới' : 'Giữ nguyên cỡ'}
                      </span>
                    </div>

                    <div className="p-3.5 rounded-2xl bg-emerald-50 border border-emerald-200/80 shadow-2xs">
                      <span className="text-[10px] font-bold text-emerald-700 uppercase tracking-wider block">
                        Ô đỗ thêm mới
                      </span>
                      <div className="text-lg font-extrabold text-emerald-800 mt-0.5 flex items-center gap-1">
                        <Plus size={16} />
                        {diffData.addedSlots.length} ô
                      </div>
                    </div>

                    <div className="p-3.5 rounded-2xl bg-rose-50 border border-rose-200/80 shadow-2xs">
                      <span className="text-[10px] font-bold text-rose-700 uppercase tracking-wider block">
                        Ô đỗ bị gỡ bỏ
                      </span>
                      <div className="text-lg font-extrabold text-rose-800 mt-0.5 flex items-center gap-1">
                        <Minus size={16} />
                        {diffData.removedSlots.length} ô
                      </div>
                    </div>

                    <div className="p-3.5 rounded-2xl bg-amber-50 border border-amber-200/80 shadow-2xs">
                      <span className="text-[10px] font-bold text-amber-700 uppercase tracking-wider block">
                        Ô đỗ sửa đổi
                      </span>
                      <div className="text-lg font-extrabold text-amber-800 mt-0.5 flex items-center gap-1">
                        <Edit3 size={15} />
                        {diffData.modifiedSlots.length} ô
                      </div>
                    </div>
                  </div>

                  {/* Cảnh báo an toàn (Safety Blockers) */}
                  {diffData.safetyBlockers.length > 0 && (
                    <div className="p-4 rounded-2xl bg-rose-50 border border-rose-200 text-rose-800 text-xs">
                      <div className="flex items-center gap-2 font-bold mb-1">
                        <ShieldAlert size={16} className="text-rose-600" />
                        <span>Rủi ro an toàn ngăn chặn áp dụng sơ đồ (Safety Blocker):</span>
                      </div>
                      <ul className="list-disc list-inside space-y-0.5 text-rose-700 ml-1">
                        {diffData.safetyBlockers.map((b, i) => (
                          <li key={i}>{b}</li>
                        ))}
                      </ul>
                    </div>
                  )}

                  {diffData.warnings.length > 0 && (
                    <div className="p-4 rounded-2xl bg-amber-50 border border-amber-200 text-amber-800 text-xs">
                      <div className="flex items-center gap-2 font-bold mb-1">
                        <AlertTriangle size={15} className="text-amber-600" />
                        <span>Cảnh báo lưu ý:</span>
                      </div>
                      <ul className="list-disc list-inside space-y-0.5 text-amber-700 ml-1">
                        {diffData.warnings.map((w, i) => (
                          <li key={i}>{w}</li>
                        ))}
                      </ul>
                    </div>
                  )}

                  {diffData.canSafelyApply && diffData.safetyBlockers.length === 0 && (
                    <div className="p-3.5 rounded-2xl bg-emerald-50 border border-emerald-200 text-emerald-800 text-xs font-semibold flex items-center gap-2">
                      <CheckCircle2 size={16} className="text-emerald-600" />
                      <span>Có thể chuyển đổi hoặc áp dụng giữa 2 phiên bản này an toàn không gây xung đột xe đang đỗ.</span>
                    </div>
                  )}

                  {/* Chi tiết từng nhóm ô đỗ thay đổi */}
                  <div className="space-y-3">
                    {/* Ô thêm mới */}
                    {diffData.addedSlots.length > 0 && (
                      <div className="rounded-2xl border border-emerald-200/90 overflow-hidden">
                        <div className="bg-emerald-50/80 px-4 py-2.5 text-xs font-bold text-emerald-900 border-b border-emerald-100 flex items-center gap-1.5">
                          <Plus size={14} className="text-emerald-600" />
                          <span>Danh sách ô thêm mới ({diffData.addedSlots.length})</span>
                        </div>
                        <div className="p-3 bg-white flex flex-wrap gap-2">
                          {diffData.addedSlots.map((s) => (
                            <span
                              key={s.code}
                              className="px-2.5 py-1 rounded-xl bg-emerald-50 text-emerald-800 border border-emerald-200 text-xs font-mono font-bold"
                            >
                              +{s.code} ({s.newX},{s.newY}) - {s.newSlotType}
                            </span>
                          ))}
                        </div>
                      </div>
                    )}

                    {/* Ô gỡ bỏ */}
                    {diffData.removedSlots.length > 0 && (
                      <div className="rounded-2xl border border-rose-200/90 overflow-hidden">
                        <div className="bg-rose-50/80 px-4 py-2.5 text-xs font-bold text-rose-900 border-b border-rose-100 flex items-center gap-1.5">
                          <Minus size={14} className="text-rose-600" />
                          <span>Danh sách ô bị gỡ bỏ ({diffData.removedSlots.length})</span>
                        </div>
                        <div className="p-3 bg-white flex flex-wrap gap-2">
                          {diffData.removedSlots.map((s) => (
                            <span
                              key={s.code}
                              className={`px-2.5 py-1 rounded-xl text-xs font-mono font-bold border ${
                                s.isSafetyBlocker
                                  ? 'bg-rose-100 text-rose-900 border-rose-300 ring-2 ring-rose-400'
                                  : 'bg-rose-50 text-rose-800 border-rose-200'
                              }`}
                            >
                              -{s.code} ({s.oldX},{s.oldY}) {s.isSafetyBlocker ? '⚠️ Có xe/Booking' : ''}
                            </span>
                          ))}
                        </div>
                      </div>
                    )}

                    {/* Ô sửa đổi */}
                    {diffData.modifiedSlots.length > 0 && (
                      <div className="rounded-2xl border border-amber-200/90 overflow-hidden">
                        <div className="bg-amber-50/80 px-4 py-2.5 text-xs font-bold text-amber-900 border-b border-amber-100 flex items-center gap-1.5">
                          <Edit3 size={14} className="text-amber-600" />
                          <span>Danh sách ô điều chỉnh ({diffData.modifiedSlots.length})</span>
                        </div>
                        <div className="p-3 bg-white space-y-1.5">
                          {diffData.modifiedSlots.map((s) => (
                            <div
                              key={s.code}
                              className="text-xs text-slate-700 flex items-center justify-between p-2 rounded-xl bg-slate-50"
                            >
                              <span className="font-mono font-bold text-amber-700">{s.code}</span>
                              <span className="text-slate-500">
                                Vị trí: ({s.oldX},{s.oldY}) ➔ ({s.newX},{s.newY}) | Loại: {s.oldSlotType} ➔ {s.newSlotType}
                              </span>
                            </div>
                          ))}
                        </div>
                      </div>
                    )}
                  </div>
                </div>
              ) : null}
            </div>
          )}
        </div>

        {/* Modal Footer */}
        <div className="p-4 sm:p-5 border-t border-slate-100 bg-slate-50 flex items-center justify-between">
          <div className="flex items-center gap-2 text-xs text-slate-500">
            <Info size={14} className="text-slate-400" />
            <span>Đồng bộ hóa an toàn ô đỗ vật lý tự động kích hoạt khi khôi phục</span>
          </div>

          <button
            type="button"
            onClick={onClose}
            className="px-4 py-2 rounded-xl bg-slate-200 hover:bg-slate-300 text-slate-700 text-xs font-bold transition-all cursor-pointer"
          >
            Đóng
          </button>
        </div>
      </div>

      {/* Dialog con xác nhận Rollback */}
      {rollbackTarget && (
        <div className="fixed inset-0 z-60 flex items-center justify-center p-4 bg-slate-950/70 backdrop-blur-xs">
          <div className="w-full max-w-md rounded-3xl bg-white border border-slate-200 p-6 shadow-2xl space-y-4">
            <div className="flex items-center gap-3">
              <div className="w-11 h-11 rounded-2xl bg-amber-50 text-amber-600 flex items-center justify-center shrink-0">
                <RotateCcw size={22} />
              </div>
              <div>
                <h3 className="text-base font-bold text-slate-900">
                  Xác nhận khôi phục sơ đồ?
                </h3>
                <p className="text-xs text-slate-500 mt-0.5">
                  Tầng: <strong className="text-slate-800">{floor.name}</strong> về{' '}
                  <strong className="text-amber-600">Phiên bản v{rollbackTarget.versionNo}</strong>
                </p>
              </div>
            </div>

            {rollbackError && (
              <div className="p-3.5 rounded-xl bg-rose-50 border border-rose-200 text-rose-700 text-xs flex items-center gap-2">
                <AlertTriangle size={15} className="text-rose-500 shrink-0" />
                <span>{rollbackError}</span>
              </div>
            )}

            <div>
              <label className="block text-xs font-bold text-slate-700 mb-1">
                Lý do khôi phục (Change note):
              </label>
              <textarea
                value={rollbackNote}
                onChange={(e) => setRollbackNote(e.target.value)}
                placeholder="Nhập lý do khôi phục về phiên bản cũ..."
                rows={3}
                className="w-full px-3 py-2 rounded-xl border border-slate-200 text-xs text-slate-800 focus:outline-hidden focus:ring-2 focus:ring-blue-500"
              />
            </div>

            <div className="flex items-center justify-end gap-2 pt-2">
              <button
                type="button"
                disabled={rollbackBusy}
                onClick={() => setRollbackTarget(null)}
                className="px-4 py-2 rounded-xl text-slate-600 hover:bg-slate-100 text-xs font-bold transition-all cursor-pointer"
              >
                Hủy bỏ
              </button>

              <button
                type="button"
                disabled={rollbackBusy}
                onClick={handleExecuteRollback}
                className="px-4 py-2 rounded-xl bg-amber-600 hover:bg-amber-700 text-white text-xs font-bold transition-all shadow-xs flex items-center gap-1.5 cursor-pointer disabled:opacity-50"
              >
                {rollbackBusy ? (
                  <>
                    <Loader2 size={14} className="animate-spin" />
                    Đang khôi phục…
                  </>
                ) : (
                  <>
                    <RotateCcw size={14} />
                    Xác nhận Khôi phục
                  </>
                )}
              </button>
            </div>
          </div>
        </div>
      )}
    </div>
  );
};
