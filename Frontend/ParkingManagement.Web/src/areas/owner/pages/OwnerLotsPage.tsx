import React, { useEffect, useState, useMemo, useCallback } from 'react';
import { useSearchParams } from 'react-router-dom';
import {
  Building2,
  Plus,
  Layers,
  Sparkles,
  RefreshCw,
  FolderTree,
  Sliders,
  Activity,
} from 'lucide-react';
import { useAppSelector } from '@/store';
import { ownerLotsApi } from '@/api/ownerLotsApi';
import { ownerStructureApi } from '@/api/ownerStructureApi';
import { ownerCapacityApi } from '@/api/ownerCapacityApi';
import type { OwnerParkingLotItemDto } from '@/types/ownerKyb';
import type {
  ZoneDto,
  FloorDto,
  CreateZoneRequest,
  UpdateZoneRequest,
  CreateFloorRequest,
  UpdateFloorRequest,
  ParkingLotHierarchyDto,
} from '@/types/ownerStructure';
import type {
  LotCapacityConfigDto,
  UpdateLotCapacityConfigRequest,
} from '@/types/ownerCapacity';
import { ZoneCard } from '../components/structure/ZoneCard';
import { ZoneModal } from '../components/structure/ZoneModal';
import { FloorTable } from '../components/structure/FloorTable';
import { FloorModal } from '../components/structure/FloorModal';
import { DeleteConfirmModal } from '../components/structure/DeleteConfirmModal';
import { RegisterLotModal } from '../components/RegisterLotModal';
import { CapacitySummaryCards } from '../components/capacity/CapacitySummaryCards';
import { EmergencyStopAlert } from '../components/capacity/EmergencyStopAlert';
import { CapacityConfigSection } from '../components/capacity/CapacityConfigSection';
import { OperationsPanel } from '../components/operations/OperationsPanel';

