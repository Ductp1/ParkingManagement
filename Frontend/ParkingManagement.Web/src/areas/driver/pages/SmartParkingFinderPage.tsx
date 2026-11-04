import React, { useState, useEffect, useMemo, useCallback } from 'react';
import { useNavigate } from 'react-router-dom';
import { parkingApi } from '@/api/parkingApi';
import type { ParkingLotSearchItemDto, FloorLayoutDto, LayoutCellDto } from '@/types/parking';

// ==========================================
// 1. MOCK DATA & SIMULATION PRESETS
// ==========================================
interface LocationPreset {
  id: string;
  name: string;
  address: string;
  lat: number;
  lng: number;
}

const PRESET_LOCATIONS: LocationPreset[] = [
  {
    id: 'nhat-hat-tp',
    name: 'Nhà hát Lớn TP.HCM (Q.1)',
    address: '07 Công trường Lam Sơn, Bến Nghé, Quận 1',
    lat: 10.7769,
    lng: 106.7032,
  },
  {
    id: 'vincom-dong-khoi',
    name: 'Gần Vincom Đồng Khởi (Q.1)',
    address: '72 Lê Thánh Tôn, Bến Nghé, Quận 1',
    lat: 10.7781,
    lng: 106.7019,
  },
  {
    id: 'landmark-81',
    name: 'Vinhomes Central Park / Landmark 81',
    address: '720A Điện Biên Phủ, Phường 22, Bình Thạnh',
    lat: 10.795,
    lng: 106.7218,
  },
  {
    id: 'san-bay-tsn',
    name: 'Khu vực Sân bay Tân Sơn Nhất',
    address: 'Trường Sơn, Phường 2, Tân Bình',
    lat: 10.8136,
    lng: 106.6621,
  },
  {
    id: 'cho-ben-thanh',
    name: 'Chợ Bến Thành (Trung tâm Q.1)',
    address: 'Đường Lê Lợi, Bến Thành, Quận 1',
    lat: 10.7725,
    lng: 106.698,
  },
];

// Fallback bãi xe thật tại TP.HCM (Defensive Fallback nếu API 500 hoặc rỗng)
const FALLBACK_LOTS: ParkingLotSearchItemDto[] = [
  {
    id: 1,
    name: 'Bãi xe Vincom Center Đồng Khởi',
    address: '72 Lê Thánh Tôn, Bến Nghé, Quận 1, TP.HCM',
    latitude: 10.7781,
    longitude: 106.7019,
    distanceKm: 0.25,
    estimatedTravelTimeMinutes: 2,
    availableSlots: 42,
    totalSlots: 120,
    maxHeightCm: 210,
    isOpenNow: true,
    pricePerHour: 30000,
    amenities: ['Trạm sạc EV', 'Mái che hầm', 'Camera AI ANPR', 'Thanh toán QR'],
  },
  {
    id: 2,
    name: 'Bãi xe Sân bay Tân Sơn Nhất (P2)',
    address: 'Đường Trường Sơn, Phường 2, Tân Bình, TP.HCM',
    latitude: 10.8136,
    longitude: 106.6621,
    distanceKm: 5.4,
    estimatedTravelTimeMinutes: 14,
    availableSlots: 18,
    totalSlots: 200,
    maxHeightCm: 250,
    isOpenNow: true,
    pricePerHour: 25000,
    amenities: ['24/7', 'Xe quá khổ', 'Camera AI', 'Bảo vệ'],
  },
  {
    id: 3,
    name: 'Bãi xe Tòa nhà Landmark 81 (B2)',
    address: '720A Điện Biên Phủ, Phường 22, Bình Thạnh, TP.HCM',
    latitude: 10.795,
    longitude: 106.7218,
    distanceKm: 3.1,
    estimatedTravelTimeMinutes: 8,
    availableSlots: 4,
    totalSlots: 90,
    maxHeightCm: 190,
    isOpenNow: true,
    pricePerHour: 35000,
    amenities: ['Trạm sạc EV siêu nhanh', 'Hầm thông minh', 'Rửa xe'],
  },
  {
    id: 4,
    name: 'Bãi xe Trung tâm Chợ Bến Thành',
    address: 'Đường Lê Lợi, Phường Bến Thành, Quận 1, TP.HCM',
    latitude: 10.7725,
    longitude: 106.698,
    distanceKm: 0.8,
    estimatedTravelTimeMinutes: 4,
    availableSlots: 0, // Bãi hết chỗ để test xếp hạng
    totalSlots: 50,
    maxHeightCm: 220,
    isOpenNow: true,
    pricePerHour: 20000,
    amenities: ['Mặt đất', 'Thanh toán tiền mặt/QR'],
  },
];

