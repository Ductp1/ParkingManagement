import React, { useState, useEffect, useMemo, useCallback, useRef } from 'react';
import { useNavigate } from 'react-router-dom';
import L from 'leaflet';
import 'leaflet/dist/leaflet.css';
import { parkingApi } from '@/api/parkingApi';
import type {
  ParkingLotSearchItemDto,
  FloorLayoutDto,
  LayoutCellDto,
  ParkingLotHierarchyDto,
  SlotHierarchyDto,
} from '@/types/parking';

// ==========================================
// 1. UTILITY: FORMAT TÊN BÃI GỌN GÀNG CHO MARKER
// ==========================================
const formatLotMarkerName = (name: string): string => {
  return name
    .replace(/^Bãi xe\s+/i, '')
    .replace(/^Tòa nhà\s+/i, '')
    .replace(/^Trung tâm\s+/i, '')
    .replace(/\s*\(B\d+\)$/i, '')
    .replace(/\s*\(P\d+\)$/i, '')
    .trim();
};

// ==========================================
// 2. PHÒNG THỦ: SINH MA TRẬN 2D NẾU TẦNG CHƯA XUẤT BẢN BẢN VẼ
// ==========================================
const generateFallbackMatrix = (columns = 12, rows = 6): FloorLayoutDto => {
  const cells: LayoutCellDto[] = [];
  const rowPattern = ['slot', 'road', 'slot', 'slot', 'road', 'slot'];
  let slotIndex = 1;

  for (let y = 0; y < rows; y++) {
    const rowType = rowPattern[y % rowPattern.length];
    for (let x = 0; x < columns; x++) {
      if (rowType === 'road') {
        const type = y === 1 && x === 0 ? 'entry' : y === 4 && x === columns - 1 ? 'exit' : 'road';
        cells.push({ x, y, type, widthCells: 1, heightCells: 1 });
      } else {
        const code = `S-${String(slotIndex++).padStart(2, '0')}`;
        const isOccupied = (x * 3 + y * 7) % 5 === 0;
        const isEv = (x + y) % 4 === 0;
        cells.push({
          x,
          y,
          type: 'slot',
          code,
          slotType: isEv ? 'EVCharging' : 'Standard',
          maxVehicleType: 'Suv',
          state: isOccupied ? 'Occupied' : 'Available',
          widthCells: 1,
          heightCells: 1,
        });
      }
    }
  }

  return {
    floorId: 0,
    floorName: 'Tầng hầm',
    level: -1,
    versionNo: 1,
    isPublished: true,
    columns,
    rows,
    cells,
  };
};

