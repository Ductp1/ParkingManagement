import React, { useState, useEffect, useCallback } from 'react';
import { useNavigate } from 'react-router-dom';
import { useAppDispatch, useAppSelector } from '@/store';
import { searchParkingLots } from '@/store/slices/parkingSlice';

export const DriverHomePage: React.FC = () => {
  const navigate = useNavigate();
  const dispatch = useAppDispatch();
  const { searchResults, isLoading } = useAppSelector((state) => state.parking);
  const { vehicles } = useAppSelector((state) => state.vehicle);

  const activeVehicle = vehicles.find((v) => v.isDefault) || vehicles[0];
  const vehicleHeight = activeVehicle?.heightCm || 145; // Mặc định Sedan 145 cm

  const [selectedLotPopup, setSelectedLotPopup] = useState(true);
  const [locationInput, setLocationInput] = useState('Vị trí hiện tại (GPS)');
  const [radius, setRadius] = useState('5');
  const [coords, setCoords] = useState<{ lat: number; lng: number }>({ lat: 10.7769, lng: 106.7009 });
  const [isLocating, setIsLocating] = useState(false);
  const [isHeightFilterEnabled, setIsHeightFilterEnabled] = useState(true);

  const performSearch = useCallback(
    (lat: number, lng: number, radKm: number, heightCm?: number) => {
      dispatch(
        searchParkingLots({
          lat,
          lng,
          radiusKm: radKm,
          vehicleHeightCm: heightCm,
          limit: 20,
        })
      );
    },
    [dispatch]
  );

  const handleGetCurrentLocation = useCallback(() => {
    if (!navigator.geolocation) {
      performSearch(coords.lat, coords.lng, Number(radius), isHeightFilterEnabled ? vehicleHeight : undefined);
      return;
    }

    setIsLocating(true);
    navigator.geolocation.getCurrentPosition(
      (pos) => {
        const nextCoords = { lat: pos.coords.latitude, lng: pos.coords.longitude };
        setCoords(nextCoords);
        setLocationInput('Vị trí GPS của bạn');
        setIsLocating(false);
        performSearch(nextCoords.lat, nextCoords.lng, Number(radius), isHeightFilterEnabled ? vehicleHeight : undefined);
      },
      (err) => {
        console.warn('Lỗi định vị GPS hoặc bị từ chối:', err.message);
        setIsLocating(false);
        setLocationInput('Trung tâm TP.HCM (Mặc định)');
        performSearch(coords.lat, coords.lng, Number(radius), isHeightFilterEnabled ? vehicleHeight : undefined);
      },
      { timeout: 8000, enableHighAccuracy: true }
    );
  }, [coords.lat, coords.lng, radius, performSearch]);

  useEffect(() => {
    handleGetCurrentLocation();
  }, [handleGetCurrentLocation]);

  const handleSearchSubmit = (e: React.FormEvent) => {
    e.preventDefault();
    performSearch(coords.lat, coords.lng, Number(radius), isHeightFilterEnabled ? vehicleHeight : undefined);
  };



  return (
    <div className="relative w-full overflow-hidden bg-slate-50 min-h-screen flex flex-col">
      {/* Dynamic Fixed Background Map Graphic */}
      <div className="absolute inset-0 pointer-events-none z-0 overflow-hidden h-[540px]">
        <div className="absolute inset-0 bg-[#e8ecf2]">
          <svg className="w-full h-full object-cover" preserveAspectRatio="xMidYMid slice" viewBox="0 0 1440 900" xmlns="http://www.w3.org/2000/svg">
            <rect fill="#f1f5f8" height="900" width="1440"></rect>
            <path d="M-40,0 L420,0 L360,340 L-40,240 Z" fill="#e5edf5" opacity="0.8"></path>
            <path d="M580,0 L1120,0 L1040,280 L520,220 Z" fill="#e8f3ec" opacity="0.9"></path>
            <rect fill="#e4f3e9" height="120" opacity="0.9" rx="20" width="380" x="580" y="110"></rect>
            <circle cx="700" cy="200" fill="#d9ede0" opacity="0.75" r="140"></circle>
            <rect fill="#d2ebd9" height="90" opacity="0.8" rx="16" width="200" x="570" y="90"></rect>
            <rect fill="#dbeafe" height="180" opacity="0.65" rx="20" width="240" x="380" y="140"></rect>
            <rect fill="#e0f2fe" height="200" opacity="0.45" rx="24" width="280" x="420" y="460"></rect>
            <line stroke="#ffffff" strokeLinecap="round" strokeWidth="36" x1="-80" x2="1600" y1="310" y2="160"></line>
            <line stroke="#e2e8f0" strokeDasharray="14 14" strokeWidth="3" x1="-80" x2="1600" y1="310" y2="160"></line>
            <line stroke="#ffffff" strokeWidth="26" x1="260" x2="380" y1="-40" y2="980"></line>
            <line opacity="0.85" stroke="#fde68a" strokeLinecap="round" strokeWidth="20" x1="940" x2="880" y1="-40" y2="980"></line>
            <path d="M120,960 C 380,560 660,620 1020,340 S 1420,210 1580,120" fill="none" stroke="#ffffff" strokeWidth="40"></path>
            <path d="M120,960 C 380,560 660,620 1020,340 S 1420,210 1580,120" fill="none" opacity="0.75" stroke="#fef08a" strokeWidth="14"></path>
            <path d="M-40,540 C 280,480 540,610 860,540 S 1320,640 1560,560" fill="none" opacity="0.5" stroke="#c7dcf7" strokeWidth="44"></path>
          </svg>
        </div>
      </div>

      {/* Hero Interactive Map & Search Stage */}
      <section className="relative z-20 w-full select-none" style={{ minHeight: '480px' }}>
        {/* Top Floating Search Bar and Filter Chips */}
        <div className="relative z-30 max-w-7xl mx-auto px-4 sm:px-6 pt-5">
          <div className="max-w-4xl mx-auto">
            <form
              onSubmit={handleSearchSubmit}
              className="bg-white/95 rounded-2xl p-2 shadow-xl shadow-blue-900/10 border-2 border-blue-200/80 flex flex-wrap lg:flex-nowrap items-center gap-2"
            >
              {/* Field 1: Location */}
              <div className="flex-1 min-w-[240px] flex items-center gap-3 px-3.5 py-1.5 hover:bg-slate-50 rounded-xl transition-colors">
                <i className="fa-solid fa-magnifying-glass text-slate-400 text-sm"></i>
                <div className="flex flex-col flex-1">
                  <label className="text-[9px] uppercase font-bold text-slate-400 tracking-wider">Địa điểm</label>
                  <input
                    className="p-0 bg-transparent border-0 text-slate-800 text-xs font-semibold focus:outline-hidden placeholder-slate-400 leading-tight"
                    value={locationInput}
                    onChange={(e) => setLocationInput(e.target.value)}
                    placeholder="Nhập tên bãi hoặc khu vực..."
                  />
                </div>
                <button
                  type="button"
                  onClick={handleGetCurrentLocation}
                  disabled={isLocating}
                  title="Lấy vị trí GPS hiện tại"
                  className="p-1.5 hover:bg-blue-100 rounded-lg text-blue-600 transition-colors cursor-pointer shrink-0"
                >
                  <i className={`fa-solid fa-crosshairs text-xs ${isLocating ? 'animate-spin text-blue-800' : ''}`}></i>
                </button>
              </div>
              <div className="hidden lg:block w-px h-8 bg-slate-200"></div>

              {/* Field 2: Date & Time */}
              <div className="flex-1 min-w-[200px] flex items-center gap-3 px-3.5 py-1.5 hover:bg-slate-50 rounded-xl transition-colors">
                <i className="fa-regular fa-clock text-slate-400 text-sm"></i>
                <div className="flex flex-col flex-1">
                  <label className="text-[9px] uppercase font-bold text-slate-400 tracking-wider">Thời gian</label>
                  <span className="text-slate-800 text-xs font-semibold leading-tight">Hôm nay, 08:30 - 12:30</span>
                </div>
              </div>
              <div className="hidden lg:block w-px h-8 bg-slate-200"></div>

              {/* Field 3: Radius */}
              <div className="w-full sm:w-[130px] flex items-center gap-2 px-3 py-1.5 hover:bg-slate-50 rounded-xl transition-colors">
                <i className="fa-solid fa-location-crosshairs text-slate-400 text-sm"></i>
                <div className="flex flex-col flex-1">
                  <label className="text-[9px] uppercase font-bold text-slate-400 tracking-wider">Bán kính</label>
                  <select
                    value={radius}
                    onChange={(e) => {
                      const nextRad = e.target.value;
                      setRadius(nextRad);
                      performSearch(coords.lat, coords.lng, Number(nextRad));
                    }}
                    className="p-0 bg-transparent border-none text-slate-800 text-xs font-semibold focus:outline-hidden cursor-pointer"
                  >
                    <option value="1">1 km</option>
                    <option value="2">2 km</option>
                    <option value="3">3 km</option>
                    <option value="5">5 km</option>
                    <option value="10">10 km</option>
                    <option value="15">15 km</option>
                  </select>
                </div>
              </div>

              {/* Search Button */}
              <button
                type="submit"
                className="w-full lg:w-auto px-6 py-2.5 rounded-xl bg-blue-600 hover:bg-blue-700 active:scale-[0.98] text-white font-bold text-xs flex items-center justify-center gap-2 shadow-md shadow-blue-600/25 transition-all cursor-pointer"
              >
                <i className="fa-solid fa-magnifying-glass text-xs"></i>
                <span>Tìm bãi đỗ</span>
              </button>
            </form>

            {/* Quick Filter Chips */}
            <div className="flex flex-wrap items-center justify-center gap-2 mt-3 text-xs">
              <button
                type="button"
                onClick={() => {
                  const nextState = !isHeightFilterEnabled;
                  setIsHeightFilterEnabled(nextState);
                  performSearch(coords.lat, coords.lng, Number(radius), nextState ? vehicleHeight : undefined);
                }}
                className={`inline-flex items-center gap-1.5 px-3 py-1 rounded-full text-[11px] font-semibold border transition-all cursor-pointer shadow-xs ${
                  isHeightFilterEnabled
                    ? 'bg-blue-600 text-white border-blue-600'
                    : 'bg-white text-slate-600 border-slate-300 hover:bg-slate-50'
                }`}
                title="Quy tắc an toàn TC-SEARCH-02: Ẩn bãi có trần < Chiều cao xe + 10cm"
              >
                <i className="fa-solid fa-shield-halved text-[10px]"></i>
                <span>
                  {isHeightFilterEnabled
                    ? `🛡️ Chống cạ trần (${vehicleHeight} cm + 10cm an toàn)`
                    : 'Chống cạ trần (Đang tắt)'}
                </span>
              </button>
              <button
                type="button"
                className="inline-flex items-center gap-1.5 px-3 py-1 rounded-full bg-white text-slate-800 border border-amber-300 shadow-xs hover:bg-amber-50 transition-all font-semibold text-[11px]"
              >
                <span className="text-amber-500 font-black text-xs">⚡</span> Có sạc EV
              </button>
              <button
                type="button"
                className="inline-flex items-center gap-1.5 px-3 py-1 rounded-full bg-white text-slate-800 border border-blue-200 shadow-xs hover:bg-blue-50 transition-all font-semibold text-[11px]"
              >
                <i className="fa-solid fa-warehouse text-blue-600 text-[10px]"></i> Hầm có mái
              </button>
              <button
                type="button"
                className="inline-flex items-center gap-1.5 px-3 py-1 rounded-full bg-white text-slate-800 border border-emerald-200 shadow-xs hover:bg-emerald-50 transition-all font-semibold text-[11px]"
              >
                <i className="fa-solid fa-video text-emerald-600 text-[10px]"></i> Camera 24/7
              </button>
            </div>
          </div>
        </div>

        {/* Map Interactive Pins Container */}
        <div className="max-w-7xl mx-auto relative h-[360px] px-6">
          {/* Landmark 81 Pin */}
          <div
            onClick={() => setSelectedLotPopup(true)}
            className="absolute left-[54%] top-[24%] z-20 cursor-pointer group"
          >
            <div className="flex items-center gap-1.5 px-2.5 py-1 rounded-full bg-white/95 border border-slate-200 shadow-md text-slate-800 text-[11px] font-semibold group-hover:scale-105 transition-transform">
              <span className="w-2 h-2 rounded-full bg-emerald-500 animate-pulse"></span>
              <span>Landmark 81 <span className="text-slate-400 font-normal">30K</span></span>
            </div>
          </div>

          {/* Central Park Pin */}
          <div className="absolute left-[62%] top-[34%] z-20">
            <div className="flex items-center gap-1.5 px-2.5 py-1 rounded-full bg-white/95 border border-slate-200 shadow-md text-slate-800 text-[11px] font-semibold">
              <span className="w-2 h-2 rounded-full bg-amber-500"></span>
              <span>Central Park <span className="text-slate-400 font-normal">25K</span></span>
            </div>
          </div>

          {/* Bitexco Pin */}
          <div className="absolute left-[38%] top-[54%] z-20">
            <div className="flex items-center gap-1.5 px-2.5 py-1 rounded-full bg-white/95 border border-slate-200 shadow-md text-slate-800 text-[11px] font-semibold">
              <span className="w-2 h-2 rounded-full bg-emerald-500"></span>
              <span>Bitexco <span className="text-slate-400 font-normal">35K</span></span>
            </div>
          </div>

          {/* Saigon Centre Pin */}
          <div className="absolute left-[29%] top-[48%] z-20">
            <div className="flex items-center gap-1.5 px-2.5 py-1 rounded-full bg-white/95 border border-slate-200 shadow-md text-slate-800 text-[11px] font-semibold">
              <span className="w-2 h-2 rounded-full bg-rose-500"></span>
              <span>Saigon Centre <span className="text-slate-400 font-normal">40K</span></span>
            </div>
          </div>

          {/* User Location Car Marker */}
          <div className="absolute left-[45%] top-[42%] z-20">
            <div className="w-8 h-8 rounded-full bg-blue-600 border-2 border-white shadow-md flex items-center justify-center text-white text-xs">
              <i className="fa-solid fa-car"></i>
            </div>
          </div>

          {/* Detailed Card Popup over Map (Left Side) */}
          {selectedLotPopup && (
            <div className="absolute left-6 sm:left-10 bottom-8 z-30 bg-white/95 backdrop-blur-sm rounded-xl p-3 border border-slate-200 shadow-lg w-[260px] animate-in fade-in">
              <div className="flex items-center justify-between text-[11px] font-bold text-emerald-600 mb-2">
                <span className="flex items-center gap-1.5">
                  <span className="w-2 h-2 rounded-full bg-emerald-500"></span>
                  42 chỗ trống
                </span>
                <button
                  onClick={() => setSelectedLotPopup(false)}
                  className="text-slate-400 hover:text-slate-600"
                  type="button"
                >
                  <i className="fa-solid fa-xmark text-xs"></i>
                </button>
              </div>
              <div className="flex items-start gap-2.5 mb-2.5">
                <div className="w-12 h-12 rounded-lg bg-slate-900 overflow-hidden shrink-0">
                  <img
                    alt="Landmark 81"
                    className="w-full h-full object-cover"
                    src="https://images.unsplash.com/photo-1573368723220-2a4c1483c66b?w=800&q=80"
                  />
                </div>
                <div className="overflow-hidden leading-tight">
                  <h4 className="font-bold text-slate-900 text-xs truncate">Landmark 81</h4>
                  <p className="text-slate-400 text-[10px] truncate mt-0.5">208 Nguyễn Hữu Cảnh</p>
                  <div className="text-xs font-bold text-slate-900 mt-1">
                    30K <span className="text-[10px] text-slate-400 font-normal">/giờ</span>
                  </div>
                </div>
              </div>
              <div className="flex items-center gap-1.5">
                <button
                  onClick={() => navigate('/driver/parking-lots')}
                  className="flex-1 py-1.5 px-2 rounded-lg border border-slate-200 text-slate-700 text-[10px] font-semibold hover:bg-slate-50 transition-colors"
                  type="button"
                >
                  Dẫn đường
                </button>
                <button
                  onClick={() => navigate('/driver/parking-lots')}
                  className="flex-1 py-1.5 px-2 rounded-lg bg-blue-600 hover:bg-blue-700 text-white text-[10px] font-semibold transition-colors shadow-xs"
                  type="button"
                >
                  Xem sơ đồ
                </button>
              </div>
            </div>
          )}

          {/* Map Controls (Right Side) */}
          <div className="absolute right-6 bottom-8 z-30 flex flex-col gap-1.5">
            <button
              className="w-8 h-8 rounded-lg bg-white border border-slate-200 text-slate-600 hover:bg-slate-50 shadow-sm flex items-center justify-center text-xs transition-colors"
              type="button"
            >
              <i className="fa-solid fa-location-arrow text-[11px]"></i>
            </button>
            <div className="bg-white rounded-lg border border-slate-200 shadow-sm overflow-hidden flex flex-col">
              <button className="w-8 h-8 hover:bg-slate-50 text-slate-600 flex items-center justify-center text-sm font-bold border-b border-slate-100" type="button">+</button>
              <button className="w-8 h-8 hover:bg-slate-50 text-slate-600 flex items-center justify-center text-sm font-bold" type="button">-</button>
            </div>
          </div>

          {/* Map Legend Bar */}
          <div className="absolute left-1/2 bottom-3 -translate-x-1/2 z-30 bg-white/90 backdrop-blur-sm px-3.5 py-1 rounded-full border border-slate-200 shadow-xs flex items-center gap-3 text-[10px] font-medium text-slate-600">
            <div className="flex items-center gap-1.5">
              <span className="w-2 h-2 rounded-full bg-emerald-500"></span>
              <span>Còn chỗ (&gt;20)</span>
            </div>
            <div className="flex items-center gap-1.5">
              <span className="w-2 h-2 rounded-full bg-amber-500"></span>
              <span>Sắp đầy (&lt;20)</span>
            </div>
            <div className="flex items-center gap-1.5">
              <span className="w-2 h-2 rounded-full bg-rose-500"></span>
              <span>Đã kín</span>
            </div>
          </div>
        </div>
      </section>

      {/* Realtime Status Bar Strip */}
      <section className="w-full max-w-7xl mx-auto px-4 sm:px-6 -mt-3 relative z-30 mb-6">
        <div className="w-full bg-white/95 border-2 border-blue-100 rounded-2xl p-4 shadow-lg shadow-blue-900/5 text-slate-800 flex flex-col md:flex-row md:items-center justify-between gap-4 backdrop-blur-md">
          <div className="flex items-center gap-3.5 flex-1 min-w-0">
            <div className="w-11 h-11 rounded-xl bg-blue-50 text-blue-600 flex items-center justify-center shrink-0 border border-blue-200/80 shadow-xs">
              <i className="fa-solid fa-radar text-lg text-blue-600"></i>
            </div>
            <div className="flex-1 min-w-0">
              <div className="flex flex-wrap items-center gap-2 mb-0.5">
                <span className="text-base font-black text-slate-900 tracking-tight whitespace-nowrap">
                  1.482 vị trí đỗ trống thời gian thực
                </span>
                <span className="inline-flex items-center gap-1.5 px-2.5 py-0.5 rounded-full bg-emerald-50 text-emerald-700 border border-emerald-200 text-[10px] font-extrabold whitespace-nowrap shadow-xs">
                  <span className="w-2 h-2 rounded-full bg-emerald-500 animate-pulse"></span> ANPR Trực tuyến
                </span>
              </div>
              <p className="text-xs text-slate-500 truncate sm:whitespace-normal font-normal">
                Mạng lưới 24 bãi đỗ thông minh kết nối camera AI ANPR &amp; cảm biến quang học tại TP.HCM
              </p>
            </div>
          </div>

          <div className="flex items-center gap-3.5 bg-slate-50 px-4 py-2.5 rounded-xl border border-slate-200 shrink-0 justify-between md:justify-end shadow-xs">
            <div className="text-left md:text-right">
              <span className="text-[10px] text-slate-400 uppercase font-bold tracking-wider block">Xe đang liên kết</span>
              <span className="text-xs font-black text-slate-800 tracking-wide">
                {activeVehicle
                  ? `${activeVehicle.plateDisplay || activeVehicle.plateNumber} (${activeVehicle.brand || ''} ${activeVehicle.model || 'Xe'} - Cao ${activeVehicle.heightCm} cm)`
                  : '51K-889.32 (Mercedes C300 - Cao 145 cm)'}
              </span>
            </div>
            <button
              onClick={() => navigate('/driver/vehicles')}
              className="px-3.5 py-1.5 rounded-lg bg-blue-600 hover:bg-blue-700 text-white text-xs font-bold flex items-center gap-1.5 transition-all shadow-sm active:scale-[0.98] cursor-pointer"
              type="button"
            >
              <i className="fa-solid fa-repeat text-[10px] text-white"></i>
              <span>Đổi xe</span>
            </button>
          </div>
        </div>
      </section>

      {/* Recommended Parking Section */}
      <main className="max-w-7xl w-full mx-auto px-4 sm:px-6 my-6 relative z-10 flex-1">
        <div className="flex items-center justify-between mb-4">
          <div className="flex items-center gap-2.5">
            <span className="w-2.5 h-2.5 rounded-full bg-blue-600 ring-4 ring-blue-100"></span>
            <h2 className="text-base font-extrabold text-slate-900 tracking-tight">Đề xuất tối ưu &amp; Bãi đỗ gần bạn</h2>
            {searchResults.length > 0 && <span className="px-2.5 py-0.5 rounded-full bg-blue-600 text-white text-[10px] font-bold shadow-xs">{searchResults.length} bãi gần nhất</span>}
          </div>
          <button
            onClick={() => navigate('/driver/parking-lots')}
            className="text-xs font-bold text-blue-700 hover:text-blue-800 hover:underline flex items-center gap-1.5 group cursor-pointer"
          >
            <span>Xem trên bản đồ trên</span>
            <i className="fa-solid fa-arrow-up-right-from-square text-[10px] transition-transform group-hover:translate-x-0.5 group-hover:-translate-y-0.5"></i>
          </button>
        </div>

        {/* 4 Cards Grid */}
        <div className="grid grid-cols-1 sm:grid-cols-2 lg:grid-cols-4 gap-4">
          {isLoading ? (
            Array.from({ length: 4 }).map((_, idx) => (
              <div key={idx} className="bg-white rounded-2xl border-2 border-slate-100 p-4 shadow-sm animate-pulse h-80 flex flex-col justify-between">
                <div>
                  <div className="h-36 bg-slate-200 rounded-xl mb-3"></div>
                  <div className="h-4 bg-slate-200 rounded w-3/4 mb-2"></div>
                  <div className="h-3 bg-slate-200 rounded w-1/2 mb-4"></div>
                </div>
                <div className="h-10 bg-slate-100 rounded-xl"></div>
              </div>
            ))
          ) : searchResults.length === 0 ? (
            <div className="col-span-full py-12 px-6 bg-white rounded-2xl border-2 border-dashed border-slate-200 text-center">
              <div className="w-16 h-16 mx-auto mb-3 rounded-full bg-blue-50 text-blue-600 flex items-center justify-center text-2xl">
                <i className="fa-solid fa-map-location-dot"></i>
              </div>
              <h3 className="font-extrabold text-slate-800 text-base mb-1">
                Không tìm thấy bãi đỗ nào trong bán kính {radius} km
              </h3>
              <p className="text-slate-500 text-xs mb-4 max-w-md mx-auto">
                Khu vực xung quanh hiện chưa có bãi đỗ phù hợp hoặc các bãi đã tạm đóng. Hãy thử mở rộng phạm vi tìm kiếm.
              </p>
              <div className="flex items-center justify-center gap-3">
                <button
                  type="button"
                  onClick={() => {
                    setRadius('10');
                    performSearch(coords.lat, coords.lng, 10);
                  }}
                  className="px-4 py-2 rounded-xl bg-blue-50 hover:bg-blue-100 text-blue-700 font-bold text-xs transition-colors border border-blue-200 cursor-pointer"
                >
                  Mở rộng 10 km
                </button>
                <button
                  type="button"
                  onClick={() => {
                    setRadius('15');
                    performSearch(coords.lat, coords.lng, 15);
                  }}
                  className="px-4 py-2 rounded-xl bg-blue-600 hover:bg-blue-700 text-white font-bold text-xs transition-colors shadow-sm cursor-pointer"
                >
                  Mở rộng 15 km
                </button>
              </div>
            </div>
          ) : (
            searchResults.map((lot) => {
              const isAvailable = lot.availableSlots > 0;
              const statusText = lot.isOpenNow
                ? (isAvailable ? 'Đang mở cửa' : 'Đã kín chỗ')
                : 'Đã đóng cửa';
              const statusColor = lot.isOpenNow
                ? (isAvailable ? 'bg-emerald-500' : 'bg-rose-500')
                : 'bg-slate-500';
              const defaultImg = 'https://images.unsplash.com/photo-1506521781263-d8422e82f27a?w=800&q=80';
              const tagText = lot.amenities && lot.amenities.length > 0 ? lot.amenities[0] : (lot.availableSlots > 20 ? '⚡ Còn nhiều chỗ' : 'Giữ chỗ tức thì');

              return (
                <article
                  key={lot.id}
                  className="bg-white rounded-2xl border-2 border-blue-100 hover:border-blue-400 overflow-hidden shadow-md hover:shadow-xl transition-all duration-300 flex flex-col group"
                >
                  <div className="relative h-36 overflow-hidden bg-slate-900">
                    <img
                      alt={lot.name}
                      className="w-full h-full object-cover group-hover:scale-105 transition-transform duration-500"
                      src={lot.coverImageUrl || defaultImg}
                    />
                    <div className="absolute inset-0 bg-gradient-to-t from-black/70 via-transparent to-black/30"></div>
                    <div className="absolute top-2.5 left-2.5 right-2.5 flex items-center justify-between z-10">
                      <span className={`inline-flex items-center gap-1.5 px-2.5 py-0.5 rounded-full ${statusColor} text-white text-[10px] font-bold shadow-sm`}>
                        <span className="w-1.5 h-1.5 rounded-full bg-white"></span> {statusText}
                      </span>
                      {lot.ratingAverage && lot.ratingAverage > 0 && (
                        <span className="inline-flex items-center gap-1 px-2 py-0.5 rounded-full bg-black/50 backdrop-blur-sm text-amber-300 text-[10px] font-bold">
                          <i className="fa-solid fa-star text-[9px]"></i> {lot.ratingAverage.toFixed(1)}
                        </span>
                      )}
                    </div>
                    <div className="absolute bottom-2.5 right-2.5 z-10">
                      <span className="inline-flex items-center gap-1 px-2.5 py-0.5 rounded-full bg-amber-500 text-white text-[10px] font-bold shadow-md">
                        {tagText}
                      </span>
                    </div>
                  </div>

                  <div className="p-3.5 flex-1 flex flex-col justify-between">
                    <div>
                      <div className="flex items-start justify-between gap-1 mb-0.5">
                        <h3 className="font-extrabold text-slate-900 text-sm truncate" title={lot.name}>{lot.name}</h3>
                      </div>
                      <p className="text-slate-500 text-[11px] mb-1.5 truncate" title={lot.address}>{lot.address}</p>
                      <div className="flex items-center gap-1 text-[10px] text-slate-500 font-medium mb-3">
                        <i className="fa-solid fa-arrow-down-up-across-line text-blue-500 text-[10px]"></i>
                        <span>Trần tối đa: <strong className="text-slate-700 font-bold">{lot.maxHeightCm} cm</strong></span>
                      </div>

                      <div className="flex items-center justify-between text-center py-2 px-2 bg-blue-50/60 rounded-xl text-slate-700 mb-3 border border-blue-100">
                        <div className="flex-1">
                          <span className="text-[9px] text-slate-500 block uppercase font-bold tracking-wider">Khoảng cách</span>
                          <span className="text-xs font-bold text-slate-800">
                            <i className="fa-solid fa-location-dot text-blue-600 text-[10px] mr-1"></i>
                            {lot.distanceKm} km
                          </span>
                        </div>
                        <div className="w-px h-6 bg-blue-200/80"></div>
                        <div className="flex-1">
                          <span className="text-[9px] text-slate-500 block uppercase font-bold tracking-wider">Thời gian</span>
                          <span className="text-xs font-bold text-slate-800">
                            <i className="fa-regular fa-clock text-blue-600 text-[10px] mr-1"></i>
                            {lot.estimatedTravelTimeMinutes} phút
                          </span>
                        </div>
                        <div className="w-px h-6 bg-blue-200/80"></div>
                        <div className="flex-1">
                          <span className="text-[9px] text-slate-500 block uppercase font-bold tracking-wider">Chỗ trống</span>
                          <span className={`text-xs font-black ${lot.availableSlots > 0 ? 'text-emerald-600' : 'text-rose-600'}`}>
                            {lot.availableSlots} chỗ
                          </span>
                        </div>
                      </div>
                    </div>

                    <div className="flex items-center gap-2 pt-1 border-t border-slate-100">
                      <button
                        onClick={() => window.open(`https://www.google.com/maps/dir/?api=1&destination=${lot.latitude},${lot.longitude}`, '_blank')}
                        type="button"
                        className="flex-1 py-1.5 rounded-xl border border-slate-200 hover:bg-slate-50 text-slate-700 font-bold text-xs transition-colors flex items-center justify-center gap-1.5 cursor-pointer"
                      >
                        <i className="fa-solid fa-diamond-turn-right text-blue-600 text-[11px]"></i>
                        <span>Chỉ đường</span>
                      </button>
                      <button
                        onClick={() => navigate(`/driver/parking-lots/${lot.id}`)}
                        type="button"
                        className="flex-1 py-1.5 rounded-xl bg-blue-600 hover:bg-blue-700 text-white font-bold text-xs transition-all shadow-xs flex items-center justify-center gap-1.5 cursor-pointer"
                      >
                        <i className="fa-solid fa-bolt text-[11px]"></i>
                        <span>Đặt chỗ</span>
                      </button>
                    </div>
                  </div>
                </article>
              );
            })
          )}
        </div>
        </main>

      {/* Trust & Performance Statistics Banner */}
      <section className="w-full bg-slate-900 text-slate-300 py-6 border-t border-slate-800 mt-12">
        <div className="max-w-7xl mx-auto px-4 sm:px-6 flex flex-wrap items-center justify-between gap-6 text-xs">
          <div className="flex items-center gap-2">
            <span className="w-2.5 h-2.5 rounded-full bg-emerald-400"></span>
            <span className="font-bold text-white text-sm">500.000+</span>
            <span className="text-slate-400">lượt gửi an toàn/tháng</span>
          </div>
          <div className="flex items-center gap-2">
            <span className="text-amber-400 font-bold">⚡</span>
            <span className="font-bold text-white text-sm">&lt; 0.2s</span>
            <span className="text-slate-400">độ trễ nhận diện ANPR</span>
          </div>
          <div className="flex items-center gap-2">
            <span className="text-emerald-400 font-bold">✓</span>
            <span className="font-bold text-white text-sm">100%</span>
            <span className="text-slate-400">đảm bảo giữ chỗ thành công</span>
          </div>
          <div className="flex items-center gap-2 bg-slate-800 px-3.5 py-1.5 rounded-xl border border-slate-700">
            <i className="fa-solid fa-headset text-blue-400"></i>
            <span className="text-slate-300">Hotline 24/7:</span>
            <span className="text-white font-bold">1900 6868</span>
          </div>
        </div>
      </section>
    </div>
  );
};