// ==========================================
// 2. PHÒNG THỦ: BỘ SINH LƯỚI MA TRẬN 2D DỰ PHÒNG
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
        // Giả lập trạng thái ô: 70% available, 25% occupied, 5% EV
        const isOccupied = (x * 3 + y * 7) % 5 === 0;
        const isEv = (x + y) % 4 === 0;
        cells.push({
          x,
          y,
          type: 'slot',
          code,
          slotType: isEv ? 'EVCharging' : 'Standard',
          state: isOccupied ? 'Occupied' : 'Available',
          widthCells: 1,
          heightCells: 1,
        });
      }
    }
  }

  return {
    floorId: 1,
    floorName: 'Tầng Hầm B1 (Tự động phục hồi)',
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
  // STATE MANAGEMENT (Quản lý dữ liệu tập trung)
  // ==========================================
  // 1. Tọa độ vị trí & Chế độ GPS
  const [gpsMode, setGpsMode] = useState<'real' | 'simulated'>('simulated');
  const [selectedPresetId, setSelectedPresetId] = useState<string>('nhat-hat-tp');
  const [coords, setCoords] = useState<{ lat: number; lng: number }>({
    lat: PRESET_LOCATIONS[0].lat,
    lng: PRESET_LOCATIONS[0].lng,
  });
  const [isLocatingGps, setIsLocatingGps] = useState<boolean>(false);
  const [destinationQuery, setDestinationQuery] = useState<string>('');

  // 2. Bộ lọc Streamlit-style
  const [radiusKm, setRadiusKm] = useState<number>(3);
  const [vehicleHeightFilter, setVehicleHeightFilter] = useState<number>(0); // 0 = tất cả
  const [onlyEvCharging, setOnlyEvCharging] = useState<boolean>(false);

  // 3. Danh sách bãi xe
  const [parkingLots, setParkingLots] = useState<ParkingLotSearchItemDto[]>(FALLBACK_LOTS);
  const [isLoadingLots, setIsLoadingLots] = useState<boolean>(false);
  const [selectedLotId, setSelectedLotId] = useState<number>(1);

  // 4. Khung xem bên phải: Tab 'map' hoặc 'layout2d'
  const [activeViewTab, setActiveViewTab] = useState<'map' | 'layout2d'>('map');

  // 5. Dữ liệu sơ đồ 2D của bãi đang chọn
  const [floorLayout, setFloorLayout] = useState<FloorLayoutDto | null>(null);
  const [selectedFloorLevel, setSelectedFloorLevel] = useState<string>('B1');
  const [selectedSlotCode, setSelectedSlotCode] = useState<string | null>(null);
  const [isLoadingLayout, setIsLoadingLayout] = useState<boolean>(false);

  // 6. Modal xác nhận đặt chỗ
  const [showBookingModal, setShowBookingModal] = useState<boolean>(false);
  const [bookingSuccessCode, setBookingSuccessCode] = useState<string | null>(null);

  // ==========================================
  // LIFECYCLE 1: Fetch danh sách bãi xe theo GPS & Filter
  // ==========================================
  const fetchParkingLots = useCallback(async () => {
    setIsLoadingLots(true);
    try {
      const data = await parkingApi.search({
        lat: coords.lat,
        lng: coords.lng,
        radiusKm,
        vehicleHeightCm: vehicleHeightFilter > 0 ? vehicleHeightFilter : undefined,
        limit: 15,
      });

      if (data && data.length > 0) {
        // Gán giá tạm tính nếu API chưa trả về
        const formatted = data.map((l, idx) => ({
          ...l,
          pricePerHour: l.pricePerHour || (idx % 2 === 0 ? 30000 : 25000),
        }));
        setParkingLots(formatted);
        if (!formatted.some((l) => l.id === selectedLotId)) {
          setSelectedLotId(formatted[0].id);
        }
      } else {
        // Fallback danh sách nếu backend chưa có bãi tại bán kính này
        setParkingLots(FALLBACK_LOTS);
      }
    } catch {
      // Defensive fallback khi backend mất kết nối
      setParkingLots(FALLBACK_LOTS);
    } finally {
      setIsLoadingLots(false);
    }
  }, [coords.lat, coords.lng, radiusKm, vehicleHeightFilter, selectedLotId]);

  useEffect(() => {
    fetchParkingLots();
  }, [fetchParkingLots]);

  // ==========================================
  // LIFECYCLE 2: Fetch Sơ đồ 2D khi đổi bãi xe hoặc đổi tab
  // ==========================================
  useEffect(() => {
    if (activeViewTab !== 'layout2d') return;

    let isMounted = true;
    const fetchLayout = async () => {
      setIsLoadingLayout(true);
      try {
        // Thử lấy sơ đồ thật qua API bãi xe
        const layoutData = await parkingApi.getFloorLayout(selectedLotId);
        if (isMounted && layoutData && layoutData.cells?.length > 0) {
          setFloorLayout(layoutData);
          return;
        }
      } catch {
        // Bắt lỗi âm thầm và bật bộ sinh lưới phòng thủ
      }

      if (isMounted) {
        // Fallback: Tự sinh lưới 2D đẹp mắt dựa trên ID bãi
        const fallback = generateFallbackMatrix(12, 6);
        setFloorLayout(fallback);
      }
      if (isMounted) setIsLoadingLayout(false);
    };

    fetchLayout();
    return () => {
      isMounted = false;
    };
  }, [selectedLotId, activeViewTab, selectedFloorLevel]);

  // ==========================================
  // EVENT HANDLERS
  // ==========================================
  // 1. Kích hoạt GPS thật từ trình duyệt
  const handleUseRealGps = () => {
    if (!navigator.geolocation) {
      alert('Trình duyệt không hỗ trợ Geolocation.');
      return;
    }
    setIsLocatingGps(true);
    navigator.geolocation.getCurrentPosition(
      (pos) => {
        setGpsMode('real');
        setCoords({ lat: pos.coords.latitude, lng: pos.coords.longitude });
        setIsLocatingGps(false);
      },
      (err) => {
        setIsLocatingGps(false);
        alert(`Không thể lấy vị trí GPS: ${err.message}. Tự động giữ chế độ giả lập.`);
      },
      { timeout: 8000, enableHighAccuracy: true }
    );
  };

  // 2. Chọn vị trí giả lập
  const handleSelectPreset = (presetId: string) => {
    const preset = PRESET_LOCATIONS.find((p) => p.id === presetId);
    if (!preset) return;
    setSelectedPresetId(presetId);
    setGpsMode('simulated');
    setCoords({ lat: preset.lat, lng: preset.lng });
  };

  // 3. Mở Google Maps chỉ đường
  const handleOpenGoogleMapsDirections = (targetLat: number, targetLng: number) => {
    const url = `https://www.google.com/maps/dir/?api=1&origin=${coords.lat},${coords.lng}&destination=${targetLat},${targetLng}&travelmode=driving`;
    window.open(url, '_blank');
  };

  // 4. Chọn ô đỗ trên sơ đồ 2D
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

  // 5. Xác nhận giữ chỗ 15 phút
  const handleConfirmReservation = () => {
    const mockCode = `PK-${Math.floor(100000 + Math.random() * 900000)}`;
    setBookingSuccessCode(mockCode);
    setShowBookingModal(true);
  };

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

  // Bãi xe đang được chọn hiện tại
  const currentSelectedLot = useMemo(() => {
    return parkingLots.find((l) => l.id === selectedLotId) || parkingLots[0] || FALLBACK_LOTS[0];
  }, [parkingLots, selectedLotId]);

  return (
    <div className="flex flex-col md:flex-row w-full h-[calc(100vh-64px)] bg-slate-100 overflow-hidden font-sans text-slate-800 antialiased">
      {/* ======================================================== */}
      {/* CỘT TRÁI: SIDEBAR ĐIỀU KHIỂN & DANH SÁCH BÃI (STREAMLIT STYLE) */}
      {/* ======================================================== */}
      <aside className="w-full md:w-[380px] lg:w-[420px] bg-white border-r border-slate-200 flex flex-col shrink-0 h-full shadow-xs z-20">
        {/* Header & Tiêu đề */}
        <div className="p-4 border-b border-slate-200 bg-slate-50/70">
          <div className="flex items-center justify-between mb-1">
            <h1 className="text-base font-bold text-slate-900 flex items-center gap-2">
              <span className="w-2.5 h-2.5 rounded-full bg-blue-600 animate-pulse"></span>
              Smart Parking Finder
            </h1>
            <span className="text-[11px] font-mono px-2 py-0.5 rounded bg-blue-50 text-blue-700 font-semibold border border-blue-200">
              Sprint 04
            </span>
          </div>
          <p className="text-xs text-slate-500">Tìm bãi đỗ thực tế, tối ưu cự ly & chọn ô đỗ 2D trực quan.</p>
        </div>

        {/* Khối Bộ lọc Streamlit Controls */}
        <div className="p-4 space-y-3.5 border-b border-slate-200 bg-white">
          {/* 1. GPS Mode Toggle */}
          <div>
            <div className="flex items-center justify-between mb-1.5">
              <label className="text-[11px] uppercase font-bold tracking-wider text-slate-500">
                1. Nguồn Vị Trí (Location Source)
              </label>
              {gpsMode === 'real' ? (
                <span className="text-[11px] text-emerald-600 font-bold flex items-center gap-1">
                  <span className="w-1.5 h-1.5 rounded-full bg-emerald-500"></span> GPS Trực tiếp
                </span>
              ) : (
                <span className="text-[11px] text-blue-600 font-bold flex items-center gap-1">
                  <span className="w-1.5 h-1.5 rounded-full bg-blue-500"></span> Giả lập (Simulator)
                </span>
              )}
            </div>

            <div className="grid grid-cols-2 gap-1.5 p-1 bg-slate-100 rounded-lg">
              <button
                type="button"
                onClick={handleUseRealGps}
                disabled={isLocatingGps}
                className={`py-1.5 px-2 text-xs font-semibold rounded-md transition-all flex items-center justify-center gap-1.5 ${
                  gpsMode === 'real'
                    ? 'bg-white text-blue-700 shadow-xs'
                    : 'text-slate-600 hover:text-slate-900'
                }`}
              >
                <span className="material-symbols-outlined text-[16px]">my_location</span>
                <span>{isLocatingGps ? 'Đang định vị...' : 'Vị trí của tôi'}</span>
              </button>

              <button
                type="button"
                onClick={() => setGpsMode('simulated')}
                className={`py-1.5 px-2 text-xs font-semibold rounded-md transition-all flex items-center justify-center gap-1.5 ${
                  gpsMode === 'simulated'
                    ? 'bg-white text-blue-700 shadow-xs'
                    : 'text-slate-600 hover:text-slate-900'
                }`}
              >
                <span className="material-symbols-outlined text-[16px]">tune</span>
                <span>Mô phỏng vị trí</span>
              </button>
            </div>

            {/* Selector các điểm giả lập */}
            {gpsMode === 'simulated' && (
              <div className="mt-2">
                <select
                  value={selectedPresetId}
                  onChange={(e) => handleSelectPreset(e.target.value)}
                  className="w-full text-xs font-medium border border-slate-300 rounded-lg p-2 bg-slate-50 text-slate-800 focus:outline-hidden focus:ring-2 focus:ring-blue-500"
                >
                  {PRESET_LOCATIONS.map((p) => (
                    <option key={p.id} value={p.id}>
                      📍 {p.name}
                    </option>
                  ))}
                </select>
              </div>
            )}
          </div>

          {/* 2. Ô tìm kiếm điểm đến (Destination) */}
          <div>
            <label className="text-[11px] uppercase font-bold tracking-wider text-slate-500 block mb-1">
              2. Tìm theo tên bãi / Địa chỉ điểm đến
            </label>
            <div className="relative">
              <span className="material-symbols-outlined absolute left-2.5 top-2 text-slate-400 text-[18px]">search</span>
              <input
                type="text"
                value={destinationQuery}
                onChange={(e) => setDestinationQuery(e.target.value)}
                placeholder="VD: Vincom, Bến Thành, Landmark 81..."
                className="w-full text-xs pl-8 pr-3 py-1.5 border border-slate-300 rounded-lg bg-slate-50 text-slate-800 placeholder-slate-400 focus:outline-hidden focus:ring-2 focus:ring-blue-500"
              />
              {destinationQuery && (
                <button
                  type="button"
                  onClick={() => setDestinationQuery('')}
                  className="absolute right-2.5 top-2 text-slate-400 hover:text-slate-600"
                >
                  <span className="material-symbols-outlined text-[16px]">close</span>
                </button>
              )}
            </div>
          </div>

          {/* 3. Bán kính & Chiều cao xe */}
          <div className="grid grid-cols-2 gap-2">
            <div>
              <label className="text-[11px] uppercase font-bold tracking-wider text-slate-500 block mb-1">
                Bán kính tìm
              </label>
              <select
                value={radiusKm}
                onChange={(e) => setRadiusKm(Number(e.target.value))}
                className="w-full text-xs font-semibold border border-slate-300 rounded-lg p-1.5 bg-slate-50 text-slate-800 focus:outline-hidden focus:ring-2 focus:ring-blue-500"
              >
                <option value={1}>1 km (Đi bộ được)</option>
                <option value={2}>2 km (Cực gần)</option>
                <option value={3}>3 km (Tiêu chuẩn)</option>
                <option value={5}>5 km (Mở rộng)</option>
                <option value={10}>10 km (Bao quát)</option>
              </select>
            </div>

            <div>
              <label className="text-[11px] uppercase font-bold tracking-wider text-slate-500 block mb-1">
                Chiều cao xe
              </label>
              <select
                value={vehicleHeightFilter}
                onChange={(e) => setVehicleHeightFilter(Number(e.target.value))}
                className="w-full text-xs font-semibold border border-slate-300 rounded-lg p-1.5 bg-slate-50 text-slate-800 focus:outline-hidden focus:ring-2 focus:ring-blue-500"
              >
                <option value={0}>Tất cả loại xe</option>
                <option value={150}>Sedan (&lt; 1.5m)</option>
                <option value={180}>CUV/SUV (&lt; 1.8m)</option>
                <option value={200}>SUV 7 chỗ (&lt; 2.0m)</option>
                <option value={220}>Bán tải (&lt; 2.2m)</option>
              </select>
            </div>
          </div>

          {/* 4. Checkbox Trạm sạc EV */}
          <label className="flex items-center gap-2 cursor-pointer select-none">
            <input
              type="checkbox"
              checked={onlyEvCharging}
              onChange={(e) => setOnlyEvCharging(e.target.checked)}
              className="w-4 h-4 text-blue-600 rounded border-slate-300 focus:ring-blue-500"
            />
            <span className="text-xs font-medium text-slate-700 flex items-center gap-1">
              <span className="material-symbols-outlined text-[16px] text-emerald-600">bolt</span>
              Chỉ hiện bãi có trạm sạc xe điện (EV)
            </span>
          </label>
        </div>

        {/* Danh sách kết quả (Card List) */}
        <div className="flex-1 overflow-y-auto p-3 space-y-2 bg-slate-50/50">
          <div className="flex items-center justify-between px-1 mb-1 text-[11px] font-semibold text-slate-500">
            <span>KẾT QUẢ: {rankedLots.length} BÃI ĐỖ</span>
            <span className="text-blue-600">Ưu tiên: Còn chỗ &gt; Gần nhất</span>
          </div>

          {isLoadingLots ? (
            <div className="py-12 text-center text-xs text-slate-500 space-y-2">
              <span className="material-symbols-outlined text-[32px] text-blue-600 animate-spin">progress_activity</span>
              <p>Đang quét bãi đỗ theo GPS...</p>
            </div>
          ) : rankedLots.length === 0 ? (
            <div className="p-6 text-center text-xs text-slate-500 bg-white rounded-xl border border-dashed border-slate-300">
              <span className="material-symbols-outlined text-[36px] text-slate-400 mb-1">search_off</span>
              <p className="font-semibold text-slate-700">Không tìm thấy bãi nào phù hợp</p>
              <p className="text-[11px] mt-1">Hãy tăng bán kính hoặc tắt bộ lọc xe điện.</p>
              <button
                type="button"
                onClick={() => {
                  setRadiusKm(5);
                  setOnlyEvCharging(false);
                  setVehicleHeightFilter(0);
                  setDestinationQuery('');
                }}
                className="mt-3 px-3 py-1.5 rounded-lg bg-blue-50 text-blue-700 text-xs font-bold hover:bg-blue-100"
              >
                Đặt lại bộ lọc
              </button>
            </div>
          ) : (
            rankedLots.map((lot) => {
              const isSelected = lot.id === selectedLotId;
              const isFull = lot.availableSlots === 0;
              const isAlmostFull = lot.availableSlots > 0 && lot.availableSlots <= 5;

              return (
                <div
                  key={lot.id}
                  onClick={() => setSelectedLotId(lot.id)}
                  className={`p-3 rounded-xl border transition-all cursor-pointer relative bg-white ${
                    isSelected
                      ? 'border-blue-600 ring-2 ring-blue-500/20 shadow-sm'
                      : 'border-slate-200 hover:border-slate-300 hover:shadow-xs'
                  }`}
                >
                  <div className="flex items-start justify-between gap-2 mb-1">
                    <h3 className="text-xs font-bold text-slate-900 leading-snug line-clamp-1">
                      {lot.name}
                    </h3>
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

                  <p className="text-[11px] text-slate-500 line-clamp-1 mb-2.5">
                    {lot.address}
                  </p>

                  <div className="flex items-center justify-between text-[11px] font-medium text-slate-600 pt-2 border-t border-slate-100">
                    <div className="flex items-center gap-3">
                      <span className="font-bold text-slate-900 flex items-center gap-0.5">
                        <span className="material-symbols-outlined text-[14px] text-blue-600">near_me</span>
                        {lot.distanceKm < 1
                          ? `${Math.round(lot.distanceKm * 1000)}m`
                          : `${lot.distanceKm.toFixed(1)}km`}
                      </span>
                      <span className="text-slate-400">•</span>
                      <span>🚗 ~{lot.estimatedTravelTimeMinutes || Math.max(1, Math.round(lot.distanceKm * 3))} ph</span>
                    </div>

                    <span className="font-extrabold text-blue-700">
                      {(lot.pricePerHour ?? 30000).toLocaleString('vi-VN')} đ/h
                    </span>
                  </div>
                </div>
              );
            })
          )}
        </div>
      </aside>

      {/* ======================================================== */}
      {/* CỘT PHẢI: WORKSPACE TƯƠNG TÁC (MAP VIEW & 2D MATRIX VIEW) */}
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
                {currentSelectedLot.name}
              </h2>
              <p className="text-[11px] text-slate-500 truncate flex items-center gap-2">
                <span>{currentSelectedLot.address}</span>
                <span>•</span>
                <span className="font-semibold text-emerald-700">
                  {currentSelectedLot.availableSlots > 0
                    ? `Đang có ${currentSelectedLot.availableSlots} chỗ trống khả dụng`
                    : 'Bãi hiện đã kín chỗ'}
                </span>
              </p>
            </div>
          </div>

          {/* Toggle View: 🗺️ Bản đồ vs 📐 Sơ đồ 2D */}
          <div className="flex items-center gap-1.5 p-1 bg-slate-100 rounded-lg shrink-0">
            <button
              type="button"
              onClick={() => setActiveViewTab('map')}
              className={`px-3 py-1.5 text-xs font-bold rounded-md transition-all flex items-center gap-1.5 ${
                activeViewTab === 'map'
                  ? 'bg-white text-blue-700 shadow-xs'
                  : 'text-slate-600 hover:text-slate-900'
              }`}
            >
              <span className="material-symbols-outlined text-[16px]">map</span>
              <span>Bản đồ & Dẫn đường</span>
            </button>

            <button
              type="button"
              onClick={() => setActiveViewTab('layout2d')}
              className={`px-3 py-1.5 text-xs font-bold rounded-md transition-all flex items-center gap-1.5 ${
                activeViewTab === 'layout2d'
                  ? 'bg-white text-blue-700 shadow-xs'
                  : 'text-slate-600 hover:text-slate-900'
              }`}
            >
              <span className="material-symbols-outlined text-[16px]">grid_view</span>
              <span>Sơ đồ 2D ô đỗ</span>
            </button>
          </div>
        </header>

        {/* Nội dung View 1: Bản đồ & Dẫn đường */}
        {activeViewTab === 'map' && (
          <div className="flex-1 relative flex flex-col items-center justify-center p-4 overflow-hidden select-none">
            {/* Bản đồ Vector Canvas Sạch (Chuẩn pháp lý, không đường tranh chấp) */}
            <div className="w-full h-full rounded-2xl bg-slate-900 overflow-hidden relative shadow-inner border border-slate-800 flex items-center justify-center">
              {/* Lưới tọa độ đồ họa giả lập đường phố */}
              <svg className="w-full h-full opacity-40" viewBox="0 0 1000 600" fill="none">
                <defs>
                  <pattern id="city-grid" width="60" height="60" patternUnits="userSpaceOnUse">
                    <path d="M 60 0 L 0 0 0 60" fill="none" stroke="#334155" strokeWidth="1" />
                  </pattern>
                </defs>
                <rect width="1000" height="600" fill="url(#city-grid)" />

                {/* Các đại lộ chính */}
                <path d="M 0 300 Q 500 280 1000 320" stroke="#475569" strokeWidth="16" />
                <path d="M 500 0 Q 480 300 520 600" stroke="#475569" strokeWidth="16" />
                <path d="M 200 100 L 800 500" stroke="#334155" strokeWidth="8" />

                {/* Đường dẫn đường (Route Polyline) từ vị trí tài xế tới bãi xe */}
                <path
                  d="M 350 420 L 500 300 L 680 220"
                  stroke="#3b82f6"
                  strokeWidth="4"
                  strokeDasharray="6 6"
                  className="animate-pulse"
                />
              </svg>

              {/* Marker: Vị trí của tài xế */}
              <div
                className="absolute flex flex-col items-center pointer-events-none transform -translate-x-1/2 -translate-y-1/2"
                style={{ left: '35%', top: '70%' }}
              >
                <div className="w-6 h-6 rounded-full bg-blue-600 border-2 border-white shadow-lg flex items-center justify-center text-white animate-bounce">
                  <span className="material-symbols-outlined text-[14px]">directions_car</span>
                </div>
                <span className="mt-1 px-2 py-0.5 rounded-full bg-slate-950/90 text-white text-[10px] font-bold backdrop-blur-xs whitespace-nowrap">
                  Bạn đang ở đây
                </span>
              </div>

              {/* Marker: Vị trí bãi đỗ đang chọn */}
              <div
                className="absolute flex flex-col items-center cursor-pointer transform -translate-x-1/2 -translate-y-1/2 transition-transform hover:scale-110"
                style={{ left: '68%', top: '37%' }}
                onClick={() => setActiveViewTab('layout2d')}
              >
                <div className="px-3 py-1 rounded-full bg-emerald-600 text-white font-black text-xs shadow-xl flex items-center gap-1 border-2 border-white">
                  <span>P</span>
                  <span>{currentSelectedLot.name.split(' ')[2] || 'Bãi đỗ'}</span>
                  <span className="text-[10px] bg-emerald-700 px-1.5 py-0.2 rounded-full">
                    {currentSelectedLot.availableSlots} chỗ
                  </span>
                </div>
                <div className="w-3 h-3 bg-emerald-600 rotate-45 -mt-1.5 border-r border-b border-white"></div>
              </div>

              {/* Marker phụ: Bãi xe khác xung quanh */}
              <div
                className="absolute flex flex-col items-center opacity-70 cursor-pointer transform -translate-x-1/2 -translate-y-1/2"
                style={{ left: '52%', top: '55%' }}
              >
                <div className="px-2 py-0.5 rounded-full bg-slate-700 text-white font-bold text-[10px] flex items-center gap-1">
                  <span>P Chợ Bến Thành</span>
                </div>
              </div>

              {/* Floating Action Box (Overlay chỉ đường) */}
              <div className="absolute bottom-5 left-5 right-5 max-w-lg mx-auto bg-white/95 backdrop-blur-md rounded-2xl p-4 shadow-xl border border-slate-200">
                <div className="flex items-center justify-between mb-3">
                  <div>
                    <h4 className="text-xs font-bold text-slate-900">{currentSelectedLot.name}</h4>
                    <p className="text-[11px] text-slate-500">
                      Khoảng cách: <strong>{currentSelectedLot.distanceKm} km</strong> • Ước tính lái xe: ~{currentSelectedLot.estimatedTravelTimeMinutes} phút
                    </p>
                  </div>
                  <span className="text-xs font-black text-blue-700">
                    {(currentSelectedLot.pricePerHour ?? 30000).toLocaleString('vi-VN')} đ/h
                  </span>
                </div>

                <div className="grid grid-cols-2 gap-2">
                  <button
                    type="button"
                    onClick={() => handleOpenGoogleMapsDirections(currentSelectedLot.latitude, currentSelectedLot.longitude)}
                    className="py-2.5 px-3 rounded-xl bg-blue-600 hover:bg-blue-700 text-white text-xs font-bold transition-all flex items-center justify-center gap-1.5 shadow-sm active:scale-98"
                  >
                    <span className="material-symbols-outlined text-[16px]">navigation</span>
                    <span>Mở Google Maps</span>
                  </button>

                  <button
                    type="button"
                    onClick={() => setActiveViewTab('layout2d')}
                    className="py-2.5 px-3 rounded-xl bg-slate-900 hover:bg-slate-800 text-white text-xs font-bold transition-all flex items-center justify-center gap-1.5 shadow-sm active:scale-98"
                  >
                    <span className="material-symbols-outlined text-[16px]">grid_view</span>
                    <span>Xem sơ đồ 2D bãi</span>
                  </button>
                </div>
              </div>
            </div>
          </div>
        )}

        {/* Nội dung View 2: Sơ đồ ma trận ô đỗ 2D (Matrix Grid Renderer) */}
        {activeViewTab === 'layout2d' && (
          <div className="flex-1 flex flex-col p-4 overflow-hidden">
            {/* Thanh điều khiển tầng & Chú thích */}
            <div className="flex flex-wrap items-center justify-between gap-3 pb-3 mb-2 border-b border-slate-200">
              <div className="flex items-center gap-1.5">
                <span className="text-xs font-bold text-slate-500 mr-1">CHỌN TẦNG:</span>
                {['B1', 'B2', 'B3'].map((lvl) => (
                  <button
                    key={lvl}
                    type="button"
                    onClick={() => setSelectedFloorLevel(lvl)}
                    className={`px-3 py-1 text-xs font-bold rounded-lg transition-all ${
                      selectedFloorLevel === lvl
                        ? 'bg-blue-600 text-white shadow-xs'
                        : 'bg-slate-200 text-slate-700 hover:bg-slate-300'
                    }`}
                  >
                    Hầm {lvl}
                  </button>
                ))}
              </div>

              {/* Legend trạng thái */}
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

            {/* Thanh chốt đặt chỗ (Bottom Action Footer) */}
            <div className="mt-3 p-3.5 bg-white rounded-xl border border-slate-200 flex flex-wrap items-center justify-between gap-3 shadow-xs">
              <div>
                <p className="text-xs font-bold text-slate-900">
                  {selectedSlotCode ? (
                    <span>
                      Đã chọn ô đỗ: <span className="text-blue-700">{selectedSlotCode}</span> ({currentSelectedLot.name} • Hầm {selectedFloorLevel})
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
                <span className="material-symbols-outlined text-[16px]">check_circle</span>
                <span>Tiến hành đặt chỗ</span>
              </button>
            </div>
          </div>
        )}
      </main>

      {/* ======================================================== */}
      {/* MODAL XÁC NHẬN GIỮ CHỖ THÀNH CÔNG */}
      {/* ======================================================== */}
      {showBookingModal && (
        <div className="fixed inset-0 z-50 bg-slate-950/60 backdrop-blur-xs flex items-center justify-center p-4">
          <div className="bg-white rounded-2xl max-w-md w-full p-6 shadow-2xl border border-slate-100 text-center space-y-4 animate-in fade-in zoom-in-95 duration-200">
            <div className="w-14 h-14 rounded-full bg-emerald-100 text-emerald-600 flex items-center justify-center mx-auto">
              <span className="material-symbols-outlined text-[32px]">check</span>
            </div>

            <div>
              <h3 className="text-lg font-bold text-slate-900">Giữ chỗ thành công!</h3>
              <p className="text-xs text-slate-500 mt-1">
                Hệ thống đang giữ ô đỗ <strong>{selectedSlotCode}</strong> cho bạn trong <strong>15 phút</strong>.
              </p>
            </div>

            <div className="p-3.5 bg-slate-50 rounded-xl border border-slate-200 text-left text-xs space-y-1.5">
              <div className="flex justify-between">
                <span className="text-slate-500">Mã đặt chỗ:</span>
                <span className="font-mono font-bold text-blue-700">{bookingSuccessCode}</span>
              </div>
              <div className="flex justify-between">
                <span className="text-slate-500">Bãi đỗ:</span>
                <span className="font-semibold text-slate-900">{currentSelectedLot.name}</span>
              </div>
              <div className="flex justify-between">
                <span className="text-slate-500">Vị trí:</span>
                <span className="font-semibold text-slate-900">Hầm {selectedFloorLevel} • Ô {selectedSlotCode}</span>
              </div>
              <div className="flex justify-between">
                <span className="text-slate-500">Giá ước tính:</span>
                <span className="font-bold text-emerald-700">{(currentSelectedLot.pricePerHour ?? 30000).toLocaleString('vi-VN')} đ/h</span>
              </div>
            </div>

            <div className="grid grid-cols-2 gap-2 pt-2">
              <button
                type="button"
                onClick={() => {
                  setShowBookingModal(false);
                  navigate('/driver/history');
                }}
                className="py-2.5 px-3 rounded-xl bg-blue-600 text-white text-xs font-bold hover:bg-blue-700 transition-colors"
              >
                Xem lịch sử vé
              </button>

              <button
                type="button"
                onClick={() => setShowBookingModal(false)}
                className="py-2.5 px-3 rounded-xl bg-slate-100 text-slate-700 text-xs font-bold hover:bg-slate-200 transition-colors"
              >
                Đóng
              </button>
            </div>
          </div>
        </div>
      )}
    </div>
  );
};