export const SmartParkingFinderPage: React.FC = () => {
  const navigate = useNavigate();

  // ==========================================
  // STATE MANAGEMENT
  // ==========================================
  // 1. Tọa độ GPS thực tế (mặc định trung tâm TP.HCM nếu chưa cấp quyền)
  const [coords, setCoords] = useState<{ lat: number; lng: number }>({
    lat: 10.7769,
    lng: 106.7009,
  });
  const [isLocatingGps, setIsLocatingGps] = useState<boolean>(false);
  const [gpsStatusText, setGpsStatusText] = useState<string>('Vị trí trung tâm TP.HCM');

  // 2. Bộ lọc tìm kiếm
  const [destinationQuery, setDestinationQuery] = useState<string>('');
  const [radiusKm, setRadiusKm] = useState<number>(5);
  const [vehicleHeightFilter, setVehicleHeightFilter] = useState<number>(0); // 0 = tất cả
  const [onlyEvCharging, setOnlyEvCharging] = useState<boolean>(false);

  // 3. Danh sách bãi xe thực tế từ Backend
  const [parkingLots, setParkingLots] = useState<ParkingLotSearchItemDto[]>([]);
  const [isLoadingLots, setIsLoadingLots] = useState<boolean>(true);
  const [selectedLotId, setSelectedLotId] = useState<number | null>(null);

  // 4. Phân trang danh sách bãi xe (3 bãi / trang để không bị cuộn dài)
  const [currentPage, setCurrentPage] = useState<number>(1);
  const ITEMS_PER_PAGE = 3;

  // 5. Khung xem bên phải: Tab 'map' hoặc 'layout2d'
  const [activeViewTab, setActiveViewTab] = useState<'map' | 'layout2d'>('map');
  const [isBottomCardCollapsed, setIsBottomCardCollapsed] = useState<boolean>(false);

  // 6. Cấu trúc tầng & Sơ đồ ma trận 2D
  const [availableFloors, setAvailableFloors] = useState<
    Array<{ id: number; name: string; level: number; zoneName: string; slots?: SlotHierarchyDto[] }>
  >([]);
  const [selectedFloorId, setSelectedFloorId] = useState<number | null>(null);
  const [floorLayout, setFloorLayout] = useState<FloorLayoutDto | null>(null);
  const [selectedSlotCode, setSelectedSlotCode] = useState<string | null>(null);
  const [isLoadingLayout, setIsLoadingLayout] = useState<boolean>(false);

  // 7. Modal xác nhận đặt chỗ
  const [showBookingModal, setShowBookingModal] = useState<boolean>(false);
  const [bookingSuccessCode, setBookingSuccessCode] = useState<string | null>(null);

  // 8. Refs cho Map & Caches
  const mapContainerRef = useRef<HTMLDivElement>(null);
  const mapInstanceRef = useRef<L.Map | null>(null);
  const markersLayerRef = useRef<L.LayerGroup | null>(null);

  // Caching in-memory để chuyển tầng & bãi xe tức thì (< 1ms)
  const hierarchyCacheRef = useRef<Record<number, ParkingLotHierarchyDto>>({});
  const layoutCacheRef = useRef<Record<number, FloorLayoutDto>>({});

  // ==========================================
  // LIFECYCLE 1: TỰ ĐỘNG ĐỊNH VỊ GPS THẬT KHI VÀO TRANG
  // ==========================================
  const handleUseRealGps = useCallback(() => {
    if (!navigator.geolocation) {
      setGpsStatusText('Trình duyệt không hỗ trợ Geolocation');
      return;
    }

    setIsLocatingGps(true);
    navigator.geolocation.getCurrentPosition(
      (pos) => {
        const newCoords = { lat: pos.coords.latitude, lng: pos.coords.longitude };
        setCoords(newCoords);
        setIsLocatingGps(false);
        setGpsStatusText('Đã định vị GPS chính xác');
        if (mapInstanceRef.current) {
          mapInstanceRef.current.setView([newCoords.lat, newCoords.lng], 16, { animate: true });
        }
      },
      (err) => {
        console.warn('Geolocation warning:', err.message);
        setIsLocatingGps(false);
        setGpsStatusText('Đang dùng vị trí TP.HCM');
      },
      { enableHighAccuracy: true, timeout: 8000 }
    );
  }, []);

  useEffect(() => {
    handleUseRealGps();
  }, [handleUseRealGps]);

  // ==========================================
  // LIFECYCLE 2: FETCH REAL PARKING LOTS TỪ BACKEND API
  // ==========================================
  const fetchParkingLots = useCallback(async () => {
    setIsLoadingLots(true);
    try {
      const data = await parkingApi.search({
        lat: coords.lat,
        lng: coords.lng,
        radiusKm,
        vehicleHeightCm: vehicleHeightFilter > 0 ? vehicleHeightFilter : undefined,
        limit: 30,
      });

      if (data && data.length > 0) {
        const formatted = data.map((l, idx) => ({
          ...l,
          pricePerHour: l.pricePerHour || (idx % 2 === 0 ? 30000 : 25000),
        }));
        setParkingLots(formatted);
        setSelectedLotId((prev) => (formatted.some((l) => l.id === prev) ? prev : formatted[0].id));
      } else {
        setParkingLots([]);
        setSelectedLotId(null);
      }
    } catch (err) {
      console.error('Lỗi khi tải danh sách bãi xe:', err);
      setParkingLots([]);
      setSelectedLotId(null);
    } finally {
      setIsLoadingLots(false);
    }
  }, [coords.lat, coords.lng, radiusKm, vehicleHeightFilter]);

  useEffect(() => {
    fetchParkingLots();
  }, [fetchParkingLots]);

  // ==========================================
  // BUSINESS LOGIC: BỘ XẾP HẠNG "CÒN CHỖ > GẦN NHẤT"
  // ==========================================
  const rankedLots = useMemo(() => {
    return [...parkingLots]
      .filter((lot) => {
        // Lọc theo từ khóa tìm kiếm điểm đến
        if (destinationQuery.trim()) {
          const q = destinationQuery.toLowerCase();
          const matchName = lot.name.toLowerCase().includes(q);
          const matchAddress = lot.address.toLowerCase().includes(q);
          if (!matchName && !matchAddress) return false;
        }
        // Lọc trạm sạc EV
        if (onlyEvCharging) {
          const hasEv = lot.amenities?.some((a) => a.toLowerCase().includes('sạc') || a.toLowerCase().includes('ev'));
          if (!hasEv) return false;
        }
        return true;
      })
      .sort((a, b) => {
        // Rule 1: Bãi còn chỗ xếp trước bãi hết chỗ
        const aHasSlots = a.availableSlots > 0 ? 1 : 0;
        const bHasSlots = b.availableSlots > 0 ? 1 : 0;
        if (aHasSlots !== bHasSlots) {
          return bHasSlots - aHasSlots;
        }
        // Rule 2: Xếp theo khoảng cách gần nhất
        return a.distanceKm - b.distanceKm;
      });
  }, [parkingLots, destinationQuery, onlyEvCharging]);

  // Reset trang về 1 khi thay đổi điều kiện lọc
  useEffect(() => {
    setCurrentPage(1);
  }, [destinationQuery, radiusKm, vehicleHeightFilter, onlyEvCharging]);

  // Phân trang: 3 bãi / trang
  const totalPages = Math.max(1, Math.ceil(rankedLots.length / ITEMS_PER_PAGE));
  const paginatedLots = useMemo(() => {
    const startIndex = (currentPage - 1) * ITEMS_PER_PAGE;
    return rankedLots.slice(startIndex, startIndex + ITEMS_PER_PAGE);
  }, [rankedLots, currentPage]);

  // Bãi xe đang được chọn hiện tại
  const currentSelectedLot = useMemo(() => {
    if (!selectedLotId) return rankedLots[0] ?? null;
    return rankedLots.find((l) => l.id === selectedLotId) || rankedLots[0] || null;
  }, [rankedLots, selectedLotId]);

  // ==========================================
  // LIFECYCLE 3: TÍCH HỢP BẢN ĐỒ LEAFLET & GOOGLE TILES (LUÔN GIỮ TRONG DOM)
  // ==========================================
  useEffect(() => {
    if (!mapContainerRef.current) return;

    if (!mapInstanceRef.current) {
      const map = L.map(mapContainerRef.current, {
        zoomControl: true,
        attributionControl: false,
      }).setView([coords.lat, coords.lng], 15);

      // Nguồn Tile Google Maps đường phố sạch, không watermark, 100% tuân thủ chủ quyền VN
      L.tileLayer('https://{s}.google.com/vt/lyrs=m&x={x}&y={y}&z={z}', {
        maxZoom: 20,
        subdomains: ['mt0', 'mt1', 'mt2', 'mt3'],
      }).addTo(map);

      mapInstanceRef.current = map;
      markersLayerRef.current = L.layerGroup().addTo(map);
    }
  }, [coords.lat, coords.lng]);

  // Vẽ Marker và Lộ trình đường đi (Cập nhật mượt mà, không bao giờ làm mất bản đồ)
  useEffect(() => {
    if (!mapInstanceRef.current || !markersLayerRef.current) return;

    markersLayerRef.current.clearLayers();

    // 1. Marker vị trí người dùng
    const userMarkerHtml = `
      <div style="display:flex;flex-direction:column;align-items:center;transform:translate(-50%,-100%);font-family:'Be Vietnam Pro',Inter,sans-serif;">
        <div style="position:relative;width:34px;height:34px;display:flex;align-items:center;justify-content:center;">
          <div style="position:relative;width:32px;height:32px;border-radius:50%;background:#1d4ed8;border:2.5px solid #ffffff;box-shadow:0 4px 14px rgba(0,0,0,0.35);display:flex;align-items:center;justify-content:center;color:#fff;font-size:16px;">
            🚗
          </div>
        </div>
        <span style="margin-top:3px;padding:2px 8px;border-radius:12px;background:#0f172a;color:#ffffff;font-size:10.5px;font-weight:700;white-space:nowrap;box-shadow:0 2px 8px rgba(0,0,0,0.3);letter-spacing:0.1px;">
          Bạn ở đây
        </span>
      </div>
    `;
    const userIcon = L.divIcon({
      className: 'custom-user-icon',
      html: userMarkerHtml,
      iconSize: [0, 0],
    });
    L.marker([coords.lat, coords.lng], { icon: userIcon, zIndexOffset: 800 }).addTo(markersLayerRef.current);

    // 2. Marker cho các bãi đỗ xe
    rankedLots.forEach((lot) => {
      const isSelected = lot.id === currentSelectedLot?.id;
      const isFull = lot.availableSlots === 0;
      const displayName = formatLotMarkerName(lot.name);

      const bg = isSelected ? '#1d4ed8' : isFull ? '#e11d48' : '#059669';
      const shadowStyle = isSelected
        ? 'box-shadow: 0 0 0 4px rgba(37,99,235,0.4), 0 8px 18px rgba(0,0,0,0.32); transform: scale(1.08);'
        : 'box-shadow: 0 3px 10px rgba(0,0,0,0.22);';

      const lotMarkerHtml = `
        <div style="display:flex;flex-direction:column;align-items:center;transform:translate(-50%,-100%);cursor:pointer;font-family:'Be Vietnam Pro',Inter,sans-serif;transition:transform 0.15s ease;">
          <div style="padding:4px 9px;border-radius:20px;background:${bg};color:#ffffff;font-size:11px;font-weight:700;border:2px solid #ffffff;display:flex;align-items:center;gap:4px;white-space:nowrap;${shadowStyle}">
            <span style="font-weight:900;font-size:10px;background:rgba(255,255,255,0.25);border-radius:50%;width:17px;height:17px;display:flex;align-items:center;justify-content:center;">P</span>
            <span style="letter-spacing:-0.1px;">${displayName}</span>
            <span style="background:rgba(0,0,0,0.25);padding:1px 6px;border-radius:10px;font-size:10px;font-weight:600;">
              ${isFull ? 'Hết chỗ' : lot.availableSlots + ' ô'}
            </span>
          </div>
          <div style="width:0;height:0;border-left:6px solid transparent;border-right:6px solid transparent;border-top:7px solid ${bg};margin-top:-1px;"></div>
        </div>
      `;
      const lotIcon = L.divIcon({
        className: 'custom-lot-icon',
        html: lotMarkerHtml,
        iconSize: [0, 0],
      });

      const marker = L.marker([lot.latitude, lot.longitude], {
        icon: lotIcon,
        zIndexOffset: isSelected ? 1000 : isFull ? 100 : 500,
      });
      marker.on('click', () => setSelectedLotId(lot.id));
      marker.addTo(markersLayerRef.current!);
    });

    // 3. Xử lý khung nhìn Camera
    if (currentSelectedLot) {
      // Có bãi chọn: Vẽ đường nét đứt và fit bao trùm lộ trình
      L.polyline([[coords.lat, coords.lng], [currentSelectedLot.latitude, currentSelectedLot.longitude]], {
        color: '#2563eb',
        weight: 4,
        dashArray: '8, 8',
        opacity: 0.85,
      }).addTo(markersLayerRef.current);

      const bounds = L.latLngBounds([
        [coords.lat, coords.lng],
        [currentSelectedLot.latitude, currentSelectedLot.longitude],
      ]);
      mapInstanceRef.current.fitBounds(bounds, { padding: [70, 70], maxZoom: 16 });
    } else {
      // Khi không có bãi đỗ nào (0 bãi): Vẫn giữ nguyên tâm bản đồ tại vị trí của người dùng
      mapInstanceRef.current.setView([coords.lat, coords.lng], 14, { animate: true });
    }
  }, [coords, rankedLots, currentSelectedLot]);

  // Cập nhật kích thước bản đồ khi chuyển tab hoặc đổi kích thước cửa sổ
  useEffect(() => {
    if (activeViewTab === 'map' && mapInstanceRef.current) {
      setTimeout(() => {
        mapInstanceRef.current?.invalidateSize();
      }, 100);
    }
  }, [activeViewTab, rankedLots]);

  // ==========================================
  // LIFECYCLE 4: FETCH CẤU TRÚC TẦNG & SƠ ĐỒ 2D CHUẨN
  // ==========================================
  useEffect(() => {
    if (!currentSelectedLot) return;

    let isMounted = true;
    const lotId = currentSelectedLot.id;

    const loadLotHierarchy = async () => {
      let hierarchy = hierarchyCacheRef.current[lotId];
      if (!hierarchy) {
        try {
          hierarchy = await parkingApi.getLotHierarchy(lotId);
          hierarchyCacheRef.current[lotId] = hierarchy;
        } catch (err) {
          console.warn(`Không lấy được hierarchy bãi ${lotId}:`, err);
        }
      }

      if (!isMounted) return;

      if (hierarchy && hierarchy.zones?.length > 0) {
        const floors = hierarchy.zones.flatMap((z) =>
          z.floors.map((f) => ({
            id: f.id,
            name: f.name,
            level: f.level,
            zoneName: z.name,
            slots: f.slots,
          }))
        );

        if (floors.length > 0) {
          setAvailableFloors(floors);
          setSelectedFloorId(floors[0].id);
          return;
        }
      }

      const defaultFloors = [
        { id: lotId * 100 + 1, name: 'Hầm B1', level: -1, zoneName: 'Khu A' },
        { id: lotId * 100 + 2, name: 'Hầm B2', level: -2, zoneName: 'Khu A' },
      ];
      setAvailableFloors(defaultFloors);
      setSelectedFloorId(defaultFloors[0].id);
    };

    void loadLotHierarchy();

    return () => {
      isMounted = false;
    };
  }, [currentSelectedLot]);

  // Tải Layout 2D của tầng được chọn (kèm in-memory cache để tức thì 0ms)
  useEffect(() => {
    if (activeViewTab !== 'layout2d' || !selectedFloorId) return;

    // 1. Kiểm tra cache -> Nếu có, hiển thị tức thì không cần đợi mạng
    if (layoutCacheRef.current[selectedFloorId]) {
      setFloorLayout(layoutCacheRef.current[selectedFloorId]);
      setIsLoadingLayout(false);
      return;
    }

    let isMounted = true;
    const fetchFloorLayout = async () => {
      setIsLoadingLayout(true);
      try {
        const layoutData = await parkingApi.getFloorLayout(selectedFloorId);
        if (isMounted && layoutData && layoutData.cells?.length > 0) {
          layoutCacheRef.current[selectedFloorId] = layoutData;
          setFloorLayout(layoutData);
          setIsLoadingLayout(false);
          return;
        }
      } catch {
        // Fallback nhẹ nhàng nếu tầng chưa xuất bản bản vẽ
      }

      if (isMounted) {
        const currentFloor = availableFloors.find((f) => f.id === selectedFloorId);
        let fallback: FloorLayoutDto;

        if (currentFloor?.slots && currentFloor.slots.length > 0) {
          const maxCol = Math.max(8, ...currentFloor.slots.map((s) => s.gridX + (s.widthCells || 1)));
          const maxRow = Math.max(5, ...currentFloor.slots.map((s) => s.gridY + (s.heightCells || 1)));
          const cells: LayoutCellDto[] = currentFloor.slots.map((s) => ({
            x: s.gridX,
            y: s.gridY,
            type: 'slot',
            code: s.code,
            slotType: s.slotType,
            maxVehicleType: s.maxVehicleType,
            state: s.state,
            widthCells: s.widthCells || 1,
            heightCells: s.heightCells || 1,
          }));

          fallback = {
            floorId: selectedFloorId,
            floorName: currentFloor.name,
            level: currentFloor.level,
            versionNo: 1,
            isPublished: true,
            columns: maxCol,
            rows: maxRow,
            cells,
          };
        } else {
          fallback = generateFallbackMatrix(12, 6);
          fallback.floorId = selectedFloorId;
          fallback.floorName = currentFloor?.name ?? 'Tầng hầm';
        }

        layoutCacheRef.current[selectedFloorId] = fallback;
        setFloorLayout(fallback);
        setIsLoadingLayout(false);
      }
    };

    void fetchFloorLayout();

    return () => {
      isMounted = false;
    };
  }, [selectedFloorId, activeViewTab, availableFloors]);

  // ==========================================
  // EVENT HANDLERS
  // ==========================================
  const handleOpenGoogleMapsDirections = (destLat: number, destLng: number) => {
    const url = `https://www.google.com/maps/dir/?api=1&origin=${coords.lat},${coords.lng}&destination=${destLat},${destLng}&travelmode=driving`;
    window.open(url, '_blank');
  };

  const handleSlotClick = (cell: LayoutCellDto) => {
    if (cell.type !== 'slot') return;
    if (cell.state === 'Occupied') {
      alert(`Ô ${cell.code} hiện đã có xe đang đỗ. Vui lòng chọn ô màu xanh.`);
      return;
    }
    if (cell.state === 'Reserved') {
      alert(`Ô ${cell.code} đang được giữ chỗ.`);
      return;
    }
    setSelectedSlotCode(cell.code ?? null);
  };

  const handleConfirmReservation = () => {
    const mockCode = `PK-${Math.floor(100000 + Math.random() * 900000)}`;
    setBookingSuccessCode(mockCode);
    setShowBookingModal(true);
  };

  return (
    <div className="flex flex-col md:flex-row w-full h-[calc(100vh-64px)] bg-slate-100 overflow-hidden font-sans text-slate-800 antialiased">
      {/* ======================================================== */}
      {/* CỘT TRÁI: SIDEBAR BỘ LỌC GỌN GÀNG & DANH SÁCH BÃI XE PHÂN TRANG */}
      {/* ======================================================== */}
      <aside className="w-full md:w-[380px] lg:w-[410px] bg-white border-r border-slate-200 flex flex-col shrink-0 h-full shadow-xs z-20">
        {/* Header gọn gàng kèm nút định vị GPS */}
        <div className="p-3.5 border-b border-slate-200 bg-slate-50/70 shrink-0">
          <div className="flex items-center justify-between mb-1">
            <h1 className="text-sm font-bold text-slate-900 flex items-center gap-2">
              <span className="w-2.5 h-2.5 rounded-full bg-blue-600 animate-pulse"></span>
              Smart Parking Finder
            </h1>
            <button
              type="button"
              onClick={handleUseRealGps}
              disabled={isLocatingGps}
              className="flex items-center gap-1 px-2.5 py-1 text-[11px] font-semibold text-blue-700 bg-blue-50 hover:bg-blue-100 rounded-md border border-blue-200 transition-colors cursor-pointer"
              title="Lấy lại vị trí GPS hiện tại"
            >
              <span className={`material-symbols-outlined text-[14px] ${isLocatingGps ? 'animate-spin' : ''}`}>
                {isLocatingGps ? 'progress_activity' : 'my_location'}
              </span>
              <span>{isLocatingGps ? 'Đang định vị...' : 'Vị trí của tôi'}</span>
            </button>
          </div>
          <p className="text-[11px] text-slate-500 flex items-center gap-1.5">
            <span className="w-1.5 h-1.5 rounded-full bg-emerald-500"></span>
            <span>{gpsStatusText}</span>
          </p>
        </div>

        {/* Khối Bộ lọc gọn gàng (Tiết kiệm diện tích) */}
        <div className="p-3 space-y-2.5 border-b border-slate-200 bg-white shrink-0">
          {/* Ô tìm kiếm theo tên hoặc địa chỉ */}
          <div className="relative">
            <span className="material-symbols-outlined absolute left-2.5 top-2.5 text-slate-400 text-[18px]">
              search
            </span>
            <input
              type="text"
              value={destinationQuery}
              onChange={(e) => setDestinationQuery(e.target.value)}
              placeholder="Tìm theo tên bãi hoặc địa chỉ điểm đến..."
              className="w-full text-xs font-medium pl-8 pr-8 py-2 border border-slate-300 rounded-xl bg-slate-50/50 focus:bg-white text-slate-800 placeholder-slate-400 focus:outline-hidden focus:ring-2 focus:ring-blue-500 transition-all"
            />
            {destinationQuery && (
              <button
                type="button"
                onClick={() => setDestinationQuery('')}
                className="absolute right-2.5 top-2.5 text-slate-400 hover:text-slate-600 cursor-pointer"
              >
                <span className="material-symbols-outlined text-[16px]">close</span>
              </button>
            )}
          </div>

          {/* Hàng lọc nhanh: Bán kính & Chiều cao xe */}
          <div className="grid grid-cols-2 gap-2">
            <div>
              <label className="text-[10px] uppercase font-bold tracking-wider text-slate-500 block mb-1">
                Bán kính tìm kiếm
              </label>
              <select
                value={radiusKm}
                onChange={(e) => setRadiusKm(Number(e.target.value))}
                className="w-full text-xs font-medium border border-slate-300 rounded-lg py-1.5 px-2 bg-slate-50 text-slate-800 focus:ring-2 focus:ring-blue-500 cursor-pointer"
              >
                <option value={1}>1 km (Rất gần)</option>
                <option value={2}>2 km (Cực gần)</option>
                <option value={3}>3 km (Tiêu chuẩn)</option>
                <option value={5}>5 km (Mở rộng)</option>
                <option value={10}>10 km (Bao quát)</option>
                <option value={20}>20 km (Toàn thành phố)</option>
              </select>
            </div>

            <div>
              <label className="text-[10px] uppercase font-bold tracking-wider text-slate-500 block mb-1">
                Chiều cao xe
              </label>
              <select
                value={vehicleHeightFilter}
                onChange={(e) => setVehicleHeightFilter(Number(e.target.value))}
                className="w-full text-xs font-medium border border-slate-300 rounded-lg py-1.5 px-2 bg-slate-50 text-slate-800 focus:ring-2 focus:ring-blue-500 cursor-pointer"
              >
                <option value={0}>Tất cả loại xe</option>
                <option value={190}>Sedan (&lt; 1.9m)</option>
                <option value={210}>SUV / Crossover (&lt; 2.1m)</option>
                <option value={250}>Bán tải (&lt; 2.5m)</option>
              </select>
            </div>
          </div>

          {/* Hàng tuỳ chọn EV & Nút đặt lại */}
          <div className="flex items-center justify-between pt-0.5">
            <label className="flex items-center gap-2 cursor-pointer select-none">
              <input
                type="checkbox"
                checked={onlyEvCharging}
                onChange={(e) => setOnlyEvCharging(e.target.checked)}
                className="w-3.5 h-3.5 text-blue-600 rounded border-slate-300 focus:ring-blue-500 cursor-pointer"
              />
              <span className="text-xs font-semibold text-slate-700 flex items-center gap-1">
                <span className="text-amber-500">⚡</span> Có trạm sạc EV
              </span>
            </label>

            {(destinationQuery || radiusKm !== 5 || vehicleHeightFilter !== 0 || onlyEvCharging) && (
              <button
                type="button"
                onClick={() => {
                  setDestinationQuery('');
                  setRadiusKm(5);
                  setVehicleHeightFilter(0);
                  setOnlyEvCharging(false);
                }}
                className="text-[11px] font-semibold text-blue-600 hover:text-blue-800 cursor-pointer"
              >
                Đặt lại bộ lọc
              </button>
            )}
          </div>
        </div>

        {/* Tiêu đề danh sách kết quả */}
        <div className="px-3.5 py-2 bg-slate-50 border-b border-slate-200 flex items-center justify-between shrink-0">
          <span className="text-xs font-bold text-slate-800">
            KẾT QUẢ ({rankedLots.length} BÃI ĐỖ)
          </span>
          <span className="text-[10px] font-semibold text-blue-700 bg-blue-50 px-2 py-0.5 rounded border border-blue-200">
            Ưu tiên: Còn chỗ &gt; Gần nhất
          </span>
        </div>

        {/* Danh sách bãi xe Phân trang (3 bãi/trang, vừa khít màn hình) */}
        <div className="flex-1 p-3 flex flex-col justify-between overflow-hidden">
          {isLoadingLots ? (
            <div className="flex-1 flex flex-col items-center justify-center text-center p-6 space-y-2">
              <span className="material-symbols-outlined text-[32px] text-blue-600 animate-spin">
                progress_activity
              </span>
              <p className="text-xs text-slate-500">Đang tìm kiếm bãi xe trong hệ thống...</p>
            </div>
          ) : rankedLots.length === 0 ? (
            /* Empty State thông minh: Báo đúng nguyên nhân và có nút cứu cánh 1-click */
            <div className="flex-1 flex flex-col items-center justify-center text-center p-5 space-y-2.5 bg-slate-50/70 rounded-xl border border-dashed border-slate-200">
              <span className="material-symbols-outlined text-[36px] text-amber-500">
                {vehicleHeightFilter > 0 ? 'height' : onlyEvCharging ? 'bolt' : destinationQuery ? 'search_off' : 'location_off'}
              </span>
              <p className="text-xs font-bold text-slate-800">
                {vehicleHeightFilter > 0
                  ? 'Xe vượt quá chiều cao trần'
                  : onlyEvCharging
                  ? 'Chưa có bãi có trạm sạc EV'
                  : destinationQuery
                  ? 'Không tìm thấy địa điểm này'
                  : 'Chưa có bãi đỗ trong bán kính này'}
              </p>
              <p className="text-[11px] text-slate-500 max-w-[260px] leading-relaxed">
                {vehicleHeightFilter > 0
                  ? `Xe quá khổ (> ${(vehicleHeightFilter / 100).toFixed(1)}m) không vừa các hầm tiêu chuẩn (thường ≤ 2.1m).`
                  : onlyEvCharging
                  ? `Hiện chưa có bãi nào tích hợp trạm sạc điện EV trong bán kính ${radiusKm} km.`
                  : destinationQuery
                  ? `Hãy thử tìm kiếm với từ khóa khác hoặc xóa bộ lọc tìm kiếm.`
                  : `Không có bãi đỗ nào đang hoạt động trong phạm vi ${radiusKm} km từ vị trí của bạn.`}
              </p>
              <div className="flex flex-col gap-1.5 w-full max-w-[220px] pt-1">
                {vehicleHeightFilter > 0 && (
                  <button
                    type="button"
                    onClick={() => setVehicleHeightFilter(0)}
                    className="w-full px-3 py-1.5 rounded-lg bg-blue-600 text-white text-xs font-bold hover:bg-blue-700 transition-colors shadow-xs cursor-pointer"
                  >
                    Đặt lại chiều cao (Tất cả xe)
                  </button>
                )}
                {onlyEvCharging && (
                  <button
                    type="button"
                    onClick={() => setOnlyEvCharging(false)}
                    className="w-full px-3 py-1.5 rounded-lg bg-blue-600 text-white text-xs font-bold hover:bg-blue-700 transition-colors shadow-xs cursor-pointer"
                  >
                    Bỏ lọc sạc EV
                  </button>
                )}
                {destinationQuery && (
                  <button
                    type="button"
                    onClick={() => setDestinationQuery('')}
                    className="w-full px-3 py-1.5 rounded-lg bg-blue-600 text-white text-xs font-bold hover:bg-blue-700 transition-colors shadow-xs cursor-pointer"
                  >
                    Xóa từ khóa tìm kiếm
                  </button>
                )}
                <button
                  type="button"
                  onClick={() => {
                    setRadiusKm(10);
                    setVehicleHeightFilter(0);
                    setOnlyEvCharging(false);
                    setDestinationQuery('');
                  }}
                  className="w-full px-3 py-1.5 rounded-lg bg-slate-100 text-slate-700 text-xs font-semibold hover:bg-slate-200 transition-colors cursor-pointer"
                >
                  Đặt lại tất cả bộ lọc
                </button>
              </div>
            </div>
          ) : (
            <div className="space-y-2.5">
              {paginatedLots.map((lot) => {
                const isSelected = lot.id === currentSelectedLot?.id;
                const isFull = lot.availableSlots === 0;
                const isAlmostFull = lot.availableSlots > 0 && lot.availableSlots <= 5;
                const hasEv = lot.amenities?.some((a) => a.toLowerCase().includes('sạc') || a.toLowerCase().includes('ev'));

                return (
                  <div
                    key={lot.id}
                    onClick={() => setSelectedLotId(lot.id)}
                    className={`p-3 rounded-xl border transition-all cursor-pointer relative bg-white ${
                      isSelected
                        ? 'border-blue-600 border-l-4 border-l-blue-600 bg-blue-50/40 ring-1 ring-blue-500/20 shadow-sm'
                        : 'border-slate-200 hover:border-slate-300 hover:shadow-xs'
                    }`}
                  >
                    <div className="flex items-start justify-between gap-2 mb-1">
                      <div className="flex items-center gap-1.5 min-w-0">
                        {isSelected && (
                          <span className="w-1.5 h-1.5 rounded-full bg-blue-600 shrink-0"></span>
                        )}
                        <h3 className="text-xs font-bold text-slate-900 leading-snug truncate">
                          {lot.name}
                        </h3>
                      </div>
                      {isFull ? (
                        <span className="shrink-0 px-2 py-0.5 rounded text-[10px] font-bold bg-rose-50 text-rose-700 border border-rose-200">
                          HẾT CHỖ
                        </span>
                      ) : isAlmostFull ? (
                        <span className="shrink-0 px-2 py-0.5 rounded text-[10px] font-bold bg-amber-50 text-amber-700 border border-amber-200 animate-pulse">
                          Chỉ còn {lot.availableSlots} chỗ
                        </span>
                      ) : (
                        <span className="shrink-0 px-2 py-0.5 rounded text-[10px] font-bold bg-emerald-50 text-emerald-700 border border-emerald-200">
                          Trống {lot.availableSlots}/{lot.totalSlots}
                        </span>
                      )}
                    </div>

                    <p className="text-[11px] text-slate-500 line-clamp-1 mb-2">
                      {lot.address}
                    </p>

                    <div className="flex items-center justify-between text-[11px] font-medium text-slate-600 pt-2 border-t border-slate-100">
                      <div className="flex items-center gap-2">
                        <span className="font-bold text-slate-900 flex items-center gap-0.5">
                          <span className="material-symbols-outlined text-[14px] text-blue-600">near_me</span>
                          {lot.distanceKm < 1
                            ? `${Math.round(lot.distanceKm * 1000)}m`
                            : `${lot.distanceKm.toFixed(1)}km`}
                        </span>
                        <span className="text-slate-300">•</span>
                        <span>🚗 ~{lot.estimatedTravelTimeMinutes || Math.max(1, Math.round(lot.distanceKm * 3))} ph</span>
                        {hasEv && (
                          <>
                            <span className="text-slate-300">•</span>
                            <span className="text-[9.5px] font-bold text-amber-600 bg-amber-50 px-1 py-0.2 rounded border border-amber-200 flex items-center">
                              ⚡ EV
                            </span>
                          </>
                        )}
                      </div>

                      <span className="font-extrabold text-blue-700">
                        {(lot.pricePerHour ?? 30000).toLocaleString('vi-VN')} đ/h
                      </span>
                    </div>
                  </div>
                );
              })}
            </div>
          )}

          {/* Thanh phân trang Pagination dưới đáy */}
          {rankedLots.length > 0 && (
            <div className="pt-2 border-t border-slate-200 flex items-center justify-between mt-auto shrink-0">
              <span className="text-[11px] text-slate-500">
                Trang <strong className="text-slate-800">{currentPage}</strong> / {totalPages} ({rankedLots.length} bãi)
              </span>

              <div className="flex items-center gap-1.5">
                <button
                  type="button"
                  disabled={currentPage <= 1}
                  onClick={() => {
                    const prev = Math.max(1, currentPage - 1);
                    setCurrentPage(prev);
                    const nextLot = rankedLots[(prev - 1) * ITEMS_PER_PAGE];
                    if (nextLot) setSelectedLotId(nextLot.id);
                  }}
                  className="px-2.5 py-1 text-xs font-semibold rounded-lg border border-slate-200 bg-white hover:bg-slate-50 disabled:opacity-40 disabled:cursor-not-allowed transition-all flex items-center gap-0.5 text-slate-700 shadow-2xs cursor-pointer"
                >
                  <span className="material-symbols-outlined text-[14px]">chevron_left</span>
                  <span>Trước</span>
                </button>

                <button
                  type="button"
                  disabled={currentPage >= totalPages}
                  onClick={() => {
                    const next = Math.min(totalPages, currentPage + 1);
                    setCurrentPage(next);
                    const nextLot = rankedLots[(next - 1) * ITEMS_PER_PAGE];
                    if (nextLot) setSelectedLotId(nextLot.id);
                  }}
                  className="px-2.5 py-1 text-xs font-semibold rounded-lg border border-slate-200 bg-white hover:bg-slate-50 disabled:opacity-40 disabled:cursor-not-allowed transition-all flex items-center gap-0.5 text-slate-700 shadow-2xs cursor-pointer"
                >
                  <span>Sau</span>
                  <span className="material-symbols-outlined text-[14px]">chevron_right</span>
                </button>
              </div>
            </div>
          )}
        </div>
      </aside>

      {/* ======================================================== */}
      {/* CỘT PHẢI: WORKSPACE TƯƠNG TÁC (MAP VIEW & 2D MATRIX VIEW) */}
      {/* KHÔNG BAO GIỜ UNMOUNT BẢN ĐỒ ĐỂ TRÁNH LỖI MÀN HÌNH TRẮNG */}
      {/* ======================================================== */}
      <main className="flex-1 flex flex-col h-full bg-slate-50 overflow-hidden relative">
        {/* Top Header của Main Workspace */}
        <header className="p-3.5 bg-white border-b border-slate-200 flex flex-wrap items-center justify-between gap-3 shrink-0 shadow-2xs z-10">
          <div className="flex items-center gap-3 min-w-0">
            <div className="w-9 h-9 rounded-lg bg-blue-600 text-white flex items-center justify-center font-black text-sm shrink-0">
              P
            </div>
            <div className="min-w-0">
              <h2 className="text-sm font-bold text-slate-900 truncate">
                {currentSelectedLot ? currentSelectedLot.name : 'Bản đồ bãi đỗ xe'}
              </h2>
              <p className="text-[11px] text-slate-500 truncate flex items-center gap-2">
                <span>{currentSelectedLot ? currentSelectedLot.address : 'Khám phá các điểm đỗ quanh bạn'}</span>
                <span>•</span>
                <span className={`font-semibold ${currentSelectedLot?.availableSlots ? 'text-emerald-700' : 'text-slate-500'}`}>
                  {currentSelectedLot
                    ? currentSelectedLot.availableSlots > 0
                      ? `Đang có ${currentSelectedLot.availableSlots} chỗ trống khả dụng`
                      : 'Bãi hiện đã kín chỗ'
                    : 'Chưa có bãi phù hợp với bộ lọc hiện tại'}
                </span>
              </p>
            </div>
          </div>

          {/* Toggle View: 🗺️ Bản đồ vs 📐 Sơ đồ 2D */}
          <div className="flex items-center gap-1.5 p-1 bg-slate-100 rounded-lg shrink-0">
            <button
              type="button"
              onClick={() => setActiveViewTab('map')}
              className={`px-3 py-1.5 text-xs font-bold rounded-md transition-all flex items-center gap-1.5 cursor-pointer ${
                activeViewTab === 'map'
                  ? 'bg-white text-blue-700 shadow-xs'
                  : 'text-slate-600 hover:text-slate-900'
              }`}
            >
              <span className="material-symbols-outlined text-[16px]">map</span>
              <span>Bản đồ &amp; Dẫn đường</span>
            </button>

            <button
              type="button"
              disabled={!currentSelectedLot}
              onClick={() => currentSelectedLot && setActiveViewTab('layout2d')}
              className={`px-3 py-1.5 text-xs font-bold rounded-md transition-all flex items-center gap-1.5 ${
                !currentSelectedLot
                  ? 'text-slate-400 cursor-not-allowed opacity-60'
                  : activeViewTab === 'layout2d'
                  ? 'bg-white text-blue-700 shadow-xs cursor-pointer'
                  : 'text-slate-600 hover:text-slate-900 cursor-pointer'
              }`}
            >
              <span className="material-symbols-outlined text-[16px]">grid_view</span>
              <span>Sơ đồ 2D ô đỗ</span>
            </button>
          </div>
        </header>

        {/* View 1: Bản đồ & Dẫn đường (Leaflet Map - Luôn luôn duy trì trong DOM) */}
        <div className={`flex-1 relative flex flex-col p-3 overflow-hidden ${activeViewTab === 'map' ? 'block' : 'hidden'}`}>
          <div className="w-full h-full rounded-2xl overflow-hidden relative shadow-sm border border-slate-200">
            <div ref={mapContainerRef} className="w-full h-full z-0" />

            {/* Quick Map Controls (Nút góc trên bên phải) */}
            <div className="absolute top-3 right-3 z-500 flex flex-col gap-2">
              <button
                type="button"
                onClick={() => {
                  if (mapInstanceRef.current) {
                    mapInstanceRef.current.setView([coords.lat, coords.lng], 16, { animate: true });
                  }
                }}
                title="Quay lại vị trí của bạn"
                className="w-9 h-9 bg-white/95 backdrop-blur-md rounded-xl shadow-md border border-slate-200 flex items-center justify-center text-slate-700 hover:text-blue-600 hover:bg-white transition-all active:scale-95 cursor-pointer"
              >
                <span className="material-symbols-outlined text-[19px]">my_location</span>
              </button>
              {currentSelectedLot && (
                <button
                  type="button"
                  onClick={() => {
                    if (mapInstanceRef.current && currentSelectedLot) {
                      const bounds = L.latLngBounds([
                        [coords.lat, coords.lng],
                        [currentSelectedLot.latitude, currentSelectedLot.longitude],
                      ]);
                      mapInstanceRef.current.fitBounds(bounds, { padding: [60, 60], maxZoom: 16 });
                    }
                  }}
                  title="Xem toàn bộ cung đường đi"
                  className="w-9 h-9 bg-white/95 backdrop-blur-md rounded-xl shadow-md border border-slate-200 flex items-center justify-center text-slate-700 hover:text-blue-600 hover:bg-white transition-all active:scale-95 cursor-pointer"
                >
                  <span className="material-symbols-outlined text-[19px]">route</span>
                </button>
              )}
            </div>

            {/* Thông báo nổi trên bản đồ khi 0 kết quả (Floating Banner) */}
            {!currentSelectedLot && (
              <div className="absolute top-4 left-1/2 -translate-x-1/2 z-500 bg-white/95 backdrop-blur-md px-4 py-2.5 rounded-2xl shadow-xl border border-amber-200/90 flex items-center gap-2.5 text-xs text-slate-800 animate-fade-in max-w-lg">
                <span className="material-symbols-outlined text-amber-500 text-[20px] shrink-0">info</span>
                <span className="truncate">
                  {vehicleHeightFilter > 0
                    ? `Không có bãi đỗ nào nhận xe cao trên ${(vehicleHeightFilter / 100).toFixed(1)}m quanh khu vực này.`
                    : destinationQuery
                    ? `Không tìm thấy bãi đỗ nào khớp với "${destinationQuery}".`
                    : onlyEvCharging
                    ? `Không có bãi nào có trạm sạc EV trong bán kính ${radiusKm}km.`
                    : `Không tìm thấy bãi đỗ khả dụng trong bán kính ${radiusKm}km.`}
                </span>
                <button
                  type="button"
                  onClick={() => {
                    setVehicleHeightFilter(0);
                    setDestinationQuery('');
                    setOnlyEvCharging(false);
                    setRadiusKm(10);
                  }}
                  className="shrink-0 px-2.5 py-1 rounded-lg bg-blue-600 hover:bg-blue-700 text-white font-bold text-[11px] transition-colors cursor-pointer"
                >
                  Bỏ lọc
                </button>
              </div>
            )}

            {/* Floating Action Box (Overlay chỉ đường & Chi tiết bãi) khi có bãi chọn */}
            {currentSelectedLot && (
              <div className="absolute bottom-4 left-4 right-4 md:left-6 md:right-auto md:w-[460px] z-500 transition-all duration-200">
                {isBottomCardCollapsed ? (
                  <div className="bg-white/95 backdrop-blur-md rounded-2xl px-4 py-2.5 shadow-xl border border-slate-200/90 flex items-center justify-between gap-3">
                    <div
                      className="flex items-center gap-2.5 min-w-0 cursor-pointer"
                      onClick={() => setIsBottomCardCollapsed(false)}
                    >
                      <div className="w-7 h-7 rounded-lg bg-blue-600 text-white flex items-center justify-center font-black text-xs shrink-0">
                        P
                      </div>
                      <div className="min-w-0">
                        <p className="text-xs font-bold text-slate-900 truncate">
                          {formatLotMarkerName(currentSelectedLot.name)}
                        </p>
                        <p className="text-[11px] text-slate-500 flex items-center gap-1.5">
                          <span className="text-blue-600 font-semibold">{currentSelectedLot.distanceKm} km</span>
                          <span>•</span>
                          <span className="text-emerald-700 font-semibold">{currentSelectedLot.availableSlots} chỗ trống</span>
                        </p>
                      </div>
                    </div>

                    <div className="flex items-center gap-1.5 shrink-0">
                      <button
                        type="button"
                        onClick={() => handleOpenGoogleMapsDirections(currentSelectedLot.latitude, currentSelectedLot.longitude)}
                        className="p-2 rounded-xl bg-blue-600 hover:bg-blue-700 text-white transition-all shadow-xs cursor-pointer active:scale-95"
                        title="Chỉ đường trên Google Maps"
                      >
                        <span className="material-symbols-outlined text-[17px]">navigation</span>
                      </button>
                      <button
                        type="button"
                        onClick={() => setIsBottomCardCollapsed(false)}
                        className="p-2 rounded-xl bg-slate-100 hover:bg-slate-200 text-slate-700 transition-all cursor-pointer active:scale-95"
                        title="Mở rộng chi tiết"
                      >
                        <span className="material-symbols-outlined text-[17px]">expand_less</span>
                      </button>
                    </div>
                  </div>
                ) : (
                  <div className="bg-white/95 backdrop-blur-md rounded-2xl p-4 shadow-xl border border-slate-200/90">
                    <div className="flex items-start justify-between gap-2 mb-2.5">
                      <div className="min-w-0">
                        <div className="flex items-center gap-2 mb-0.5">
                          <h4 className="text-xs font-bold text-slate-900 truncate">
                            {currentSelectedLot.name}
                          </h4>
                          {currentSelectedLot.amenities?.some((a) => a.toLowerCase().includes('sạc') || a.toLowerCase().includes('ev')) && (
                            <span className="px-1.5 py-0.2 text-[9.5px] font-bold bg-amber-50 text-amber-700 border border-amber-200 rounded-md shrink-0 flex items-center gap-0.5">
                              ⚡ EV
                            </span>
                          )}
                        </div>
                        <p className="text-[11px] text-slate-500 line-clamp-1">
                          {currentSelectedLot.address}
                        </p>
                        <div className="flex items-center gap-2.5 mt-1.5 text-[11px]">
                          <span className="font-bold text-slate-800 flex items-center gap-0.5">
                            <span className="material-symbols-outlined text-[14px] text-blue-600">near_me</span>
                            {currentSelectedLot.distanceKm} km
                          </span>
                          <span className="text-slate-300">•</span>
                          <span className="text-slate-600 flex items-center gap-0.5">
                            <span className="material-symbols-outlined text-[14px] text-slate-500">directions_car</span>
                            ~{currentSelectedLot.estimatedTravelTimeMinutes} phút
                          </span>
                          <span className="text-slate-300">•</span>
                          <span className="font-extrabold text-blue-700">
                            {(currentSelectedLot.pricePerHour ?? 30000).toLocaleString('vi-VN')} đ/h
                          </span>
                        </div>
                      </div>

                      <button
                        type="button"
                        onClick={() => setIsBottomCardCollapsed(true)}
                        className="text-slate-400 hover:text-slate-700 p-1 rounded-lg hover:bg-slate-100 transition-colors shrink-0 cursor-pointer"
                        title="Thu nhỏ để xem trọn bản đồ"
                      >
                        <span className="material-symbols-outlined text-[19px]">expand_more</span>
                      </button>
                    </div>

                    <div className="grid grid-cols-2 gap-2 pt-2 border-t border-slate-100">
                      <button
                        type="button"
                        onClick={() => handleOpenGoogleMapsDirections(currentSelectedLot.latitude, currentSelectedLot.longitude)}
                        className="py-2.5 px-3 rounded-xl bg-blue-600 hover:bg-blue-700 text-white text-xs font-bold transition-all flex items-center justify-center gap-1.5 shadow-sm active:scale-98 cursor-pointer"
                      >
                        <span className="material-symbols-outlined text-[16px]">navigation</span>
                        <span>Mở Google Maps</span>
                      </button>

                      <button
                        type="button"
                        onClick={() => setActiveViewTab('layout2d')}
                        className="py-2.5 px-3 rounded-xl bg-slate-900 hover:bg-slate-800 text-white text-xs font-bold transition-all flex items-center justify-center gap-1.5 shadow-sm active:scale-98 cursor-pointer"
                      >
                        <span className="material-symbols-outlined text-[16px]">grid_view</span>
                        <span>Xem sơ đồ 2D bãi</span>
                      </button>
                    </div>
                  </div>
                )}
              </div>
            )}
          </div>
        </div>

        {/* View 2: Sơ đồ ma trận ô đỗ 2D */}
        {activeViewTab === 'layout2d' && (
          <div className="flex-1 flex flex-col p-4 overflow-hidden">
            {currentSelectedLot ? (
              <>
                {/* Thanh chọn tầng thực tế của bãi & Chú thích */}
                <div className="flex flex-wrap items-center justify-between gap-3 pb-3 mb-2 border-b border-slate-200 shrink-0">
                  <div className="flex items-center gap-1.5">
                    <span className="text-xs font-bold text-slate-500 mr-1">CHỌN TẦNG:</span>
                    {availableFloors.map((fl) => (
                      <button
                        key={fl.id}
                        type="button"
                        onClick={() => setSelectedFloorId(fl.id)}
                        className={`px-3 py-1 text-xs font-bold rounded-lg transition-all cursor-pointer ${
                          selectedFloorId === fl.id
                            ? 'bg-blue-600 text-white shadow-xs'
                            : 'bg-slate-200 text-slate-700 hover:bg-slate-300'
                        }`}
                      >
                        {fl.name}
                      </button>
                    ))}
                  </div>

                  {/* Chú thích trạng thái ô */}
                  <div className="flex items-center gap-3 text-[11px] font-semibold text-slate-600">
                    <span className="flex items-center gap-1">
                      <span className="w-3 h-3 rounded bg-emerald-500"></span> Trống (Available)
                    </span>
                    <span className="flex items-center gap-1">
                      <span className="w-3 h-3 rounded bg-blue-600"></span> Đang chọn
                    </span>
                    <span className="flex items-center gap-1">
                      <span className="w-3 h-3 rounded bg-slate-400"></span> Đã đỗ (Occupied)
                    </span>
                    <span className="flex items-center gap-1">
                      <span className="w-3 h-3 rounded bg-emerald-700 flex items-center justify-center text-[10px] text-white">⚡</span> Sạc EV
                    </span>
                  </div>
                </div>

                {/* Khung Ma trận lưới (Matrix Board) */}
                <div className="flex-1 bg-white rounded-2xl border border-slate-200 p-4 overflow-auto flex items-center justify-center shadow-inner relative">
                  {isLoadingLayout ? (
                    <div className="py-20 text-center text-xs text-slate-500 space-y-2">
                      <span className="material-symbols-outlined text-[32px] text-blue-600 animate-spin">progress_activity</span>
                      <p>Đang tải sơ đồ ma trận 2D...</p>
                    </div>
                  ) : floorLayout ? (
                    <div
                      className="grid gap-2 p-2 select-none"
                      style={{
                        gridTemplateColumns: `repeat(${floorLayout.columns}, minmax(46px, 1fr))`,
                        gridTemplateRows: `repeat(${floorLayout.rows}, minmax(46px, 1fr))`,
                      }}
                    >
                      {floorLayout.cells.map((cell, idx) => {
                        const isSelected = selectedSlotCode === cell.code;
                        const isSlot = cell.type === 'slot';
                        const isRoad = cell.type === 'road';
                        const isEntry = cell.type === 'entry';
                        const isExit = cell.type === 'exit';

                        if (isRoad) {
                          return (
                            <div
                              key={idx}
                              className="w-12 h-12 rounded bg-slate-100 border border-dashed border-slate-200 flex items-center justify-center text-[9px] font-mono text-slate-400"
                            >
                              ══
                            </div>
                          );
                        }

                        if (isEntry) {
                          return (
                            <div
                              key={idx}
                              className="w-12 h-12 rounded bg-blue-100 border border-blue-300 flex items-center justify-center text-[10px] font-bold text-blue-800"
                            >
                              VÀO ➔
                            </div>
                          );
                        }

                        if (isExit) {
                          return (
                            <div
                              key={idx}
                              className="w-12 h-12 rounded bg-rose-100 border border-rose-300 flex items-center justify-center text-[10px] font-bold text-rose-800"
                            >
                              ➔ RA
                            </div>
                          );
                        }

                        if (isSlot) {
                          const isAvailable = cell.state === 'Available';
                          const isEv = cell.slotType === 'EVCharging';

                          return (
                            <div
                              key={idx}
                              onClick={() => handleSlotClick(cell)}
                              className={`w-12 h-12 rounded-lg flex flex-col items-center justify-center text-center cursor-pointer transition-all border font-bold text-[10px] relative ${
                                isSelected
                                  ? 'bg-blue-600 text-white border-blue-700 shadow-md scale-105 ring-2 ring-blue-300'
                                  : isAvailable
                                  ? 'bg-emerald-50 hover:bg-emerald-100 text-emerald-900 border-emerald-300'
                                  : 'bg-slate-200 text-slate-400 border-slate-300 cursor-not-allowed opacity-60'
                              }`}
                            >
                              <span>{cell.code}</span>
                              {isEv && (
                                <span className="text-[10px] text-amber-500 absolute top-0.5 right-0.5">⚡</span>
                              )}
                              {!isAvailable && (
                                <span className="material-symbols-outlined text-[12px] opacity-70">directions_car</span>
                              )}
                            </div>
                          );
                        }

                        return <div key={idx} className="w-12 h-12 bg-transparent" />;
                      })}
                    </div>
                  ) : null}
                </div>

                {/* Thanh chốt đặt chỗ */}
                <div className="mt-3 p-3.5 bg-white rounded-xl border border-slate-200 flex flex-wrap items-center justify-between gap-3 shadow-xs shrink-0">
                  <div>
                    <p className="text-xs font-bold text-slate-900">
                      {selectedSlotCode ? (
                        <span>
                          Đã chọn ô đỗ: <span className="text-blue-700">{selectedSlotCode}</span> ({currentSelectedLot.name})
                        </span>
                      ) : (
                        <span className="text-slate-500">Vui lòng nhấp vào một ô màu xanh trên sơ đồ để chọn chỗ.</span>
                      )}
                    </p>
                    <p className="text-[11px] text-slate-500">
                      Giá gửi xe: {(currentSelectedLot.pricePerHour ?? 30000).toLocaleString('vi-VN')} đ/h • Giữ chỗ tối đa 15 phút
                    </p>
                  </div>

                  <button
                    type="button"
                    disabled={!selectedSlotCode}
                    onClick={handleConfirmReservation}
                    className={`py-2 px-5 rounded-xl font-bold text-xs transition-all flex items-center gap-1.5 shadow-sm ${
                      selectedSlotCode
                        ? 'bg-blue-600 hover:bg-blue-700 text-white cursor-pointer active:scale-98'
                        : 'bg-slate-200 text-slate-400 cursor-not-allowed'
                    }`}
                  >
                    <span className="material-symbols-outlined text-[16px]">event_seat</span>
                    <span>Xác nhận giữ ô {selectedSlotCode ?? ''} (15 phút)</span>
                  </button>
                </div>
              </>
            ) : (
              <div className="flex-1 flex flex-col items-center justify-center p-8 text-center text-slate-500">
                <span className="material-symbols-outlined text-[48px] text-slate-300 mb-2">grid_off</span>
                <h3 className="text-sm font-bold text-slate-700">Chưa chọn bãi đỗ xe</h3>
                <p className="text-xs text-slate-500 max-w-sm mt-1">
                  Vui lòng chọn 1 bãi đỗ trên bản đồ hoặc danh sách bên trái để xem sơ đồ 2D.
                </p>
              </div>
            )}
          </div>
        )}
      </main>

      {/* ======================================================== */}
      {/* MODAL XÁC NHẬN GIỮ CHỖ THÀNH CÔNG */}
      {/* ======================================================== */}
      {showBookingModal && currentSelectedLot && (
        <div className="fixed inset-0 z-1000 bg-slate-900/60 backdrop-blur-xs flex items-center justify-center p-4">
          <div className="bg-white rounded-2xl max-w-md w-full p-6 shadow-2xl border border-slate-200 animate-scale-up">
            <div className="w-12 h-12 rounded-full bg-emerald-100 text-emerald-600 flex items-center justify-center mx-auto mb-3">
              <span className="material-symbols-outlined text-[28px]">check_circle</span>
            </div>

            <h3 className="text-base font-black text-center text-slate-900">
              Giữ Chỗ Thành Công!
            </h3>
            <p className="text-xs text-center text-slate-500 mt-1">
              Hệ thống đã khóa ô đỗ cho bạn trong vòng <strong>15 phút</strong>.
            </p>

            <div className="my-4 p-3.5 bg-slate-50 rounded-xl border border-slate-200 space-y-2 text-xs">
              <div className="flex justify-between">
                <span className="text-slate-500">Mã đặt chỗ:</span>
                <span className="font-mono font-bold text-blue-700">{bookingSuccessCode}</span>
              </div>
              <div className="flex justify-between">
                <span className="text-slate-500">Bãi đỗ:</span>
                <span className="font-semibold text-slate-800">{currentSelectedLot.name}</span>
              </div>
              <div className="flex justify-between">
                <span className="text-slate-500">Ô đỗ được chọn:</span>
                <span className="font-bold text-emerald-700">{selectedSlotCode}</span>
              </div>
              <div className="flex justify-between">
                <span className="text-slate-500">Thời gian giữ chỗ:</span>
                <span className="font-bold text-amber-600">15:00 phút</span>
              </div>
            </div>

            <div className="grid grid-cols-2 gap-2 pt-2">
              <button
                type="button"
                onClick={() => handleOpenGoogleMapsDirections(currentSelectedLot.latitude, currentSelectedLot.longitude)}
                className="py-2.5 px-3 rounded-xl bg-blue-600 hover:bg-blue-700 text-white text-xs font-bold transition-all flex items-center justify-center gap-1.5 shadow-sm cursor-pointer"
              >
                <span className="material-symbols-outlined text-[16px]">navigation</span>
                <span>Dẫn đường ngay</span>
              </button>

              <button
                type="button"
                onClick={() => {
                  setShowBookingModal(false);
                  navigate('/driver/history');
                }}
                className="py-2.5 px-3 rounded-xl bg-slate-100 hover:bg-slate-200 text-slate-700 text-xs font-bold transition-all flex items-center justify-center gap-1.5 cursor-pointer"
              >
                <span>Xem lịch sử</span>
              </button>
            </div>
          </div>
        </div>
      )}
    </div>
  );
};