export const OwnerLotsPage: React.FC = () => {
  const [searchParams, setSearchParams] = useSearchParams();
  const { user } = useAppSelector((s) => s.auth);
  const ownerProfileId = user?.id || 2;

  // Danh sách bãi đỗ
  const [lots, setLots] = useState<OwnerParkingLotItemDto[]>([]);
  const [selectedLotId, setSelectedLotId] = useState<number | null>(null);
  const [loadingLots, setLoadingLots] = useState(true);
  const [isRegisterModalOpen, setIsRegisterModalOpen] = useState(false);

  // Tab điều hướng: 'structure' (US-058) | 'capacity' (UI-26) | 'operations' (US-067/068/069)
  const [activeTab, setActiveTab] = useState<'structure' | 'capacity' | 'operations'>(() => {
    const tabParam = searchParams.get('tab');
    return tabParam === 'capacity' || tabParam === 'operations' ? tabParam : 'structure';
  });

  // Cấu hình Sức chứa & Vùng đệm vãng lai (UI-26)
  const [capacityConfig, setCapacityConfig] = useState<LotCapacityConfigDto | null>(null);
  const [loadingCapacity, setLoadingCapacity] = useState(false);
  const [savingCapacity, setSavingCapacity] = useState(false);

  // Cấu trúc phân tầng
  const [zones, setZones] = useState<ZoneDto[]>([]);
  const [selectedZoneId, setSelectedZoneId] = useState<number | null>(null);
  const [floors, setFloors] = useState<FloorDto[]>([]);
  const [loadingZones, setLoadingZones] = useState(false);
  const [loadingFloors, setLoadingFloors] = useState(false);

  // Hierarchy tổng quan (để đếm tổng số tầng của từng Zone)
  const [hierarchy, setHierarchy] = useState<ParkingLotHierarchyDto | null>(null);

  // Trạng thái Modals
  const [isZoneModalOpen, setIsZoneModalOpen] = useState(false);
  const [editingZone, setEditingZone] = useState<ZoneDto | null>(null);

  const [isFloorModalOpen, setIsFloorModalOpen] = useState(false);
  const [editingFloor, setEditingFloor] = useState<FloorDto | null>(null);

  const [deleteTarget, setDeleteTarget] = useState<{
    type: 'zone' | 'floor';
    id: number;
    name: string;
  } | null>(null);

  // Thông báo phản hồi
  const [actionMessage, setActionMessage] = useState<{
    type: 'success' | 'error';
    text: string;
  } | null>(null);

  // 1. Tải danh sách bãi đỗ của chủ bãi
  const loadOwnerLots = useCallback(async (autoSelectId?: number) => {
    setLoadingLots(true);
    try {
      const data = await ownerLotsApi.getMyLots(ownerProfileId);
      setLots(data);
      if (data.length > 0) {
        const queryLotId = searchParams.get('lotId');
        const targetId = autoSelectId
          ? autoSelectId
          : queryLotId
            ? parseInt(queryLotId, 10)
            : data[0].id;
        setSelectedLotId(targetId);
      } else {
        setSelectedLotId(null);
      }
    } catch {
      // Mock data fallback nếu backend chưa chạy
      const mockLots: OwnerParkingLotItemDto[] = [
        {
          id: 4,
          name: 'Bãi xe Chợ Bến Thành (đêm)',
          address: 'Lê Lợi, Q.1, TP.HCM',
          city: 'TP.HCM',
          district: 'Quận 1',
          totalSlots: 20,
          availableSlots: 11,
          maxHeightCm: 300,
          status: 'Active',
          openingHours: '18:00 - 07:00',
          createdAtUtc: new Date().toISOString(),
        },
      ];
      setLots(mockLots);
      setSelectedLotId(4);
    } finally {
      setLoadingLots(false);
    }
  }, [ownerProfileId, searchParams]);

  useEffect(() => {
    void loadOwnerLots();
  }, [loadOwnerLots]);

  // Bãi đỗ đang chọn
  const selectedLot = useMemo(
    () => lots.find((l) => l.id === selectedLotId) || null,
    [lots, selectedLotId]
  );

  // 2. Tải danh sách Zones và Cây phân cấp khi thay đổi bãi đỗ
  const loadStructureData = useCallback(async (lotId: number) => {
    setLoadingZones(true);
    setActionMessage(null);
    try {
      const [zonesData, hierarchyData] = await Promise.allSettled([
        ownerStructureApi.getZones(lotId),
        ownerStructureApi.getHierarchy(lotId),
      ]);

      if (zonesData.status === 'fulfilled') {
        setZones(zonesData.value);
        if (zonesData.value.length > 0) {
          // Tự động chọn Zone đầu tiên nếu chưa chọn hoặc id cũ không còn
          setSelectedZoneId((prev) => {
            const exists = zonesData.value.some((z) => z.id === prev);
            return exists ? prev : zonesData.value[0].id;
          });
        } else {
          setSelectedZoneId(null);
          setFloors([]);
        }
      }

      if (hierarchyData.status === 'fulfilled') {
        setHierarchy(hierarchyData.value);
      }
    } catch {
      setActionMessage({
        type: 'error',
        text: 'Không thể tải cấu trúc bãi đỗ xe. Vui lòng thử lại.',
      });
    } finally {
      setLoadingZones(false);
    }
  }, []);

  // Tải cấu hình sức chứa
  const loadCapacityConfig = useCallback(
    async (lotId: number) => {
      setLoadingCapacity(true);
      try {
        const config = await ownerCapacityApi.getCapacityConfig(lotId, ownerProfileId);
        setCapacityConfig(config);
      } catch (err) {
        console.error('Failed to fetch capacity config, using fallback:', err);
        const currentLot = lots.find((l) => l.id === lotId);
        const total = currentLot?.totalSlots || 20;
        setCapacityConfig({
          parkingLotId: lotId,
          parkingLotName: currentLot?.name || 'Bãi đỗ xe',
          totalSlots: total,
          walkInBufferPercent: 30,
          calculatedWalkInBufferSlots: Math.round(total * 0.3),
          onlineQuota: Math.max(0, total - Math.round(total * 0.3)),
          maxAllowedOnlineQuota: Math.max(0, total - Math.round(total * 0.3)),
          onlineLockThreshold: 5,
          instantBookingPaused: false,
          integrationTier: 'Manual',
          isEmergencyStopped: false,
        });
      } finally {
        setLoadingCapacity(false);
      }
    },
    [ownerProfileId, lots]
  );

  // Đồng bộ searchParams và load dữ liệu khi bãi xe hoặc tab thay đổi
  useEffect(() => {
    if (selectedLotId) {
      setSearchParams((prev) => {
        const next = new URLSearchParams(prev);
        next.set('lotId', selectedLotId.toString());
        next.set('tab', activeTab);
        return next;
      });
      if (activeTab === 'structure') {
        void loadStructureData(selectedLotId);
      } else {
        void loadCapacityConfig(selectedLotId);
      }
    }
  }, [selectedLotId, activeTab, loadStructureData, loadCapacityConfig, setSearchParams]);

  const handleTabChange = (tab: 'structure' | 'capacity' | 'operations') => {
    setActiveTab(tab);
    if (selectedLotId) {
      setSearchParams((prev) => {
        const next = new URLSearchParams(prev);
        next.set('lotId', selectedLotId.toString());
        next.set('tab', tab);
        return next;
      });
      if (tab === 'capacity' && capacityConfig?.parkingLotId !== selectedLotId) {
        void loadCapacityConfig(selectedLotId);
      } else if (tab === 'structure' && zones.length === 0) {
        void loadStructureData(selectedLotId);
      }
    }
  };

  // Cập nhật cấu hình sức chứa
  const handleSaveCapacity = async (payload: UpdateLotCapacityConfigRequest) => {
    if (!selectedLotId) return;
    setSavingCapacity(true);
    try {
      const updated = await ownerCapacityApi.updateCapacityConfig(selectedLotId, payload);
      setCapacityConfig(updated);
      setActionMessage({
        type: 'success',
        text: 'Cập nhật cấu hình sức chứa & vùng đệm vãng lai thành công!',
      });
    } catch (err: unknown) {
      const apiError = err as { response?: { data?: { detail?: string; title?: string } } };
      const errMsg =
        apiError?.response?.data?.detail ||
        apiError?.response?.data?.title ||
        'Không thể lưu cấu hình sức chứa. Vui lòng kiểm tra lại.';
      setActionMessage({
        type: 'error',
        text: errMsg,
      });
      throw err;
    } finally {
      setSavingCapacity(false);
    }
  };

  // 3. Tải danh sách Tầng khi thay đổi Zone đang chọn
  const loadFloorsOfZone = useCallback(async (zoneId: number) => {
    setLoadingFloors(true);
    try {
      const data = await ownerStructureApi.getFloors(zoneId);
      setFloors(data);
    } catch {
      setFloors([]);
    } finally {
      setLoadingFloors(false);
    }
  }, []);

  useEffect(() => {
    if (selectedZoneId) {
      void loadFloorsOfZone(selectedZoneId);
    } else {
      setFloors([]);
    }
  }, [selectedZoneId, loadFloorsOfZone]);

  // Zone đang được chọn
  const selectedZone = useMemo(
    () => zones.find((z) => z.id === selectedZoneId) || null,
    [zones, selectedZoneId]
  );

  // Đếm số tầng của từng Zone dựa trên cây Hierarchy
  const zoneFloorCounts = useMemo(() => {
    const map = new Map<number, number>();
    if (hierarchy?.zones) {
      hierarchy.zones.forEach((z) => {
        map.set(z.id, z.floors.length);
      });
    }
    return map;
  }, [hierarchy]);

  // ==================== HÀNH ĐỘNG VỚI ZONE ====================

  const handleOpenAddZone = () => {
    setEditingZone(null);
    setIsZoneModalOpen(true);
  };

  const handleOpenEditZone = (zone: ZoneDto) => {
    setEditingZone(zone);
    setIsZoneModalOpen(true);
  };

  const handleSaveZone = async (payload: CreateZoneRequest | UpdateZoneRequest) => {
    if (!selectedLotId) return;

    if (editingZone) {
      const updated = await ownerStructureApi.updateZone(
        editingZone.id,
        payload as UpdateZoneRequest
      );
      setZones((prev) => prev.map((z) => (z.id === updated.id ? updated : z)));
      setActionMessage({
        type: 'success',
        text: `Đã cập nhật khu vực "${updated.name}" thành công!`,
      });
    } else {
      const created = await ownerStructureApi.createZone(
        selectedLotId,
        payload as CreateZoneRequest
      );
      setZones((prev) => [...prev, created]);
      setSelectedZoneId(created.id);
      setActionMessage({
        type: 'success',
        text: `Đã tạo khu vực "${created.name}" thành công!`,
      });
    }
    if (selectedLotId) void loadStructureData(selectedLotId);
  };

  const handleDeleteZoneClick = (zone: ZoneDto) => {
    setDeleteTarget({
      type: 'zone',
      id: zone.id,
      name: `Khu vực: ${zone.code} - ${zone.name}`,
    });
  };

  // ==================== HÀNH ĐỘNG VỚI FLOOR ====================

  const handleOpenAddFloor = () => {
    setEditingFloor(null);
    setIsFloorModalOpen(true);
  };

  const handleOpenEditFloor = (floor: FloorDto) => {
    setEditingFloor(floor);
    setIsFloorModalOpen(true);
  };

  const handleSaveFloor = async (payload: CreateFloorRequest | UpdateFloorRequest) => {
    if (!selectedZoneId) return;

    if (editingFloor) {
      const updated = await ownerStructureApi.updateFloor(
        editingFloor.id,
        payload as UpdateFloorRequest
      );
      setFloors((prev) => prev.map((f) => (f.id === updated.id ? updated : f)));
      setActionMessage({
        type: 'success',
        text: `Đã cập nhật thông số tầng "${updated.name}" thành công!`,
      });
    } else {
      const created = await ownerStructureApi.createFloor(
        selectedZoneId,
        payload as CreateFloorRequest
      );
      setFloors((prev) => [...prev, created]);
      setActionMessage({
        type: 'success',
        text: `Đã khai báo tầng "${created.name}" thành công!`,
      });
    }
    if (selectedLotId) void loadStructureData(selectedLotId);
  };

  const handleDeleteFloorClick = (floor: FloorDto) => {
    setDeleteTarget({
      type: 'floor',
      id: floor.id,
      name: `Tầng: ${floor.name} (Level ${floor.level})`,
    });
  };

  // ==================== XÁC NHẬN XÓA ====================

  const handleConfirmDelete = async () => {
    if (!deleteTarget) return;

    if (deleteTarget.type === 'zone') {
      await ownerStructureApi.deleteZone(deleteTarget.id);
      setZones((prev) => prev.filter((z) => z.id !== deleteTarget.id));
      if (selectedZoneId === deleteTarget.id) {
        setSelectedZoneId(null);
      }
      setActionMessage({
        type: 'success',
        text: 'Đã xóa khu vực thành công!',
      });
    } else {
      await ownerStructureApi.deleteFloor(deleteTarget.id);
      setFloors((prev) => prev.filter((f) => f.id !== deleteTarget.id));
      setActionMessage({
        type: 'success',
        text: 'Đã xóa tầng thành công!',
      });
    }
    if (selectedLotId) void loadStructureData(selectedLotId);
  };

  return (
    <div className="max-w-7xl mx-auto px-4 sm:px-6 lg:px-8 py-8 sm:py-10">
      {/* =====================================================================
          1. HEADER TRANG & BỘ CHỌN BÃI ĐỖ XE
      ====================================================================== */}
      <div className="flex flex-col md:flex-row md:items-center justify-between gap-4 pb-6 border-b border-slate-200">
        <div>
          <div className="inline-flex items-center gap-2 px-3 py-1 rounded-full bg-indigo-100 text-indigo-800 text-xs font-semibold uppercase tracking-wider mb-2">
            {activeTab === 'structure' ? (
              <>
                <Building2 size={13} />
                Hạ tầng & Cấu trúc Phân tầng (US-058)
              </>
            ) : (
              <>
                <Sliders size={13} />
                Sức chứa & Phân bổ Real-time (UI-26)
              </>
            )}
          </div>
          <h1 className="text-2xl sm:text-3xl font-extrabold text-slate-900 tracking-tight">
            {activeTab === 'structure'
              ? 'Khai báo Tầng & Khu vực (Zones & Floors)'
              : 'Cấu hình Sức chứa & Vùng đệm Vãng lai'}
          </h1>
          <p className="text-slate-500 text-xs sm:text-sm mt-1">
            {activeTab === 'structure'
              ? 'Thiết lập kiến trúc không gian bãi xe: Phân chia khu vực (tòa nhà / hầm), cao độ tầng và ma trận lưới ô đỗ.'
              : 'Quản lý tỷ lệ giữ chỗ cho khách vãng lai, phân bổ hạn ngạch đặt chỗ Online và thiết lập ngưỡng khóa an toàn.'}
          </p>
        </div>

        {/* Hành động: Chọn bãi đỗ & Thêm bãi mới */}
        <div className="flex items-center gap-2.5 flex-wrap">
          {lots.length > 0 && (
            <div className="relative">
              <select
                disabled={loadingLots}
                value={selectedLotId || ''}
                onChange={(e) => setSelectedLotId(parseInt(e.target.value, 10))}
                className="px-3.5 py-2.5 rounded-xl bg-white border border-slate-200 text-xs font-bold text-slate-800 shadow-2xs focus:outline-hidden focus:border-indigo-600 pr-8"
              >
                {lots.map((lot) => (
                  <option key={lot.id} value={lot.id}>
                    {lot.name} ({lot.totalSlots} chỗ)
                  </option>
                ))}
              </select>
            </div>
          )}

          <button
            type="button"
            onClick={() => setIsRegisterModalOpen(true)}
            className="px-4 py-2.5 rounded-xl bg-indigo-600 hover:bg-indigo-700 text-white text-xs font-bold shadow-md shadow-indigo-600/20 active:scale-[0.98] transition-all flex items-center gap-1.5 cursor-pointer shrink-0"
          >
            <Plus size={15} />
            Đăng Ký Bãi Mới
          </button>
        </div>
      </div>

      {/* Thông báo kết quả thao tác */}
      {actionMessage && (
        <div
          className={`mt-4 p-4 rounded-2xl border text-xs font-semibold flex items-center justify-between ${
            actionMessage.type === 'success'
              ? 'bg-emerald-50 text-emerald-800 border-emerald-200'
              : 'bg-rose-50 text-rose-800 border-rose-200'
          }`}
        >
          <span>{actionMessage.text}</span>
          <button
            type="button"
            onClick={() => setActionMessage(null)}
            className="text-slate-400 hover:text-slate-700 text-xs cursor-pointer ml-3"
          >
            Đóng
          </button>
        </div>
      )}

      {/* Trường hợp Chủ bãi chưa có bất kỳ bãi xe nào */}
      {lots.length === 0 && !loadingLots && (
        <div className="mt-8 rounded-3xl bg-white/90 border border-slate-200/90 p-10 text-center shadow-xs">
          <div className="w-16 h-16 rounded-2xl bg-indigo-50 text-indigo-600 flex items-center justify-center mx-auto mb-4">
            <Building2 size={32} />
          </div>
          <h2 className="text-xl font-bold text-slate-900">
            Bạn chưa đăng ký bãi đỗ xe nào
          </h2>
          <p className="text-xs sm:text-sm text-slate-500 max-w-md mx-auto mt-2 leading-relaxed">
            Trước khi khai báo tầng và khu vực, vui lòng tạo cơ sở bãi đỗ xe đầu tiên của bạn trên hệ thống.
          </p>
          <button
            type="button"
            onClick={() => setIsRegisterModalOpen(true)}
            className="mt-6 px-6 py-3 rounded-xl bg-indigo-600 hover:bg-indigo-700 text-white text-xs font-bold shadow-md shadow-indigo-600/25 transition-all inline-flex items-center gap-2 cursor-pointer"
          >
            <Plus size={16} />
            Khai báo Bãi đỗ xe đầu tiên
          </button>
        </div>
      )}

      {/* =====================================================================
          TAB NAVIGATION SWITCHER (US-058 vs UI-26)
      ====================================================================== */}
      {selectedLot && (
        <div className="flex items-center gap-2 mt-6 p-1.5 bg-slate-100/90 rounded-2xl w-fit border border-slate-200/70 shadow-2xs">
          <button
            type="button"
            onClick={() => handleTabChange('structure')}
            className={`px-4 py-2 rounded-xl text-xs sm:text-sm font-bold flex items-center gap-2 transition-all cursor-pointer ${
              activeTab === 'structure'
                ? 'bg-white text-indigo-700 shadow-xs'
                : 'text-slate-600 hover:text-slate-900 hover:bg-white/50'
            }`}
          >
            <FolderTree size={16} />
            Sơ đồ & Phân tầng (US-058)
          </button>

          <button
            type="button"
            onClick={() => handleTabChange('capacity')}
            className={`px-4 py-2 rounded-xl text-xs sm:text-sm font-bold flex items-center gap-2 transition-all cursor-pointer ${
              activeTab === 'capacity'
                ? 'bg-white text-indigo-700 shadow-xs'
                : 'text-slate-600 hover:text-slate-900 hover:bg-white/50'
            }`}
          >
            <Sliders size={16} />
            Sức chứa & Vùng đệm (UI-26)
          </button>

          <button
            type="button"
            onClick={() => handleTabChange('operations')}
            className={`px-4 py-2 rounded-xl text-xs sm:text-sm font-bold flex items-center gap-2 transition-all cursor-pointer ${
              activeTab === 'operations'
                ? 'bg-white text-indigo-700 shadow-xs'
                : 'text-slate-600 hover:text-slate-900 hover:bg-white/50'
            }`}
          >
            <Activity size={16} />
            Vận hành Real-time (US-067)
          </button>
        </div>
      )}

      {/* =====================================================================
          2. TAB 1 CONTENT: CẤU TRÚC PHÂN TẦNG (ZONES & FLOORS)
      ====================================================================== */}
      {selectedLot && activeTab === 'structure' && (
        <>
          {/* Thống kê tóm tắt bãi đỗ xe đang chọn */}
          <div className="grid grid-cols-2 sm:grid-cols-4 gap-4 mt-6">
            <div className="p-4 rounded-2xl bg-white border border-slate-200/80 shadow-2xs">
              <span className="text-[11px] font-bold text-slate-400 uppercase tracking-wider">Tổng Khu vực</span>
              <div className="text-xl font-extrabold text-slate-900 mt-1 flex items-center gap-1.5">
                <FolderTree size={18} className="text-indigo-600" />
                <span>{zones.length} Zone</span>
              </div>
            </div>

            <div className="p-4 rounded-2xl bg-white border border-slate-200/80 shadow-2xs">
              <span className="text-[11px] font-bold text-slate-400 uppercase tracking-wider">Tổng số Tầng</span>
              <div className="text-xl font-extrabold text-slate-900 mt-1 flex items-center gap-1.5">
                <Layers size={18} className="text-purple-600" />
                <span>
                  {hierarchy
                    ? hierarchy.zones.reduce((sum, z) => sum + z.floors.length, 0)
                    : floors.length}{' '}
                  Tầng
                </span>
              </div>
            </div>

            <div className="p-4 rounded-2xl bg-white border border-slate-200/80 shadow-2xs">
              <span className="text-[11px] font-bold text-slate-400 uppercase tracking-wider">Quy mô cơ sở</span>
              <div className="text-xl font-extrabold text-slate-900 mt-1 flex items-center gap-1.5">
                <Sparkles size={18} className="text-amber-500" />
                <span>{selectedLot.totalSlots} ô đỗ</span>
              </div>
            </div>

            <div className="p-4 rounded-2xl bg-white border border-slate-200/80 shadow-2xs">
              <span className="text-[11px] font-bold text-slate-400 uppercase tracking-wider">Trạng thái bãi</span>
              <div className="mt-1">
                <span className="inline-flex items-center gap-1 text-xs font-bold px-2.5 py-0.5 rounded-full bg-emerald-50 text-emerald-700 border border-emerald-200">
                  <span className="w-1.5 h-1.5 rounded-full bg-emerald-500"></span>
                  {selectedLot.status}
                </span>
              </div>
            </div>
          </div>

          {/* BỐ CỤC 2 CỘT MASTER-DETAIL (ZONES <-> FLOORS) */}
          <div className="grid grid-cols-1 lg:grid-cols-12 gap-6 mt-6">
            {/* CỘT TRÁI (4 cols): Quản lý Khu vực (Zones) */}
            <div className="lg:col-span-4 space-y-4">
              <div className="rounded-3xl bg-white border border-slate-200/90 p-5 shadow-xs">
                <div className="flex items-center justify-between pb-4 border-b border-slate-100">
                  <div>
                    <h2 className="text-sm font-bold text-slate-900">Khu vực (Zones)</h2>
                    <p className="text-[11px] text-slate-500">Bấm vào khu vực để xem các tầng</p>
                  </div>
                  <button
                    type="button"
                    onClick={handleOpenAddZone}
                    className="px-3 py-1.5 rounded-xl bg-indigo-50 hover:bg-indigo-100 text-indigo-700 text-xs font-bold transition-colors flex items-center gap-1 cursor-pointer"
                  >
                    <Plus size={14} />
                    Thêm Zone
                  </button>
                </div>

                {/* Danh sách Zone Cards */}
                <div className="mt-4 space-y-3">
                  {loadingZones ? (
                    <div className="py-8 text-center text-xs text-slate-400">
                      <RefreshCw size={18} className="animate-spin mx-auto mb-2 text-indigo-600" />
                      Đang tải danh sách khu vực…
                    </div>
                  ) : zones.length === 0 ? (
                    <div className="p-6 text-center text-slate-400 text-xs">
                      Chưa có khu vực nào. Hãy nhấn <strong>"+ Thêm Zone"</strong> bên trên.
                    </div>
                  ) : (
                    zones.map((zone) => (
                      <ZoneCard
                        key={zone.id}
                        zone={zone}
                        isSelected={selectedZoneId === zone.id}
                        floorCount={zoneFloorCounts.get(zone.id) || 0}
                        onSelect={(z) => setSelectedZoneId(z.id)}
                        onEdit={handleOpenEditZone}
                        onDelete={handleDeleteZoneClick}
                      />
                    ))
                  )}
                </div>
              </div>
            </div>

            {/* CỘT PHẢI (8 cols): Quản lý Tầng (Floors) thuộc Zone đang chọn */}
            <div className="lg:col-span-8">
              {selectedZone ? (
                <FloorTable
                  floors={floors}
                  zoneName={`${selectedZone.code} - ${selectedZone.name}`}
                  loading={loadingFloors}
                  onAddFloor={handleOpenAddFloor}
                  onEditFloor={handleOpenEditFloor}
                  onDeleteFloor={handleDeleteFloorClick}
                />
              ) : (
                <div className="rounded-3xl bg-white border border-slate-200/90 p-12 text-center shadow-xs">
                  <div className="w-14 h-14 rounded-2xl bg-indigo-50 text-indigo-600 flex items-center justify-center mx-auto mb-3">
                    <Layers size={28} />
                  </div>
                  <h3 className="text-sm font-bold text-slate-900">
                    Chưa chọn khu vực
                  </h3>
                  <p className="text-xs text-slate-500 max-w-sm mx-auto mt-1">
                    Vui lòng chọn một Khu vực (Zone) ở cột bên trái hoặc bấm "Thêm Zone" để bắt đầu quản lý danh sách tầng.
                  </p>
                </div>
              )}
            </div>
          </div>
        </>
      )}

      {/* =====================================================================
          3. TAB 2 CONTENT: CẤU HÌNH SỨC CHỨA & VÙNG ĐỆM VÃNG LAI (UI-26)
      ====================================================================== */}
      {selectedLot && activeTab === 'capacity' && (
        <div className="mt-6 space-y-6">
          {loadingCapacity ? (
            <div className="py-16 text-center text-xs sm:text-sm text-slate-500 bg-white rounded-3xl border border-slate-200/90 shadow-xs">
              <RefreshCw size={24} className="animate-spin mx-auto mb-3 text-indigo-600" />
              Đang tải cấu hình sức chứa và thông số kỹ thuật bãi đỗ…
            </div>
          ) : capacityConfig ? (
            <>
              {/* Cảnh báo Dừng khẩn cấp nếu có */}
              <EmergencyStopAlert
                isEmergencyStopped={capacityConfig.isEmergencyStopped}
                emergencyReason={capacityConfig.emergencyReason}
              />

              {/* 4 Thẻ KPI Tóm Tắt */}
              <CapacitySummaryCards config={capacityConfig} />

              {/* Bảng điều khiển cấu hình chính với thanh phân bổ động & preset */}
              <CapacityConfigSection
                config={capacityConfig}
                saving={savingCapacity}
                onSave={handleSaveCapacity}
                onRefresh={() => loadCapacityConfig(selectedLot.id)}
              />
            </>
          ) : (
            <div className="p-8 text-center text-slate-400 text-xs bg-white rounded-3xl border border-slate-200">
              Không tìm thấy cấu hình sức chứa cho bãi xe này.
            </div>
          )}
        </div>
      )}

      {/* =====================================================================
          3. TAB 3 CONTENT: VẬN HÀNH REAL-TIME (US-067 dashboard / US-068 heartbeat / US-069 emergency)
      ====================================================================== */}
      {selectedLot && activeTab === 'operations' && (
        <OperationsPanel
          key={selectedLot.id}
          lotId={selectedLot.id}
          lotName={selectedLot.name}
          onLotChanged={() => { void loadOwnerLots(selectedLot.id); }}
        />
      )}

      {/* =====================================================================
          4. CÁC MODALS THAO TÁC
      ====================================================================== */}
      {/* Modal Thêm / Sửa Zone */}
      <ZoneModal
        isOpen={isZoneModalOpen}
        onClose={() => setIsZoneModalOpen(false)}
        lotId={selectedLotId || 0}
        initialData={editingZone}
        onSubmit={handleSaveZone}
      />

      {/* Modal Thêm / Sửa Floor */}
      <FloorModal
        isOpen={isFloorModalOpen}
        onClose={() => setIsFloorModalOpen(false)}
        zoneId={selectedZoneId || 0}
        zoneName={selectedZone ? `${selectedZone.code} - ${selectedZone.name}` : ''}
        initialData={editingFloor}
        onSubmit={handleSaveFloor}
      />

      {/* Modal Xác Nhận Xóa */}
      <DeleteConfirmModal
        isOpen={!!deleteTarget}
        onClose={() => setDeleteTarget(null)}
        title={deleteTarget?.type === 'zone' ? 'Xác nhận xóa Khu vực (Zone)' : 'Xác nhận xóa Tầng (Floor)'}
        description={
          deleteTarget?.type === 'zone'
            ? 'Bạn có chắc chắn muốn xóa khu vực này? Hành động này sẽ thất bại nếu khu vực còn tầng đỗ xe trực thuộc.'
            : 'Bạn có chắc chắn muốn xóa tầng này? Hành động này sẽ thất bại nếu tầng còn ô đỗ xe trực thuộc.'
        }
        itemName={deleteTarget?.name || ''}
        onConfirm={handleConfirmDelete}
      />

      {/* Modal Đăng Ký Bãi Xe Mới */}
      <RegisterLotModal
        isOpen={isRegisterModalOpen}
        onClose={() => setIsRegisterModalOpen(false)}
        ownerProfileId={ownerProfileId}
        onSuccess={(newLotId, lotName) => {
          loadOwnerLots(newLotId);
          setActionMessage({
            type: 'success',
            text: `Đã đăng ký cơ sở bãi đỗ "${lotName}" thành công! Vui lòng khai báo Zone và Floor bên dưới.`,
          });
        }}
      />
    </div>
  );
};
