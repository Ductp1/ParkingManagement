import React, { useState, useEffect } from 'react';
import { useNavigate, useParams } from 'react-router-dom';
import { useAppSelector } from '@/store';
import { parkingApi } from '@/api/parkingApi';
import type { ParkingLotDto } from '@/types/parking';

interface SpotInfo {
  code: string;
  desc: string;
  isEv: boolean;
  status: 'available' | 'occupied' | 'selected';
  x: string;
  y: string;
  badgeText: string;
  typeText: string;
}

const AVAILABLE_SPOTS: SpotInfo[] = [
  {
    code: 'B-02',
    desc: 'Khu B • Vị trí VIP sát sảnh thang máy Landmark 81 • 25 mét',
    isEv: false,
    status: 'available',
    x: '28%',
    y: '38%',
    badgeText: 'VIP Gần thang máy',
    typeText: 'VIP Thang máy',
  },
  {
    code: 'B-04',
    desc: 'Khu B • Cạnh trụ B-12 • Sạc nhanh 120kW • Cách thang máy 45m',
    isEv: true,
    status: 'selected',
    x: '38%',
    y: '48%',
    badgeText: 'Đã chọn (Sạc EV 120kW)',
    typeText: 'Sạc EV 120kW',
  },
  {
    code: 'B-05',
    desc: 'Khu B • Trạm sạc EV siêu nhanh 120kW • Cạnh cổng kiểm soát • Cách thang máy 55m',
    isEv: true,
    status: 'available',
    x: '52%',
    y: '48%',
    badgeText: 'Trống (Sạc EV)',
    typeText: 'Sạc EV 120kW',
  },
  {
    code: 'B-06',
    desc: 'Khu B • Ô đậu tiêu chuẩn SUV/Sedan • Cạnh trụ B-14 • Cách thang máy 60m',
    isEv: false,
    status: 'available',
    x: '66%',
    y: '48%',
    badgeText: 'Trống (SUV)',
    typeText: 'SUV/Sedan',
  },
];

export const LotDetailPage: React.FC = () => {
  const { id } = useParams<{ id: string }>();
  const lotId = id ? parseInt(id, 10) : 1;
  const navigate = useNavigate();

  const { vehicles } = useAppSelector((state) => state.vehicle);

  const [lot, setLot] = useState<ParkingLotDto | null>(null);
  const [selectedFloor, setSelectedFloor] = useState<'B1' | 'B2' | 'B3' | 'F1'>('B2');
  const [selectedSpotCode, setSelectedSpotCode] = useState<string>('B-04');
  const [hasEvService, setHasEvService] = useState<boolean>(true);
  const [hasWashService, setHasWashService] = useState<boolean>(false);
  const [hasInsurance] = useState<boolean>(true);
  const [bookingNote, setBookingNote] = useState<string>('');
  const [paymentMethod, setPaymentMethod] = useState<'wallet' | 'momo' | 'card' | 'etc'>('wallet');
  const [showSuccessModal, setShowSuccessModal] = useState<boolean>(false);
  const [showStickyStepper, setShowStickyStepper] = useState<boolean>(false);
  const [countdown, setCountdown] = useState<number>(493); // 8m 13s in seconds

  // Fetch lot info from API
  useEffect(() => {
    parkingApi
      .getById(lotId)
      .then((data) => setLot(data))
      .catch(() => {
        // Fallback default
      });
  }, [lotId]);

  // Scroll listener for sticky stepper
  useEffect(() => {
    const handleScroll = () => {
      if (window.scrollY > 300) {
        setShowStickyStepper(true);
      } else {
        setShowStickyStepper(false);
      }
    };
    window.addEventListener('scroll', handleScroll, { passive: true });
    return () => window.removeEventListener('scroll', handleScroll);
  }, []);

  // Countdown timer effect
  useEffect(() => {
    const timer = setInterval(() => {
      setCountdown((prev) => (prev > 0 ? prev - 1 : 600));
    }, 1000);
    return () => clearInterval(timer);
  }, []);

  const formatCountdown = (seconds: number) => {
    const m = Math.floor(seconds / 60);
    const s = seconds % 60;
    return `${String(m).padStart(2, '0')}:${String(s).padStart(2, '0')}`;
  };

  const currentSpot = AVAILABLE_SPOTS.find((s) => s.code === selectedSpotCode) || AVAILABLE_SPOTS[1];

  // Default vehicle selection (from real user vehicles in Redux or fallback)
  const defaultVehicle = vehicles.find((v) => v.isDefault) || vehicles[0];
  const vehiclePlate = defaultVehicle?.plateDisplay || defaultVehicle?.plateNumber || '51K - 889.32';
  const vehicleBrand = defaultVehicle?.brand || 'Mercedes-Benz';
  const vehicleModel = defaultVehicle?.model || 'EQE SUV';
  const vehicleColor = defaultVehicle?.color || 'Xanh Obsidian';
  const vehicleFuel = defaultVehicle?.fuelType || 'Electric';

  // Pricing calculations
  const hourlyRate = 30000;
  const durationHours = 4;
  const parkingFee = hourlyRate * durationHours; // 120.000
  const techFee = 10000;
  const vipDiscount = 24000; // -20%
  const evFee = hasEvService ? 50000 : 0;
  const washFee = hasWashService ? 80000 : 0;
  const totalAmount = parkingFee + techFee - vipDiscount + evFee + washFee;

  const handleSpotSelect = (spotCode: string) => {
    setSelectedSpotCode(spotCode);
    const spot = AVAILABLE_SPOTS.find((s) => s.code === spotCode);
    if (spot) {
      setHasEvService(spot.isEv);
    }
  };

  const handleConfirmBooking = () => {
    setShowSuccessModal(true);
  };

  return (
    <div className="min-h-screen bg-slate-50 flex flex-col font-sans">
      {/* 1. Smart Sticky Stepper (Floats under header on scroll) */}
      <div
        className={`fixed top-16 left-0 right-0 z-40 bg-white/95 backdrop-blur-md border-b border-slate-200 shadow-sm transition-all duration-300 ease-in-out ${
          showStickyStepper ? 'translate-y-0 opacity-100 pointer-events-auto' : '-translate-y-full opacity-0 pointer-events-none'
        }`}
      >
        <div className="max-w-4xl mx-auto px-4 py-3 flex items-center justify-between">
          {/* Step 1 */}
          <div
            className="flex items-center gap-3 cursor-pointer group flex-1"
            onClick={() => window.scrollTo({ top: 0, behavior: 'smooth' })}
          >
            <div className="w-8 h-8 rounded-full bg-blue-600 text-white flex items-center justify-center font-bold text-xs shadow-sm">
              <span className="material-symbols-outlined text-[16px]">check</span>
            </div>
            <div className="flex flex-col min-w-0">
              <span className="text-xs font-bold text-slate-800 truncate">1. Chọn bãi đỗ</span>
              <span className="text-[11px] text-slate-500 truncate">{lot?.name || 'Landmark 81 Garage'}</span>
            </div>
          </div>

          <div className="h-0.5 flex-1 mx-3 bg-blue-600 rounded-full hidden sm:block"></div>

          {/* Step 2 */}
          <div
            className="flex items-center gap-3 cursor-pointer group flex-1 justify-center sm:justify-start"
            onClick={() => document.getElementById('map-section')?.scrollIntoView({ behavior: 'smooth' })}
          >
            <div className="w-8 h-8 rounded-full bg-blue-600 text-white flex items-center justify-center font-bold text-xs shadow-md ring-4 ring-blue-100">
              <span>2</span>
            </div>
            <div className="flex flex-col min-w-0">
              <span className="text-xs font-bold text-blue-600 truncate">2. Chọn vị trí</span>
              <span className="text-[11px] text-blue-600 font-medium truncate">
                Tầng Hầm {selectedFloor} • Ô {selectedSpotCode}
              </span>
            </div>
          </div>

          <div className="h-0.5 flex-1 mx-3 bg-slate-200 rounded-full hidden sm:block"></div>

          {/* Step 3 */}
          <div
            className="flex items-center gap-3 cursor-pointer group flex-1 justify-end sm:justify-start"
            onClick={() => document.getElementById('booking-section')?.scrollIntoView({ behavior: 'smooth' })}
          >
            <div className="w-8 h-8 rounded-full bg-slate-100 text-slate-600 border border-slate-300 flex items-center justify-center font-bold text-xs">
              <span>3</span>
            </div>
            <div className="flex flex-col min-w-0 text-left">
              <span className="text-xs font-semibold text-slate-600 truncate">3. Thanh toán</span>
              <span className="text-[11px] text-slate-400 truncate">{totalAmount.toLocaleString('vi-VN')} đ</span>
            </div>
          </div>
        </div>
      </div>

      {/* 2. Hero Section with Cover Image */}
      <div className="relative w-full h-80 overflow-hidden bg-slate-900">
        <img
          src={lot?.coverImageUrl || "https://images.unsplash.com/photo-1506521781263-d8422e82f27a?auto=format&fit=crop&w=1600&q=80"}
          alt={lot?.name || "Landmark 81 Underground Garage"}
          className="w-full h-full object-cover opacity-80"
        />
        <div className="absolute inset-0 bg-gradient-to-t from-slate-950/90 via-slate-900/40 to-transparent"></div>

        <div className="absolute top-6 left-4 sm:left-8 lg:left-12 flex flex-wrap items-center gap-3">
          <div className="inline-flex items-center gap-2 px-3.5 py-1.5 rounded-full bg-white/90 backdrop-blur-md shadow-sm">
            <span className="w-2.5 h-2.5 rounded-full bg-emerald-500 animate-pulse"></span>
            <span className="text-xs text-slate-900 uppercase tracking-wider font-bold">CAMERA AI ANPR LIVE</span>
          </div>
          <div className="inline-flex items-center gap-1.5 px-3.5 py-1.5 rounded-full bg-white/90 backdrop-blur-md shadow-sm">
            <span className="material-symbols-outlined text-[16px] text-blue-600">bolt</span>
            <span className="text-xs text-slate-900 font-bold">Siêu sạc DC 120kW</span>
          </div>
        </div>

        <div className="absolute bottom-6 right-4 sm:right-8 lg:right-12 hidden md:flex items-center gap-2 px-4 py-2 rounded-xl bg-white/90 backdrop-blur-md shadow-md text-slate-900">
          <span className="material-symbols-outlined text-blue-600 text-[20px]">verified</span>
          <span className="text-xs font-bold">Hệ sinh thái ParkMaster Premium</span>
        </div>
      </div>

      {/* Main Content Area */}
      <div className="w-full max-w-7xl mx-auto px-4 sm:px-6 lg:px-8 pb-20">
        {/* AC3 Warning Alert if Lot is Suspended/Not Bookable */}
        {lot && (!lot.canBook || lot.status !== 'Active') && (
          <div className="mt-6 p-4 rounded-2xl bg-amber-50 border border-amber-300 text-amber-900 flex items-start gap-3 shadow-sm">
            <span className="material-symbols-outlined text-amber-600 text-[26px] shrink-0">warning</span>
            <div>
              <h4 className="font-bold text-sm">Bãi đỗ này hiện đang tạm ngừng tiếp nhận đặt chỗ online</h4>
              <p className="text-xs text-amber-800 mt-1">
                Trạng thái hiện tại: <strong>{lot.status}</strong>. Hệ thống tạm khóa luồng đặt giữ chỗ trực tuyến để đảm bảo an toàn. Quý khách vui lòng liên hệ hotline chung của bãi: <strong className="underline">{lot.hotlinePhone || '1900 6868'}</strong> để được hỗ trợ.
              </p>
            </div>
          </div>
        )}

        {/* 3. White Overlapping Info Card */}
        <div className="relative z-10 -mt-16 mx-auto max-w-7xl shadow-lg rounded-2xl bg-white p-6 sm:p-8 border border-slate-100 mb-10">
          <div className="flex flex-col lg:flex-row lg:items-center justify-between gap-6">
            <div className="flex flex-col sm:flex-row items-start sm:items-center gap-5">
              <div className="w-20 h-20 sm:w-24 sm:h-24 rounded-2xl bg-gradient-to-br from-blue-600 to-indigo-700 flex items-center justify-center text-white shadow-lg shrink-0">
                <span className="material-symbols-outlined text-[42px]">apartment</span>
              </div>
              <div className="flex flex-col">
                <div className="flex flex-wrap items-center gap-3 mb-1">
                  <h1 className="text-2xl sm:text-3xl font-extrabold text-slate-900 tracking-tight">
                    {lot?.name || 'Landmark 81 Parking Garage'}
                  </h1>
                  <span className="px-2.5 py-1 rounded-full bg-blue-50 text-blue-700 text-[11px] font-bold uppercase tracking-wider flex items-center gap-1">
                    <span className="material-symbols-outlined text-[14px]">stars</span> TÒA THÁP BIỂU TƯỢNG TP.HCM
                  </span>
                </div>
                <p className="text-sm text-slate-500 flex items-center gap-1.5 mb-3">
                  <span className="material-symbols-outlined text-[18px] text-slate-400">location_on</span>
                  {lot?.address || '208 Nguyễn Hữu Cảnh, Phường 22, Bình Thạnh, TP. Hồ Chí Minh'}
                </p>
                <div className="flex flex-wrap items-center gap-2 sm:gap-4">
                  <span className="inline-flex items-center gap-1.5 px-3 py-1 rounded-lg bg-slate-100 text-xs text-slate-800 font-medium">
                    <span className="material-symbols-outlined text-[16px] text-emerald-600">schedule</span>
                    Mở cửa: {lot?.openingHours || '24/7 (Cả ngày lễ)'}
                  </span>
                  <span className="inline-flex items-center gap-1.5 px-3 py-1 rounded-lg bg-emerald-50 text-xs text-emerald-700 font-bold">
                    <span className="w-2 h-2 rounded-full bg-emerald-500"></span>
                    Trống {lot ? `${lot.availableSlots}/${lot.totalSlots}` : '42/120'} chỗ đỗ
                  </span>
                  <span className="inline-flex items-center gap-1.5 px-3 py-1 rounded-lg bg-slate-100 text-xs text-slate-800 font-medium">
                    <span className="material-symbols-outlined text-[16px] text-blue-600">call</span>
                    Hotline: {lot?.hotlinePhone || '1900 6868'}
                  </span>
                  <span className="inline-flex items-center gap-1.5 px-3 py-1 rounded-lg bg-slate-100 text-xs text-slate-800 font-medium">
                    <span className="material-symbols-outlined text-[16px] text-amber-600">height</span>
                    Cao tối đa: {(lot?.maxHeightCm ?? 210) / 100}m
                  </span>
                </div>

                {/* Amenities Badges (US-018 AC1) */}
                {lot?.amenities && lot.amenities.length > 0 && (
                  <div className="flex flex-wrap items-center gap-1.5 mt-3 pt-3 border-t border-slate-100">
                    <span className="text-[11px] font-bold text-slate-400 uppercase tracking-wider mr-1">Tiện ích:</span>
                    {lot.amenities.map((item, idx) => (
                      <span key={idx} className="inline-flex items-center gap-1 px-2.5 py-0.5 rounded-full bg-blue-50 text-blue-700 text-xs font-semibold">
                        <span className="material-symbols-outlined text-[13px]">check_circle</span>
                        {item}
                      </span>
                    ))}
                  </div>
                )}
              </div>
            </div>

            <div className="flex items-center gap-3 pt-4 lg:pt-0">
              <button
                onClick={() => navigate('/driver')}
                className="flex-1 sm:flex-none px-4 py-2.5 rounded-xl bg-blue-50 text-blue-700 hover:bg-blue-100 transition-colors text-sm flex items-center justify-center gap-2 font-semibold shadow-xs"
                type="button"
              >
                <span className="material-symbols-outlined text-[18px]">sync_alt</span>
                <span>Đổi địa điểm</span>
              </button>
              <button
                aria-label="Chia sẻ"
                className="p-2.5 rounded-xl bg-slate-100 text-slate-600 hover:text-slate-900 hover:bg-slate-200 transition-colors flex items-center justify-center"
                type="button"
              >
                <span className="material-symbols-outlined text-[20px]">share</span>
              </button>
              <button
                aria-label="Lưu địa điểm"
                className="p-2.5 rounded-xl bg-slate-100 text-slate-600 hover:text-slate-900 hover:bg-slate-200 transition-colors flex items-center justify-center"
                type="button"
              >
                <span className="material-symbols-outlined text-[20px]">bookmark_border</span>
              </button>
            </div>
          </div>
        </div>

        {/* 4. Floor Selector Tabs */}
        <div className="flex flex-col md:flex-row md:items-center justify-between gap-4 p-3 bg-white rounded-2xl shadow-xs border border-slate-200 mb-6">
          <div className="flex flex-wrap items-center gap-2 p-1 bg-slate-100 rounded-xl">
            <button
              onClick={() => setSelectedFloor('B1')}
              className={`px-4 py-2 rounded-lg text-sm font-medium transition-colors ${
                selectedFloor === 'B1' ? 'bg-blue-600 text-white shadow-xs' : 'text-slate-600 hover:text-slate-900'
              }`}
            >
              Tầng Hầm B1 (Kín 95%)
            </button>
            <button
              onClick={() => setSelectedFloor('B2')}
              className={`px-4 py-2 rounded-lg text-sm font-semibold transition-colors flex items-center gap-1.5 ${
                selectedFloor === 'B2' ? 'bg-blue-600 text-white shadow-xs' : 'text-slate-600 hover:text-slate-900'
              }`}
            >
              <span className="w-2 h-2 rounded-full bg-emerald-400"></span>
              Tầng Hầm B2 (Đang xem • 42 chỗ trống)
            </button>
            <button
              onClick={() => setSelectedFloor('B3')}
              className={`px-4 py-2 rounded-lg text-sm font-medium transition-colors ${
                selectedFloor === 'B3' ? 'bg-blue-600 text-white shadow-xs' : 'text-slate-600 hover:text-slate-900'
              }`}
            >
              Tầng Hầm B3 (38 chỗ trống)
            </button>
            <button
              onClick={() => setSelectedFloor('F1')}
              className={`px-4 py-2 rounded-lg text-sm font-medium transition-colors ${
                selectedFloor === 'F1' ? 'bg-blue-600 text-white shadow-xs' : 'text-slate-600 hover:text-slate-900'
              }`}
            >
              Tầng 1 (Khu VIP)
            </button>
          </div>

          <div className="flex items-center gap-4 px-3 py-1">
            <div className="flex items-center gap-1.5 text-xs text-slate-500">
              <span className="w-3 h-3 rounded-full bg-slate-300"></span>
              Đang trống
            </div>
            <div className="flex items-center gap-1.5 text-xs text-slate-500">
              <span className="w-3 h-3 rounded-full bg-blue-600"></span>
              Đang chọn
            </div>
            <div className="flex items-center gap-1.5 text-xs text-slate-500">
              <span className="w-3 h-3 rounded-full bg-emerald-600"></span>
              Trạm sạc EV
            </div>
            <div className="flex items-center gap-1.5 text-xs text-slate-500">
              <span className="w-3 h-3 rounded-full bg-slate-400"></span>
              Đã đỗ
            </div>
          </div>
        </div>

        {/* 5. Technical Blueprint / Parking Map Section */}
        <section className="rounded-2xl bg-white p-6 shadow-md border border-slate-100 mb-10 scroll-mt-36" id="map-section">
          <div className="flex flex-col sm:flex-row sm:items-center justify-between pb-4 gap-2 border-b border-slate-100">
            <div>
              <h2 className="text-xl font-bold text-slate-900 flex items-center gap-2">
                <span className="material-symbols-outlined text-blue-600">view_quilt</span>
                Sơ đồ Kỹ thuật Tầng Hầm B2 - Phân khu B (EV Priority)
              </h2>
              <p className="text-xs text-slate-500 mt-0.5">
                Nhấp trực tiếp vào bất kỳ vị trí ô đỗ hoặc danh sách bên dưới để tự động cuộn xuống đặt chỗ & thanh toán
              </p>
            </div>
            <div className="flex items-center gap-2">
              <span className="text-xs px-3 py-1.5 rounded-lg bg-emerald-50 text-emerald-700 font-bold flex items-center gap-1.5 border border-emerald-200">
                <span className="material-symbols-outlined text-[16px]">sensors</span> Cảm biến đỗ xe: Hoạt động tốt
              </span>
            </div>
          </div>

          {/* Blueprint Visual Canvas */}
          <div className="relative w-full rounded-2xl overflow-hidden shadow-sm bg-slate-900 min-h-[380px] flex items-center justify-center mt-5 select-none border border-slate-800">
            {/* SVG Background Floor Grid Blueprint */}
            <svg className="w-full h-80 opacity-60" viewBox="0 0 1000 400" fill="none" xmlns="http://www.w3.org/2000/svg">
              <defs>
                <pattern id="grid" width="40" height="40" patternUnits="userSpaceOnUse">
                  <path d="M 40 0 L 0 0 0 40" fill="none" stroke="#334155" strokeWidth="0.8" />
                </pattern>
              </defs>
              <rect width="1000" height="400" fill="url(#grid)" />
              {/* Lane Markings */}
              <rect x="50" y="40" width="900" height="320" rx="16" stroke="#475569" strokeWidth="2" strokeDasharray="8 8" />
              <line x1="50" y1="200" x2="950" y2="200" stroke="#3b82f6" strokeWidth="3" strokeDasharray="12 12" />
              {/* Parking slots visual boundaries */}
              <rect x="180" y="80" width="120" height="90" rx="8" fill="#1e293b" stroke="#64748b" strokeWidth="1.5" />
              <rect x="320" y="80" width="120" height="90" rx="8" fill="#1e293b" stroke="#3b82f6" strokeWidth="2" />
              <rect x="460" y="80" width="120" height="90" rx="8" fill="#1e293b" stroke="#10b981" strokeWidth="2" />
              <rect x="600" y="80" width="120" height="90" rx="8" fill="#1e293b" stroke="#64748b" strokeWidth="1.5" />

              <rect x="180" y="230" width="120" height="90" rx="8" fill="#1e293b" stroke="#475569" strokeWidth="1" />
              <rect x="320" y="230" width="120" height="90" rx="8" fill="#1e293b" stroke="#475569" strokeWidth="1" />
              <rect x="460" y="230" width="120" height="90" rx="8" fill="#1e293b" stroke="#475569" strokeWidth="1" />
              <rect x="600" y="230" width="120" height="90" rx="8" fill="#1e293b" stroke="#475569" strokeWidth="1" />

              <text x="500" y="215" fill="#94a3b8" fontSize="12" fontFamily="sans-serif" textAnchor="middle" letterSpacing="4">
                LÀN XE LƯU THÔNG KHU B (CHIỀU CAO TỐI ĐA 2.2M)
              </text>
            </svg>

            {/* Interactive Pins */}
            {AVAILABLE_SPOTS.map((spot) => {
              const isSelected = selectedSpotCode === spot.code;
              return (
                <button
                  key={spot.code}
                  style={{ top: spot.y, left: spot.x }}
                  onClick={() => handleSpotSelect(spot.code)}
                  className={`absolute -translate-x-1/2 -translate-y-1/2 flex flex-col items-center cursor-pointer group focus:outline-hidden transition-transform duration-200 z-20 ${
                    isSelected ? 'scale-110 z-30' : 'hover:scale-105'
                  }`}
                  title={`Nhấp để chọn ${spot.code}`}
                  type="button"
                >
                  <div
                    className={`px-3 py-1.5 rounded-lg text-xs font-bold shadow-lg flex items-center gap-1.5 transition-all ${
                      isSelected
                        ? 'bg-blue-600 text-white ring-4 ring-blue-400/40 shadow-blue-500/50'
                        : spot.isEv
                        ? 'bg-emerald-600 text-white ring-2 ring-emerald-300'
                        : 'bg-white text-slate-800 ring-1 ring-slate-300 hover:bg-blue-50'
                    }`}
                  >
                    <span className="material-symbols-outlined text-[15px]">
                      {isSelected ? 'local_parking' : spot.isEv ? 'electric_car' : 'check_circle'}
                    </span>
                    <span>{isSelected ? `Vị trí ${spot.code} (Đã chọn)` : `${spot.code} (${spot.typeText})`}</span>
                    {isSelected && <span className="material-symbols-outlined text-[14px]">arrow_forward</span>}
                  </div>
                  <div
                    className={`relative w-5 h-5 rounded-full flex items-center justify-center mt-1 ${
                      isSelected ? 'bg-blue-400/40 animate-pulse' : 'bg-white/30'
                    }`}
                  >
                    <div
                      className={`w-2.5 h-2.5 rounded-full ${
                        isSelected ? 'bg-blue-500' : spot.isEv ? 'bg-emerald-500' : 'bg-slate-300'
                      }`}
                    ></div>
                  </div>
                </button>
              );
            })}
          </div>

          {/* 6. Quick Selection Cards (Danh sách ô trống gợi ý) */}
          <div className="mt-6 flex flex-col gap-3">
            <div className="flex items-center justify-between">
              <span className="text-xs font-bold text-slate-500 uppercase tracking-wider">
                Danh sách ô trống gợi ý (Nhấp để chọn & cuộn xuống thanh toán)
              </span>
              <span className="text-xs text-blue-600 flex items-center gap-1 font-semibold">
                <span className="material-symbols-outlined text-[15px]">touch_app</span> Chạm vào ô để chọn
              </span>
            </div>

            <div className="grid grid-cols-1 sm:grid-cols-2 lg:grid-cols-4 gap-3.5">
              {AVAILABLE_SPOTS.map((spot) => {
                const isSelected = selectedSpotCode === spot.code;
                return (
                  <div
                    key={spot.code}
                    onClick={() => handleSpotSelect(spot.code)}
                    className={`p-4 rounded-xl cursor-pointer transition-all duration-200 group shadow-xs flex flex-col justify-between ${
                      isSelected
                        ? 'border-2 border-blue-600 bg-blue-50/60 ring-2 ring-blue-100'
                        : 'border border-slate-200 bg-white hover:border-blue-400 hover:bg-slate-50'
                    }`}
                  >
                    <div>
                      <div className="flex items-center justify-between mb-2">
                        <span
                          className={`px-2.5 py-0.5 rounded font-bold text-sm ${
                            isSelected ? 'bg-blue-600 text-white' : 'bg-slate-100 text-slate-800'
                          }`}
                        >
                          {spot.code}
                        </span>
                        {spot.isEv ? (
                          <span className="text-emerald-700 font-semibold text-xs flex items-center gap-1">
                            <span className="material-symbols-outlined text-[14px]">bolt</span> Sạc EV 120kW
                          </span>
                        ) : (
                          <span className="text-slate-500 font-semibold text-xs flex items-center gap-1">
                            <span className="material-symbols-outlined text-[14px]">check_circle</span> Trống
                          </span>
                        )}
                      </div>
                      <p className="text-xs text-slate-500">{spot.desc}</p>
                    </div>

                    <div
                      className={`mt-3 pt-2.5 border-t flex items-center justify-between text-xs font-bold ${
                        isSelected
                          ? 'border-blue-200 text-blue-600'
                          : 'border-slate-100 text-slate-500 group-hover:text-blue-600'
                      }`}
                    >
                      <span>{isSelected ? 'Vị trí đang chọn' : 'Chọn & Đặt chỗ'}</span>
                      <span className="material-symbols-outlined text-[16px] group-hover:translate-x-1 transition-transform">
                        south
                      </span>
                    </div>
                  </div>
                );
              })}
            </div>
          </div>

          {/* Selected Spot Summary Banner */}
          <div className="mt-5 p-4 sm:p-5 rounded-xl bg-blue-50/80 flex flex-col lg:flex-row lg:items-center justify-between gap-4 border border-blue-200">
            <div className="flex items-start sm:items-center gap-3.5">
              <div className="w-12 h-12 rounded-xl bg-blue-600 text-white flex items-center justify-center shrink-0 shadow-sm">
                <span className="material-symbols-outlined text-[26px]">local_parking</span>
              </div>
              <div>
                <div className="flex items-center gap-2">
                  <span className="text-base font-bold text-slate-900">
                    Vị trí {currentSpot.code} đã được chọn ({currentSpot.typeText})
                  </span>
                  <span className="px-2 py-0.5 rounded-full bg-emerald-100 text-emerald-800 text-[10px] font-bold">
                    Tự động giữ chỗ
                  </span>
                </div>
                <p className="text-xs text-slate-600 mt-0.5">{currentSpot.desc}</p>
              </div>
            </div>

            <a
              href="#booking-section"
              className="flex items-center gap-2 text-blue-600 text-xs font-bold bg-white px-4 py-2.5 rounded-xl border border-blue-200 shadow-xs shrink-0 hover:bg-blue-600 hover:text-white transition-all"
            >
              <span>Điền thông tin đặt chỗ</span>
              <span className="material-symbols-outlined text-[18px]">arrow_downward</span>
            </a>
          </div>
        </section>

        {/* 7. Booking Form & Checkout Section */}
        <section className="scroll-mt-24 flex flex-col gap-6" id="booking-section">
          <div className="flex flex-col sm:flex-row sm:items-center justify-between gap-3 p-4 rounded-2xl bg-white border border-slate-200 shadow-xs">
            <div className="flex items-center gap-3">
              <div className="w-10 h-10 rounded-xl bg-blue-50 text-blue-600 flex items-center justify-center font-bold">
                <span className="material-symbols-outlined text-[22px]">assignment_turned_in</span>
              </div>
              <div>
                <h2 className="text-lg font-bold text-slate-900">
                  Hoàn tất thông tin đặt chỗ & Thanh toán cho vị trí đã chọn
                </h2>
                <p className="text-xs text-slate-500">
                  Vị trí đang chọn: <strong className="text-blue-600 font-bold">{currentSpot.code} (Hầm {selectedFloor})</strong> • Hệ thống tự động mở barrier bằng biển số xe
                </p>
              </div>
            </div>

            <div className="inline-flex items-center gap-2 px-3.5 py-1.5 rounded-lg bg-slate-100 text-slate-800 text-xs self-start sm:self-auto font-medium">
              <span className="material-symbols-outlined text-[18px] text-emerald-600 animate-spin">history</span>
              <span>Giữ chỗ trong:</span>
              <span className="text-base font-extrabold text-blue-600">{formatCountdown(countdown)}</span>
            </div>
          </div>

          {/* 2-Column Responsive Layout */}
          <div className="grid grid-cols-1 lg:grid-cols-12 gap-8 items-start">
            {/* Left Column: Inputs & Add-on Services (8 Cols) */}
            <div className="lg:col-span-8 flex flex-col gap-6">
              {/* 7a. Biển số xe nhận diện ANPR */}
              <div className="rounded-2xl bg-white p-6 shadow-sm border border-slate-200">
                <div className="flex items-center justify-between mb-4">
                  <h3 className="text-base font-bold text-slate-900 flex items-center gap-2">
                    <span className="material-symbols-outlined text-blue-600 text-[20px]">directions_car</span>
                    Biển số xe nhận diện ANPR
                  </h3>
                  <button
                    onClick={() => navigate('/driver/vehicles')}
                    className="text-blue-600 text-xs font-bold hover:underline flex items-center gap-1"
                    type="button"
                  >
                    <span className="material-symbols-outlined text-[16px]">sync_alt</span> Đổi xe khác
                  </button>
                </div>

                <div className="p-4 rounded-xl bg-slate-50 flex flex-col sm:flex-row sm:items-center justify-between gap-4 border border-slate-200">
                  <div className="flex items-center gap-4">
                    <div className="w-14 h-14 rounded-xl bg-white border border-slate-200 flex items-center justify-center text-blue-600 font-bold text-lg shadow-xs">
                      {vehicleModel.split(' ')[0] || 'EQE'}
                    </div>
                    <div>
                      <div className="flex items-center gap-2.5">
                        <span className="text-xl font-extrabold text-slate-900 tracking-wide">
                          {vehiclePlate}
                        </span>
                        <span className="px-2.5 py-0.5 rounded-full bg-emerald-50 text-emerald-700 text-[10px] font-bold flex items-center gap-1 border border-emerald-200">
                          <span className="material-symbols-outlined text-[12px]">verified</span> Đã xác thực ANPR
                        </span>
                      </div>
                      <p className="text-xs text-slate-500 mt-0.5">
                        {vehicleBrand} {vehicleModel} • Màu {vehicleColor} • Nhiên liệu: {vehicleFuel}
                      </p>
                    </div>
                  </div>

                  <span className="px-3 py-1.5 rounded-lg bg-slate-200 text-slate-700 text-[10px] uppercase tracking-wider font-bold self-start sm:self-auto border border-slate-300">
                    Xe mặc định
                  </span>
                </div>
              </div>

              {/* 7b. Thời gian gửi & lấy xe dự kiến */}
              <div className="rounded-2xl bg-white p-6 shadow-sm border border-slate-200">
                <h3 className="text-base font-bold text-slate-900 flex items-center gap-2 mb-4">
                  <span className="material-symbols-outlined text-blue-600 text-[20px]">schedule</span>
                  Thời gian gửi & lấy xe dự kiến
                </h3>

                <div className="grid grid-cols-1 sm:grid-cols-2 gap-4 mb-4">
                  <div className="p-4 rounded-xl bg-slate-50 border border-slate-200 flex flex-col gap-1.5">
                    <label className="text-[11px] text-slate-500 font-bold uppercase tracking-wider">
                      Giờ vào dự kiến
                    </label>
                    <div className="flex items-center justify-between">
                      <span className="text-base font-bold text-slate-900">Hôm nay, 14:00</span>
                      <span className="material-symbols-outlined text-blue-600 text-[20px]">calendar_today</span>
                    </div>
                    <span className="text-xs text-emerald-700 flex items-center gap-1 font-medium">
                      <span className="material-symbols-outlined text-[14px]">sensors</span> Check-in tự động barrier
                    </span>
                  </div>

                  <div className="p-4 rounded-xl bg-slate-50 border border-slate-200 flex flex-col gap-1.5">
                    <label className="text-[11px] text-slate-500 font-bold uppercase tracking-wider">
                      Giờ ra dự kiến
                    </label>
                    <div className="flex items-center justify-between">
                      <span className="text-base font-bold text-slate-900">Hôm nay, 18:00</span>
                      <span className="material-symbols-outlined text-blue-600 text-[20px]">calendar_today</span>
                    </div>
                    <span className="text-xs text-slate-500">Tổng thời lượng: 4 giờ 00 phút (Tính 4 giờ dịch vụ)</span>
                  </div>
                </div>

                <div className="flex items-center gap-2 px-3.5 py-2.5 rounded-lg bg-blue-50/60 text-slate-700 text-xs border border-blue-100">
                  <span className="material-symbols-outlined text-[18px] text-blue-600">info</span>
                  <span>Hệ thống tự động bảo lưu vị trí thêm 15 phút sau giờ vào dự kiến mà không thu thêm phí phụ thu.</span>
                </div>
              </div>

              {/* 7c. Dịch vụ tiện ích cộng thêm */}
              <div className="rounded-2xl bg-white p-6 shadow-sm border border-slate-200">
                <h3 className="text-base font-bold text-slate-900 flex items-center gap-2 mb-4">
                  <span className="material-symbols-outlined text-blue-600 text-[20px]">tune</span>
                  Dịch vụ tiện ích cộng thêm
                </h3>

                <div className="flex flex-col gap-3">
                  {/* Service 1: EV Fast-charge */}
                  <label className="flex items-center justify-between p-4 rounded-xl bg-slate-50 hover:bg-slate-100 cursor-pointer transition-colors border border-slate-200">
                    <div className="flex items-center gap-3.5">
                      <input
                        type="checkbox"
                        checked={hasEvService}
                        onChange={(e) => setHasEvService(e.target.checked)}
                        className="w-5 h-5 rounded text-blue-600 focus:ring-blue-500 border-slate-300 cursor-pointer"
                      />
                      <div>
                        <div className="flex items-center gap-2">
                          <span className="text-sm font-bold text-slate-900">
                            Sạc pin xe điện Fast-charge DC 120kW (Trụ {currentSpot.code})
                          </span>
                          <span className="px-2 py-0.5 rounded bg-emerald-100 text-emerald-800 text-[10px] font-bold">
                            Tự ngắt an toàn
                          </span>
                        </div>
                        <p className="text-xs text-slate-500">Công suất tối đa 120kW, sạc từ 20% lên 80% chỉ trong 35 phút</p>
                      </div>
                    </div>
                    <span className="text-sm font-bold text-blue-600 whitespace-nowrap">+50.000 đ</span>
                  </label>

                  {/* Service 2: Car wash */}
                  <label className="flex items-center justify-between p-4 rounded-xl bg-slate-50 hover:bg-slate-100 cursor-pointer transition-colors border border-slate-200">
                    <div className="flex items-center gap-3.5">
                      <input
                        type="checkbox"
                        checked={hasWashService}
                        onChange={(e) => setHasWashService(e.target.checked)}
                        className="w-5 h-5 rounded text-blue-600 focus:ring-blue-500 border-slate-300 cursor-pointer"
                      />
                      <div>
                        <span className="text-sm font-bold text-slate-900">Rửa xe bọt tuyết & Hút bụi nội thất VIP</span>
                        <p className="text-xs text-slate-500">Kỹ thuật viên phục vụ tận ô đỗ xe {currentSpot.code} trước giờ hẹn</p>
                      </div>
                    </div>
                    <span className="text-sm font-bold text-slate-900 whitespace-nowrap">+80.000 đ</span>
                  </label>

                  {/* Service 3: Insurance */}
                  <label className="flex items-center justify-between p-4 rounded-xl bg-slate-50 border border-slate-200">
                    <div className="flex items-center gap-3.5">
                      <input
                        type="checkbox"
                        checked={hasInsurance}
                        readOnly
                        className="w-5 h-5 rounded text-blue-600 focus:ring-blue-500 border-slate-300 cursor-pointer"
                      />
                      <div>
                        <span className="text-sm font-bold text-slate-900">
                          Bảo hiểm trầy xước & rủi ro bãi đỗ ParkMaster Care
                        </span>
                        <p className="text-xs text-slate-500">Đền bù 100% rủi ro trầy xước với hệ thống AI giám sát 360° liên tục</p>
                      </div>
                    </div>
                    <span className="text-sm font-bold text-emerald-700 whitespace-nowrap">0 đ (VIP Miễn phí)</span>
                  </label>
                </div>
              </div>

              {/* 7d. Ghi chú điều phối */}
              <div className="rounded-2xl bg-white p-6 shadow-sm border border-slate-200">
                <label className="text-base font-bold text-slate-900 flex items-center gap-2 mb-3">
                  <span className="material-symbols-outlined text-blue-600 text-[20px]">sticky_note_2</span>
                  Ghi chú cho bộ phận điều phối (Tùy chọn)
                </label>
                <textarea
                  value={bookingNote}
                  onChange={(e) => setBookingNote(e.target.value)}
                  placeholder="Ví dụ: Xe gầm thấp, vui lòng hỗ trợ góc đánh lái rộng khi vào vị trí..."
                  rows={3}
                  className="w-full px-4 py-3 rounded-xl bg-slate-50 text-slate-900 placeholder:text-slate-400 border border-slate-200 focus:outline-hidden focus:border-blue-600 focus:bg-white transition-colors text-sm resize-none"
                />
              </div>
            </div>

            {/* Right Column: Sticky Summary & Checkout Card (4 Cols) */}
            <div className="lg:col-span-4 sticky top-24 flex flex-col gap-6">
              <div className="rounded-2xl bg-white p-6 shadow-xl border border-slate-200 flex flex-col gap-5">
                <div className="flex items-center justify-between">
                  <h3 className="text-lg font-extrabold text-slate-900">Tóm tắt chi phí đặt chỗ</h3>
                  <span className="px-2.5 py-1 rounded bg-emerald-50 text-emerald-700 text-[10px] font-bold uppercase tracking-wider flex items-center gap-1 border border-emerald-200">
                    <span className="material-symbols-outlined text-[13px]">shield</span> Giữ chỗ 100%
                  </span>
                </div>

                {/* Spot snapshot */}
                <div className="p-3.5 rounded-xl bg-slate-50 flex items-center justify-between border border-slate-200">
                  <div className="flex items-center gap-3">
                    <div className="w-10 h-10 rounded-lg bg-blue-600 text-white flex items-center justify-center font-bold text-sm shadow-xs">
                      {currentSpot.code}
                    </div>
                    <div>
                      <span className="text-xs font-bold text-slate-900">
                        Vị trí {currentSpot.code} • Hầm {selectedFloor}
                      </span>
                      <p className="text-[11px] text-slate-500">{lot?.name || 'Landmark 81 Garage'}</p>
                    </div>
                  </div>
                  <span className="px-2 py-0.5 rounded bg-emerald-100 text-emerald-800 text-[10px] font-bold">
                    Khả dụng
                  </span>
                </div>

                {/* Line item breakdown */}
                <div className="flex flex-col gap-2.5 text-xs pt-2">
                  <div className="flex items-center justify-between text-slate-500">
                    <span>Đơn giá gửi</span>
                    <span className="text-slate-900 font-semibold">{hourlyRate.toLocaleString('vi-VN')} đ/h</span>
                  </div>
                  <div className="flex items-center justify-between text-slate-500">
                    <span>Tạm tính 4 giờ gửi xe</span>
                    <span className="text-slate-900 font-semibold">{parkingFee.toLocaleString('vi-VN')} đ</span>
                  </div>
                  <div className="flex items-center justify-between text-slate-500">
                    <span>Phí công nghệ ANPR & IoT</span>
                    <span className="text-slate-900 font-semibold">{techFee.toLocaleString('vi-VN')} đ</span>
                  </div>
                  <div className="flex items-center justify-between text-emerald-700 font-medium">
                    <span className="flex items-center gap-1">
                      <span className="material-symbols-outlined text-[15px]">stars</span>
                      Chiết khấu VIP (-20%)
                    </span>
                    <span className="font-bold">-{vipDiscount.toLocaleString('vi-VN')} đ</span>
                  </div>
                  {hasEvService && (
                    <div className="flex items-center justify-between text-slate-500">
                      <span>Dịch vụ sạc xe EV Fast (120kW)</span>
                      <span className="text-slate-900 font-semibold">+{evFee.toLocaleString('vi-VN')} đ</span>
                    </div>
                  )}
                  {hasWashService && (
                    <div className="flex items-center justify-between text-slate-500">
                      <span>Rửa xe bọt tuyết & Hút bụi</span>
                      <span className="text-slate-900 font-semibold">+{washFee.toLocaleString('vi-VN')} đ</span>
                    </div>
                  )}
                </div>

                <div className="h-px w-full bg-slate-200 my-1"></div>

                {/* Total */}
                <div className="flex items-baseline justify-between">
                  <span className="text-sm font-bold text-slate-900">TỔNG THANH TOÁN:</span>
                  <span className="text-2xl font-extrabold text-blue-600 tracking-tight">
                    {totalAmount.toLocaleString('vi-VN')} đ
                  </span>
                </div>

                {/* Payment Methods */}
                <div className="flex flex-col gap-2 pt-1">
                  <span className="text-[11px] uppercase font-bold text-slate-400">Phương thức thanh toán</span>
                  
                  <label
                    onClick={() => setPaymentMethod('wallet')}
                    className={`flex items-center justify-between p-3 rounded-xl cursor-pointer transition-colors border ${
                      paymentMethod === 'wallet' ? 'bg-blue-50 border-blue-600 ring-1 ring-blue-600' : 'bg-slate-50 border-slate-200'
                    }`}
                  >
                    <div className="flex items-center gap-3">
                      <input
                        type="radio"
                        name="paymethod"
                        checked={paymentMethod === 'wallet'}
                        onChange={() => setPaymentMethod('wallet')}
                        className="w-4 h-4 text-blue-600 focus:ring-blue-500"
                      />
                      <span className="text-xs font-bold text-slate-900">Ví ParkMaster Pay</span>
                    </div>
                    <span className="text-[11px] text-emerald-700 font-bold">Số dư: 2.450.000đ</span>
                  </label>

                  <label
                    onClick={() => setPaymentMethod('momo')}
                    className={`flex items-center justify-between p-3 rounded-xl cursor-pointer transition-colors border ${
                      paymentMethod === 'momo' ? 'bg-blue-50 border-blue-600 ring-1 ring-blue-600' : 'bg-slate-50 border-slate-200'
                    }`}
                  >
                    <div className="flex items-center gap-3">
                      <input
                        type="radio"
                        name="paymethod"
                        checked={paymentMethod === 'momo'}
                        onChange={() => setPaymentMethod('momo')}
                        className="w-4 h-4 text-blue-600 focus:ring-blue-500"
                      />
                      <span className="text-xs font-medium text-slate-900">MoMo / VNPay QR</span>
                    </div>
                    <span className="material-symbols-outlined text-slate-500 text-[18px]">qr_code_2</span>
                  </label>

                  <label
                    onClick={() => setPaymentMethod('card')}
                    className={`flex items-center justify-between p-3 rounded-xl cursor-pointer transition-colors border ${
                      paymentMethod === 'card' ? 'bg-blue-50 border-blue-600 ring-1 ring-blue-600' : 'bg-slate-50 border-slate-200'
                    }`}
                  >
                    <div className="flex items-center gap-3">
                      <input
                        type="radio"
                        name="paymethod"
                        checked={paymentMethod === 'card'}
                        onChange={() => setPaymentMethod('card')}
                        className="w-4 h-4 text-blue-600 focus:ring-blue-500"
                      />
                      <span className="text-xs font-medium text-slate-900">Thẻ Visa / MasterCard</span>
                    </div>
                    <span className="material-symbols-outlined text-slate-500 text-[18px]">credit_card</span>
                  </label>

                  <label
                    onClick={() => setPaymentMethod('etc')}
                    className={`flex items-center justify-between p-3 rounded-xl cursor-pointer transition-colors border ${
                      paymentMethod === 'etc' ? 'bg-blue-50 border-blue-600 ring-1 ring-blue-600' : 'bg-slate-50 border-slate-200'
                    }`}
                  >
                    <div className="flex items-center gap-3">
                      <input
                        type="radio"
                        name="paymethod"
                        checked={paymentMethod === 'etc'}
                        onChange={() => setPaymentMethod('etc')}
                        className="w-4 h-4 text-blue-600 focus:ring-blue-500"
                      />
                      <span className="text-xs font-medium text-slate-900">Ví VETC / ePass ETC</span>
                    </div>
                    <span className="material-symbols-outlined text-slate-500 text-[18px]">toll</span>
                  </label>
                </div>

                {/* CTA Button (AC3: Disable khi bãi không khả dụng) */}
                <button
                  type="button"
                  disabled={lot?.canBook === false}
                  onClick={handleConfirmBooking}
                  className={`w-full py-4 rounded-xl font-bold text-sm transition-all shadow-lg flex items-center justify-center gap-2 mt-2 ${
                    lot?.canBook === false
                      ? 'bg-slate-300 text-slate-500 cursor-not-allowed shadow-none'
                      : 'bg-blue-600 text-white hover:bg-blue-700 shadow-blue-500/20 cursor-pointer'
                  }`}
                >
                  <span className="material-symbols-outlined text-[20px]">{lot?.canBook === false ? 'block' : 'lock'}</span>
                  <span>{lot?.canBook === false ? 'Tạm ngừng nhận đặt chỗ' : `Xác nhận & Thanh toán (${totalAmount.toLocaleString('vi-VN')}đ)`}</span>
                </button>

                <div className="flex items-center justify-center gap-2 text-center text-slate-500 text-[11px]">
                  <span className="material-symbols-outlined text-[15px] text-emerald-600">verified_user</span>
                  <span>Miễn phí hủy trước 30 phút • Hoàn tiền 100% trong 30 giây</span>
                </div>
              </div>
            </div>
          </div>
        </section>
      </div>

      {/* Booking Success Modal */}
      {showSuccessModal && (
        <div className="fixed inset-0 bg-slate-900/60 backdrop-blur-xs z-50 flex items-center justify-center p-4 animate-in fade-in">
          <div className="bg-white rounded-2xl shadow-2xl p-6 sm:p-7 w-full max-w-md border border-slate-200 flex flex-col items-center text-center">
            <div className="w-16 h-16 rounded-2xl bg-emerald-100 text-emerald-600 flex items-center justify-center mb-4">
              <span className="material-symbols-outlined text-[36px]">check_circle</span>
            </div>
            <h3 className="text-xl font-bold text-slate-900">Đặt chỗ thành công!</h3>
            <p className="text-xs text-slate-500 mt-1 mb-5">
              Hệ thống ANPR đã kích hoạt nhận diện cho biển số xe <strong className="text-slate-800">{vehiclePlate}</strong>
            </p>

            {/* Ticket Card Preview */}
            <div className="w-full p-4 rounded-xl bg-slate-50 border border-slate-200 text-left flex flex-col gap-2.5 mb-5 text-xs">
              <div className="flex justify-between border-b pb-2">
                <span className="text-slate-500">Mã vé:</span>
                <span className="font-bold text-blue-600">#PKM-2025-8842</span>
              </div>
              <div className="flex justify-between">
                <span className="text-slate-500">Bãi đỗ:</span>
                <span className="font-semibold text-slate-800">{lot?.name || 'Landmark 81 Garage'}</span>
              </div>
              <div className="flex justify-between">
                <span className="text-slate-500">Vị trí đỗ:</span>
                <span className="font-bold text-emerald-600">Ô {currentSpot.code} (Hầm {selectedFloor})</span>
              </div>
              <div className="flex justify-between">
                <span className="text-slate-500">Thời gian:</span>
                <span className="font-semibold text-slate-800">Hôm nay, 14:00 - 18:00</span>
              </div>
              <div className="flex justify-between border-t pt-2">
                <span className="text-slate-500">Tổng thanh toán:</span>
                <span className="font-extrabold text-blue-600">{totalAmount.toLocaleString('vi-VN')} đ</span>
              </div>
            </div>

            <div className="flex gap-3 w-full">
              <button
                type="button"
                onClick={() => navigate('/driver/history')}
                className="flex-1 py-3 rounded-xl bg-blue-600 text-white font-bold text-xs hover:bg-blue-700 transition-colors shadow-sm"
              >
                Xem vé trong Lịch sử
              </button>
              <button
                type="button"
                onClick={() => setShowSuccessModal(false)}
                className="px-5 py-3 rounded-xl bg-slate-100 text-slate-700 font-bold text-xs hover:bg-slate-200 transition-colors"
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
