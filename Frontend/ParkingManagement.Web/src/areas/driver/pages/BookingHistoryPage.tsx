import React, { useState } from 'react';
import { useNavigate } from 'react-router-dom';

interface BookingItem {
  id: string;
  code: string;
  plate: string;
  carModel: string;
  location: string;
  spot: string;
  timeRange: string;
  dateLabel: string;
  services: string[];
  totalPrice: number;
  paymentMethod: string;
  status: 'upcoming' | 'completed' | 'cancelled';
  statusText: string;
  rating?: number;
  reviewed?: boolean;
}

const INITIAL_BOOKINGS: BookingItem[] = [
  {
    id: '1',
    code: '#PKM-2025-8842',
    plate: '51K-889.32',
    carModel: 'Mercedes EQE SUV',
    location: 'Landmark 81 Parking Garage',
    spot: 'Vị trí B-04 • Hầm B2 (Cạnh trụ B-12)',
    timeRange: '14:00 - 18:00 (4h 00m)',
    dateLabel: 'Hôm nay',
    services: ['Sạc nhanh EV 120kW', 'Bảo hiểm VIP'],
    totalPrice: 156000,
    paymentMethod: 'Ví ParkMaster',
    status: 'upcoming',
    statusText: 'Sắp diễn ra',
  },
  {
    id: '2',
    code: '#PKM-2025-7621',
    plate: '51K-889.32',
    carModel: 'Mercedes EQE SUV',
    location: 'Bitexco Financial Tower',
    spot: 'Vị trí A-15 • Tầng Hầm B1',
    timeRange: '09:15 - 12:45 (3h 30m)',
    dateLabel: 'Hôm qua',
    services: ['Đỗ xe tiêu chuẩn'],
    totalPrice: 115000,
    paymentMethod: 'Ví MoMo AutoPay',
    status: 'completed',
    statusText: 'Đã hoàn thành',
  },
  {
    id: '3',
    code: '#PKM-2025-6320',
    plate: '51K-889.32',
    carModel: 'Mercedes EQE SUV',
    location: 'Saigon Centre / Takashimaya',
    spot: 'Vị trí C-08 • Tầng Hầm B3',
    timeRange: '17:00 - 21:00 (4h 00m)',
    dateLabel: '18/10/2025',
    services: ['Sạc nhanh EV 120kW', 'Rửa xe bọt tuyết'],
    totalPrice: 210000,
    paymentMethod: 'Thẻ Visa **8842',
    status: 'completed',
    statusText: 'Đã hoàn thành',
    reviewed: true,
    rating: 5,
  },
  {
    id: '4',
    code: '#PKM-2025-5412',
    plate: '51K-889.32',
    carModel: 'Mercedes EQE SUV',
    location: 'Sân bay Tân Sơn Nhất (Ga T3 TCP)',
    spot: 'Vị trí VIP-02 (Tầng 2)',
    timeRange: '06:00 - 20:00 (3 ngày lưu bãi)',
    dateLabel: '12/10 - 15/10',
    services: ['Đỗ dài ngày VIP', 'Camera bảo vệ 24/7'],
    totalPrice: 450000,
    paymentMethod: 'Ví VETC AutoPay',
    status: 'completed',
    statusText: 'Đã hoàn thành',
  },
  {
    id: '5',
    code: '#PKM-2025-4109',
    plate: '51K-889.32',
    carModel: 'Mercedes EQE SUV',
    location: 'Vincom Center Đồng Khởi',
    spot: 'Vị trí B-22 • Tầng Hầm B2',
    timeRange: 'Hủy trước giờ đỗ 45 phút',
    dateLabel: '05/10/2025',
    services: ['Đỗ xe tiêu chuẩn'],
    totalPrice: 0,
    paymentMethod: 'Đã hoàn 100% (90.000 đ)',
    status: 'cancelled',
    statusText: 'Đã hủy (Hoàn 100%)',
  },
];

const RATING_DESCRIPTIONS: Record<number, string> = {
  1: 'Rất không hài lòng (1 sao)',
  2: 'Chưa hài lòng (2 sao)',
  3: 'Tạm ổn (3 sao)',
  4: 'Hài lòng (4 sao)',
  5: 'Rất hài lòng (5 sao)',
};

export const BookingHistoryPage: React.FC = () => {
  const navigate = useNavigate();

  const [bookings, setBookings] = useState<BookingItem[]>(INITIAL_BOOKINGS);
  const [activeTab, setActiveTab] = useState<'all' | 'upcoming' | 'completed' | 'cancelled'>('all');
  const [searchQuery, setSearchQuery] = useState('');

  // Modals state
  const [activeFeedbackBooking, setActiveFeedbackBooking] = useState<BookingItem | null>(null);
  const [ratingValue, setRatingValue] = useState<number>(5);
  const [selectedTags, setSelectedTags] = useState<string[]>(['✓ Vị trí dễ tìm']);
  const [commentText, setCommentText] = useState<string>('');
  const [showToast, setShowToast] = useState<boolean>(false);

  // QR Modal
  const [activeQrBooking, setActiveQrBooking] = useState<BookingItem | null>(null);

  const availableTags = [
    '✓ Vị trí dễ tìm',
    '⚡ Sạc EV nhanh',
    '🤝 Bảo vệ nhiệt tình',
    '⏱ Ra vào nhanh chóng',
    '🔒 An ninh đảm bảo',
  ];

  const handleOpenFeedback = (booking: BookingItem) => {
    setActiveFeedbackBooking(booking);
    setRatingValue(booking.rating || 5);
    setCommentText('');
    setSelectedTags(['✓ Vị trí dễ tìm']);
  };

  const handleToggleTag = (tag: string) => {
    setSelectedTags((prev) =>
      prev.includes(tag) ? prev.filter((t) => t !== tag) : [...prev, tag]
    );
  };

  const handleSubmitFeedback = () => {
    if (!activeFeedbackBooking) return;
    setBookings((prev) =>
      prev.map((b) =>
        b.id === activeFeedbackBooking.id ? { ...b, reviewed: true, rating: ratingValue } : b
      )
    );
    setActiveFeedbackBooking(null);
    setShowToast(true);
    setTimeout(() => setShowToast(false), 3500);
  };

  const filteredBookings = bookings.filter((b) => {
    if (activeTab === 'upcoming' && b.status !== 'upcoming') return false;
    if (activeTab === 'completed' && b.status !== 'completed') return false;
    if (activeTab === 'cancelled' && b.status !== 'cancelled') return false;

    if (searchQuery.trim()) {
      const q = searchQuery.toLowerCase();
      return (
        b.code.toLowerCase().includes(q) ||
        b.location.toLowerCase().includes(q) ||
        b.plate.toLowerCase().includes(q) ||
        b.spot.toLowerCase().includes(q)
      );
    }
    return true;
  });

  return (
    <div className="min-h-screen bg-slate-50 flex flex-col font-sans">
      <main className="flex-1 w-full max-w-7xl mx-auto px-4 sm:px-6 lg:px-8 py-8">
        {/* 1. Breadcrumbs & Header Section */}
        <div className="flex flex-col gap-5 mb-8">
          <nav aria-label="Breadcrumb" className="flex items-center gap-2 text-xs text-slate-500">
            <span
              onClick={() => navigate('/driver')}
              className="hover:text-blue-600 transition-colors flex items-center gap-1 cursor-pointer"
            >
              <span className="material-symbols-outlined text-[15px]">home</span>
              Trang chủ
            </span>
            <span className="text-slate-300">/</span>
            <span className="hover:text-blue-600 transition-colors cursor-pointer">Quản lý đặt chỗ</span>
            <span className="text-slate-300">/</span>
            <span className="text-blue-600 font-semibold">Lịch sử đặt</span>
          </nav>

          <div className="flex flex-col lg:flex-row lg:items-center justify-between gap-5 pb-2">
            <div className="flex flex-col gap-1.5">
              <div className="flex items-center gap-3">
                <h1 className="text-3xl sm:text-4xl font-extrabold text-slate-900 tracking-tight">
                  Lịch sử đặt chỗ
                </h1>
                <span className="inline-flex items-center gap-1.5 px-3 py-1 rounded-full bg-blue-50 text-blue-700 text-xs font-bold">
                  <span className="w-2 h-2 rounded-full bg-blue-600 animate-pulse"></span>
                  Đồng bộ thời gian thực
                </span>
              </div>
              <p className="text-sm text-slate-500 max-w-2xl">
                Theo dõi, tra cứu hóa đơn và quản lý toàn bộ các lượt đặt chỗ bãi đỗ xe thông minh của bạn.
              </p>
            </div>

            <div className="flex items-center gap-3 self-start lg:self-center">
              <button
                type="button"
                className="inline-flex items-center gap-2 px-4 py-2.5 rounded-xl bg-white border border-slate-200 text-slate-700 hover:bg-slate-50 text-xs font-semibold shadow-xs transition-all"
              >
                <span className="material-symbols-outlined text-[18px] text-slate-500">download</span>
                <span>Xuất file hóa đơn</span>
                <span className="text-[10px] px-1.5 py-0.5 rounded bg-slate-100 text-slate-600">Excel/PDF</span>
              </button>
              <button
                type="button"
                onClick={() => navigate('/driver/parking-lots')}
                className="inline-flex items-center gap-2 px-5 py-2.5 rounded-xl bg-blue-600 hover:bg-blue-700 text-white text-xs font-bold shadow-md shadow-blue-500/20 hover:shadow-lg transition-all"
              >
                <span className="material-symbols-outlined text-[18px]">add_circle</span>
                <span>Đặt chỗ mới</span>
              </button>
            </div>
          </div>
        </div>

        {/* 2. Quick Stats Overview (4 KPI Telemetry Cards) */}
        <div className="grid grid-cols-1 sm:grid-cols-2 lg:grid-cols-4 gap-4 mb-8">
          {/* KPI 1 */}
          <div className="bg-white rounded-2xl p-5 shadow-xs border border-slate-200 hover:shadow-md transition-shadow">
            <div className="flex items-center justify-between mb-3">
              <span className="text-[11px] text-slate-500 uppercase tracking-wider font-bold">Tổng lượt đặt</span>
              <div className="w-9 h-9 rounded-xl bg-blue-50 flex items-center justify-center text-blue-600">
                <span className="material-symbols-outlined text-[20px]">calendar_month</span>
              </div>
            </div>
            <div className="flex items-baseline gap-2 mb-2">
              <span className="text-3xl font-extrabold text-slate-900">28</span>
              <span className="text-sm text-slate-500">lượt</span>
            </div>
            <div className="flex items-center gap-1.5 text-xs text-emerald-700 font-bold">
              <span className="material-symbols-outlined text-[16px]">trending_up</span>
              <span>+3 lượt tháng này</span>
              <span className="text-slate-400 font-normal ml-auto">Toàn bộ kỳ</span>
            </div>
          </div>

          {/* KPI 2 */}
          <div className="bg-white rounded-2xl p-5 shadow-xs border border-slate-200 hover:shadow-md transition-shadow">
            <div className="flex items-center justify-between mb-3">
              <span className="text-[11px] text-blue-600 uppercase tracking-wider font-bold">Sắp diễn ra</span>
              <div className="w-9 h-9 rounded-xl bg-blue-100 flex items-center justify-center text-blue-600">
                <span className="material-symbols-outlined text-[20px]">schedule</span>
              </div>
            </div>
            <div className="flex items-baseline gap-2 mb-2">
              <span className="text-3xl font-extrabold text-blue-600">01</span>
              <span className="text-sm text-slate-500">lượt giữ chỗ</span>
            </div>
            <div className="inline-flex items-center gap-1.5 px-2 py-0.5 rounded-full bg-blue-50 text-blue-700 text-xs font-semibold truncate max-w-full">
              <span className="w-1.5 h-1.5 rounded-full bg-blue-600 animate-ping"></span>
              <span className="truncate">Landmark 81 · Vị trí B-04</span>
            </div>
          </div>

          {/* KPI 3 */}
          <div className="bg-white rounded-2xl p-5 shadow-xs border border-slate-200 hover:shadow-md transition-shadow">
            <div className="flex items-center justify-between mb-3">
              <span className="text-[11px] text-slate-500 uppercase tracking-wider font-bold">Đã hoàn thành</span>
              <div className="w-9 h-9 rounded-xl bg-emerald-50 flex items-center justify-center text-emerald-600">
                <span className="material-symbols-outlined text-[20px]">check_circle</span>
              </div>
            </div>
            <div className="flex items-baseline gap-2 mb-2">
              <span className="text-3xl font-extrabold text-slate-900">24</span>
              <span className="text-sm text-slate-500">phiên đỗ</span>
            </div>
            <div className="flex items-center gap-1.5 text-xs text-emerald-700 font-bold">
              <span className="material-symbols-outlined text-[16px]">verified</span>
              <span>Đúng giờ 100%</span>
              <span className="text-slate-400 font-normal ml-auto">Không vi phạm</span>
            </div>
          </div>

          {/* KPI 4 */}
          <div className="bg-white rounded-2xl p-5 shadow-xs border border-slate-200 hover:shadow-md transition-shadow">
            <div className="flex items-center justify-between mb-3">
              <span className="text-[11px] text-slate-500 uppercase tracking-wider font-bold">Tổng chi tích lũy</span>
              <div className="w-9 h-9 rounded-xl bg-slate-100 flex items-center justify-center text-slate-800">
                <span className="material-symbols-outlined text-[20px]">account_balance_wallet</span>
              </div>
            </div>
            <div className="flex items-baseline gap-1.5 mb-2">
              <span className="text-3xl font-extrabold text-slate-900">3.420.000</span>
              <span className="text-sm text-slate-500 font-semibold">đ</span>
            </div>
            <div className="flex items-center gap-1.5 text-xs text-emerald-700 font-bold">
              <span className="material-symbols-outlined text-[15px]">savings</span>
              <span>Tiết kiệm 850.000 đ qua VIP</span>
            </div>
          </div>
        </div>

        {/* 3. Filter Tabs & Search Bar Card */}
        <div className="bg-white rounded-2xl p-4 shadow-xs border border-slate-200 mb-6 flex flex-col xl:flex-row items-stretch xl:items-center justify-between gap-4">
          {/* Status Tabs */}
          <div className="flex items-center overflow-x-auto gap-1.5 bg-slate-100 p-1.5 rounded-xl shrink-0">
            <button
              type="button"
              onClick={() => setActiveTab('all')}
              className={`inline-flex items-center gap-2 px-4 py-2 rounded-lg text-xs font-bold transition-colors ${
                activeTab === 'all' ? 'bg-blue-600 text-white shadow-xs' : 'text-slate-600 hover:text-slate-900'
              }`}
            >
              <span>Tất cả</span>
              <span className={`px-2 py-0.5 rounded-full text-[10px] ${activeTab === 'all' ? 'bg-white/20 text-white' : 'bg-slate-200 text-slate-700'}`}>
                28
              </span>
            </button>
            <button
              type="button"
              onClick={() => setActiveTab('upcoming')}
              className={`inline-flex items-center gap-2 px-4 py-2 rounded-lg text-xs font-bold transition-colors ${
                activeTab === 'upcoming' ? 'bg-blue-600 text-white shadow-xs' : 'text-slate-600 hover:text-slate-900'
              }`}
            >
              <span>Sắp tới</span>
              <span className="px-2 py-0.5 rounded-full bg-blue-100 text-blue-700 text-[10px]">1</span>
            </button>
            <button
              type="button"
              onClick={() => setActiveTab('completed')}
              className={`inline-flex items-center gap-2 px-4 py-2 rounded-lg text-xs font-bold transition-colors ${
                activeTab === 'completed' ? 'bg-blue-600 text-white shadow-xs' : 'text-slate-600 hover:text-slate-900'
              }`}
            >
              <span>Đã hoàn thành</span>
              <span className="px-2 py-0.5 rounded-full bg-slate-200 text-slate-700 text-[10px]">24</span>
            </button>
            <button
              type="button"
              onClick={() => setActiveTab('cancelled')}
              className={`inline-flex items-center gap-2 px-4 py-2 rounded-lg text-xs font-bold transition-colors ${
                activeTab === 'cancelled' ? 'bg-blue-600 text-white shadow-xs' : 'text-slate-600 hover:text-slate-900'
              }`}
            >
              <span>Đã hủy</span>
              <span className="px-2 py-0.5 rounded-full bg-red-100 text-red-700 text-[10px]">3</span>
            </button>
          </div>

          {/* Search & Filters Container */}
          <div className="flex flex-wrap sm:flex-nowrap items-center gap-3 w-full xl:w-auto">
            <div className="relative flex-1 min-w-[260px] xl:w-80">
              <span className="material-symbols-outlined absolute left-3.5 top-1/2 -translate-y-1/2 text-[18px] text-slate-400">
                search
              </span>
              <input
                type="text"
                value={searchQuery}
                onChange={(e) => setSearchQuery(e.target.value)}
                placeholder="Tìm theo mã, bãi đỗ, biển số..."
                className="w-full pl-10 pr-4 py-2 h-10 rounded-xl bg-slate-50 border border-slate-200 text-slate-800 placeholder:text-slate-400 text-xs focus:bg-white focus:outline-hidden focus:ring-2 focus:ring-blue-500/20 transition-all"
              />
            </div>

            <div className="relative shrink-0">
              <button
                type="button"
                className="h-10 px-3.5 rounded-xl bg-slate-50 border border-slate-200 text-slate-700 text-xs font-semibold inline-flex items-center gap-2 hover:bg-slate-100 transition-colors"
              >
                <span className="material-symbols-outlined text-[18px] text-slate-500">calendar_today</span>
                <span>Tháng 10/2025</span>
                <span className="material-symbols-outlined text-[18px] text-slate-400">expand_more</span>
              </button>
            </div>

            <button
              type="button"
              className="h-10 px-3.5 rounded-xl bg-slate-50 border border-slate-200 text-slate-700 hover:text-slate-900 text-xs font-semibold inline-flex items-center gap-2 shrink-0 hover:bg-slate-100 transition-colors"
            >
              <span className="material-symbols-outlined text-[18px]">tune</span>
              <span>Lọc nâng cao</span>
            </button>
          </div>
        </div>

        {/* 4. Booking History List / Table */}
        <div className="bg-white rounded-2xl shadow-xs border border-slate-200 overflow-hidden mb-8">
          <div className="overflow-x-auto w-full">
            <table className="w-full text-left border-collapse">
              <thead>
                <tr className="bg-slate-50 border-b border-slate-200 text-slate-500 text-[11px] uppercase tracking-wider font-bold">
                  <th className="py-3.5 px-6">Mã đặt chỗ & Biển số</th>
                  <th className="py-3.5 px-6">Bãi đỗ & Vị trí</th>
                  <th className="py-3.5 px-6">Thời gian gửi & Lấy</th>
                  <th className="py-3.5 px-6">Dịch vụ đi kèm</th>
                  <th className="py-3.5 px-6">Tổng tiền</th>
                  <th className="py-3.5 px-6">Trạng thái</th>
                  <th className="py-3.5 px-6 text-right">Thao tác</th>
                </tr>
              </thead>
              <tbody className="divide-y divide-slate-100 text-xs">
                {filteredBookings.map((b) => {
                  const isUpcoming = b.status === 'upcoming';
                  const isCancelled = b.status === 'cancelled';

                  return (
                    <tr
                      key={b.id}
                      className={`hover:bg-slate-50/80 transition-colors group ${
                        isUpcoming ? 'bg-blue-50/30' : ''
                      }`}
                    >
                      <td className="py-4 px-6 relative">
                        {isUpcoming && <div className="absolute left-0 top-0 bottom-0 w-1 bg-blue-600"></div>}
                        <div className="flex flex-col gap-1">
                          <div className="flex items-center gap-2">
                            <span className={`font-bold ${isUpcoming ? 'text-blue-600 text-sm' : isCancelled ? 'text-slate-400 line-through' : 'text-slate-800'}`}>
                              {b.code}
                            </span>
                            {isUpcoming && (
                              <span className="inline-flex items-center gap-0.5 px-1.5 py-0.5 rounded bg-blue-100 text-blue-700 text-[10px] font-bold">
                                <span className="material-symbols-outlined text-[12px]">camera</span>
                                ANPR Verified
                              </span>
                            )}
                          </div>
                          <div className="flex items-center gap-1.5 text-slate-500 text-xs">
                            <span className="material-symbols-outlined text-[16px]">directions_car</span>
                            <span className="font-semibold text-slate-800">{b.plate}</span>
                            <span className="text-slate-300">·</span>
                            <span>{b.carModel}</span>
                          </div>
                        </div>
                      </td>

                      <td className="py-4 px-6">
                        <div className="flex flex-col gap-0.5 min-w-0">
                          <span className="font-bold text-slate-900 truncate">{b.location}</span>
                          <span className="text-[11px] text-slate-500 truncate">{b.spot}</span>
                        </div>
                      </td>

                      <td className="py-4 px-6">
                        <div className="flex flex-col gap-0.5 min-w-0">
                          <div className="flex items-center gap-1.5 font-semibold text-slate-800">
                            <span className="material-symbols-outlined text-[15px] text-blue-600">calendar_today</span>
                            <span>{b.dateLabel}</span>
                          </div>
                          <div className="flex items-center gap-1 text-[11px] text-slate-500">
                            <span className="material-symbols-outlined text-[14px]">schedule</span>
                            <span>{b.timeRange}</span>
                          </div>
                        </div>
                      </td>

                      <td className="py-4 px-6">
                        <div className="flex flex-wrap gap-1.5 max-w-xs">
                          {b.services.map((srv, idx) => (
                            <span
                              key={idx}
                              className="inline-flex items-center gap-1 px-2 py-0.5 rounded bg-slate-100 text-slate-700 text-[11px] font-medium"
                            >
                              <span className="material-symbols-outlined text-[13px] text-blue-600">
                                {srv.includes('Sạc') ? 'bolt' : srv.includes('Bảo hiểm') ? 'shield' : 'local_parking'}
                              </span>
                              {srv}
                            </span>
                          ))}
                        </div>
                      </td>

                      <td className="py-4 px-6">
                        <div className="flex flex-col gap-0.5">
                          <span className="font-bold text-slate-900">
                            {b.totalPrice > 0 ? `${b.totalPrice.toLocaleString('vi-VN')} đ` : '0 đ'}
                          </span>
                          <span className="text-[11px] text-slate-500">{b.paymentMethod}</span>
                        </div>
                      </td>

                      <td className="py-4 px-6">
                        {isUpcoming && (
                          <span className="inline-flex items-center gap-1.5 px-3 py-1 rounded-full bg-blue-100 text-blue-700 text-[11px] font-bold">
                            <span className="w-1.5 h-1.5 rounded-full bg-blue-600 animate-pulse"></span>
                            Sắp diễn ra
                          </span>
                        )}
                        {b.status === 'completed' && (
                          <span className="inline-flex items-center gap-1.5 px-3 py-1 rounded-full bg-emerald-100 text-emerald-800 text-[11px] font-bold">
                            <span className="w-1.5 h-1.5 rounded-full bg-emerald-600"></span>
                            Đã hoàn thành
                          </span>
                        )}
                        {isCancelled && (
                          <span className="inline-flex items-center gap-1.5 px-3 py-1 rounded-full bg-red-100 text-red-700 text-[11px] font-bold">
                            <span className="w-1.5 h-1.5 rounded-full bg-red-600"></span>
                            {b.statusText}
                          </span>
                        )}
                      </td>

                      <td className="py-4 px-6 text-right">
                        <div className="inline-flex items-center gap-2 justify-end">
                          {isUpcoming ? (
                            <>
                              <button
                                type="button"
                                onClick={() => setActiveQrBooking(b)}
                                className="inline-flex items-center gap-1 px-3 py-1.5 rounded-lg bg-blue-600 text-white hover:bg-blue-700 text-xs font-bold shadow-xs transition-all"
                              >
                                <span className="material-symbols-outlined text-[16px]">qr_code_2</span>
                                <span>Vé / QR Vào bãi</span>
                              </button>
                              <button
                                type="button"
                                title="Hủy giữ chỗ"
                                className="p-1.5 rounded-lg text-slate-400 hover:text-red-600 hover:bg-red-50 transition-colors"
                              >
                                <span className="material-symbols-outlined text-[20px]">cancel</span>
                              </button>
                            </>
                          ) : b.status === 'completed' ? (
                            <>
                              {b.reviewed ? (
                                <span className="inline-flex items-center gap-1 px-2.5 py-1 rounded-lg bg-amber-50 text-amber-700 font-bold text-[11px] border border-amber-200">
                                  <span className="material-symbols-outlined text-[14px] text-amber-500">star</span>
                                  {b.rating} sao
                                </span>
                              ) : (
                                <button
                                  type="button"
                                  onClick={() => handleOpenFeedback(b)}
                                  className="inline-flex items-center gap-1 px-2.5 py-1.5 rounded-lg border border-amber-300 bg-amber-50 hover:bg-amber-100 text-amber-800 font-bold text-xs transition-all shadow-2xs"
                                >
                                  <span className="material-symbols-outlined text-[16px] text-amber-500">star</span>
                                  <span>Đánh giá</span>
                                </button>
                              )}
                              <button
                                type="button"
                                onClick={() => setActiveQrBooking(b)}
                                className="px-3 py-1.5 rounded-lg bg-slate-100 hover:bg-slate-200 text-slate-700 font-medium text-xs transition-colors"
                              >
                                Chi tiết
                              </button>
                              <button
                                type="button"
                                onClick={() => navigate('/driver/parking-lots')}
                                className="px-3 py-1.5 rounded-lg text-blue-600 hover:bg-blue-50 font-bold text-xs transition-colors"
                              >
                                Đặt lại
                              </button>
                            </>
                          ) : (
                            <>
                              <button
                                type="button"
                                className="px-3 py-1.5 rounded-lg bg-slate-100 text-slate-600 text-xs font-medium"
                              >
                                Lý do hoàn
                              </button>
                              <button
                                type="button"
                                onClick={() => navigate('/driver/parking-lots')}
                                className="px-3 py-1.5 rounded-lg text-blue-600 hover:bg-blue-50 font-bold text-xs transition-colors"
                              >
                                Đặt lại
                              </button>
                            </>
                          )}
                        </div>
                      </td>
                    </tr>
                  );
                })}
              </tbody>
            </table>
          </div>

          {/* Pagination */}
          <div className="bg-white px-6 py-4 flex flex-col sm:flex-row items-center justify-between gap-4 border-t border-slate-100 text-xs">
            <span className="text-slate-500">
              Hiển thị <span className="font-bold text-slate-800">1 - {filteredBookings.length}</span> trong tổng số{' '}
              <span className="font-bold text-slate-800">28</span> lượt đặt chỗ
            </span>

            <div className="flex items-center gap-1.5">
              <button
                type="button"
                disabled
                className="px-3 py-1.5 rounded-lg text-slate-400 font-medium bg-slate-100 cursor-not-allowed inline-flex items-center gap-1"
              >
                <span className="material-symbols-outlined text-[16px]">chevron_left</span>
                Trước
              </button>
              <button type="button" className="w-8 h-8 rounded-lg bg-blue-600 text-white font-bold flex items-center justify-center">
                1
              </button>
              <button type="button" className="w-8 h-8 rounded-lg text-slate-700 hover:bg-slate-100 font-medium flex items-center justify-center transition-colors">
                2
              </button>
              <button type="button" className="w-8 h-8 rounded-lg text-slate-700 hover:bg-slate-100 font-medium flex items-center justify-center transition-colors">
                3
              </button>
              <span className="px-1 text-slate-400">...</span>
              <button type="button" className="px-3 py-1.5 rounded-lg text-slate-700 hover:bg-slate-100 font-medium bg-slate-100 inline-flex items-center gap-1 transition-colors">
                Sau
                <span className="material-symbols-outlined text-[16px]">chevron_right</span>
              </button>
            </div>
          </div>
        </div>

        {/* 6. Help & Guarantee Banner Card */}
        <div className="rounded-2xl p-5 bg-gradient-to-r from-blue-50 via-indigo-50 to-slate-50 border border-blue-100 shadow-xs flex flex-col md:flex-row md:items-center justify-between gap-5">
          <div className="flex items-center gap-4">
            <div className="w-12 h-12 rounded-xl bg-blue-600 text-white flex items-center justify-center shrink-0 shadow-sm">
              <span className="material-symbols-outlined text-[26px]">support_agent</span>
            </div>
            <div className="flex flex-col">
              <span className="text-sm font-bold text-slate-900">
                Cần xuất hóa đơn VAT điện tử hoặc giải quyết khiếu nại?
              </span>
              <p className="text-xs text-slate-500">
                Hỗ trợ tức thời 24/7 qua tổng đài <span className="font-bold text-blue-600">1900 6868</span> hoặc email{' '}
                <span className="font-semibold text-slate-700">hotro@parkmaster.vn</span>. Hoàn tiền 100% nếu bãi đỗ không đúng cam kết.
              </p>
            </div>
          </div>

          <div className="flex items-center flex-wrap gap-2.5 shrink-0">
            <div className="inline-flex items-center gap-1.5 px-3 py-1.5 rounded-lg bg-white text-slate-600 text-xs shadow-2xs border border-slate-200">
              <span className="material-symbols-outlined text-[16px] text-emerald-600">verified_user</span>
              <span className="font-semibold">PCI-DSS Level 1</span>
            </div>
            <div className="inline-flex items-center gap-1.5 px-3 py-1.5 rounded-lg bg-white text-slate-600 text-xs shadow-2xs border border-slate-200">
              <span className="material-symbols-outlined text-[16px] text-blue-600">security</span>
              <span className="font-semibold">ISO 27001 Security</span>
            </div>
            <div className="inline-flex items-center gap-1.5 px-3 py-1.5 rounded-lg bg-white text-slate-600 text-xs shadow-2xs border border-slate-200">
              <span className="material-symbols-outlined text-[16px] text-emerald-600">center_focus_strong</span>
              <span className="font-semibold">AI ANPR 99.98%</span>
            </div>
          </div>
        </div>
      </main>

      {/* 7. Interactive Feedback Modal */}
      {activeFeedbackBooking && (
        <div className="fixed inset-0 bg-slate-900/60 backdrop-blur-xs z-50 flex items-center justify-center p-4 animate-in fade-in">
          <div className="bg-white rounded-2xl shadow-2xl p-6 sm:p-7 w-full max-w-lg border border-slate-200 max-h-[92vh] overflow-y-auto">
            {/* Header */}
            <div className="flex items-center justify-between pb-3 border-b border-slate-100">
              <div className="flex items-center gap-2.5">
                <div className="w-9 h-9 rounded-xl bg-amber-50 border border-amber-200 flex items-center justify-center text-amber-500">
                  <span className="material-symbols-outlined text-[20px]">hotel_class</span>
                </div>
                <div>
                  <h3 className="font-bold text-lg text-slate-900 leading-tight">Đánh giá dịch vụ</h3>
                  <p className="text-xs text-slate-500">Phản hồi của bạn giúp nâng cấp chất lượng bãi đỗ</p>
                </div>
              </div>
              <button
                type="button"
                onClick={() => setActiveFeedbackBooking(null)}
                className="text-slate-400 hover:text-slate-700 p-1 rounded-lg hover:bg-slate-100 transition-colors"
              >
                <span className="material-symbols-outlined text-[22px]">close</span>
              </button>
            </div>

            {/* Booking Summary Box */}
            <div className="bg-slate-50 border border-slate-200 rounded-xl p-3.5 mt-4 mb-5 flex items-center gap-3">
              <div className="w-10 h-10 rounded-lg bg-blue-50 flex items-center justify-center text-blue-600 shrink-0">
                <span className="material-symbols-outlined text-[22px]">local_parking</span>
              </div>
              <div className="flex flex-col min-w-0">
                <div className="flex items-center gap-2">
                  <span className="font-bold text-slate-900 text-xs truncate">
                    {activeFeedbackBooking.location}
                  </span>
                  <span className="px-1.5 py-0.5 rounded bg-blue-100 text-blue-700 text-[10px] font-bold shrink-0">
                    {activeFeedbackBooking.code}
                  </span>
                </div>
                <div className="text-[11px] text-slate-500 truncate mt-0.5">
                  {activeFeedbackBooking.spot} • {activeFeedbackBooking.timeRange}
                </div>
              </div>
            </div>

            {/* 5-Star Rating */}
            <div className="mb-5 flex flex-col items-center justify-center p-4 bg-amber-50/50 rounded-xl border border-amber-200/60">
              <span className="text-xs font-bold uppercase tracking-wider text-slate-600 mb-1">
                Mức độ hài lòng của bạn
              </span>
              <span className="text-sm font-bold text-amber-700 mb-2.5">
                {RATING_DESCRIPTIONS[ratingValue]}
              </span>
              <div className="flex items-center gap-2">
                {[1, 2, 3, 4, 5].map((star) => (
                  <button
                    key={star}
                    type="button"
                    onClick={() => setRatingValue(star)}
                    className="p-1 transition-transform hover:scale-110 active:scale-95 focus:outline-hidden"
                  >
                    <svg
                      className={`w-8 h-8 transition-colors ${
                        star <= ratingValue ? 'text-amber-400 fill-amber-400' : 'text-slate-300 fill-slate-300'
                      }`}
                      viewBox="0 0 24 24"
                    >
                      <path d="M12 17.27L18.18 21l-1.64-7.03L22 9.24l-7.19-.61L12 2 9.19 8.63 2 9.24l5.46 4.73L5.82 21z" />
                    </svg>
                  </button>
                ))}
              </div>
            </div>

            {/* Quick Tag Pills */}
            <div className="mb-4">
              <label className="block text-xs font-bold text-slate-700 mb-2">Điểm bạn hài lòng nhất:</label>
              <div className="flex flex-wrap gap-2">
                {availableTags.map((tag) => {
                  const isSelected = selectedTags.includes(tag);
                  return (
                    <button
                      key={tag}
                      type="button"
                      onClick={() => handleToggleTag(tag)}
                      className={`px-3 py-1.5 rounded-full text-xs font-medium transition-colors cursor-pointer select-none border ${
                        isSelected
                          ? 'border-blue-600 bg-blue-50 text-blue-700 font-bold'
                          : 'border-slate-200 text-slate-600 bg-white hover:bg-slate-50'
                      }`}
                    >
                      {tag}
                    </button>
                  );
                })}
              </div>
            </div>

            {/* Comment Section */}
            <div className="mb-5">
              <div className="flex items-center justify-between mb-1.5">
                <label className="text-xs font-bold text-slate-700">Chia sẻ trải nghiệm của bạn</label>
                <span className="text-[11px] text-slate-400">{commentText.length}/500</span>
              </div>
              <textarea
                value={commentText}
                onChange={(e) => setCommentText(e.target.value.slice(0, 500))}
                rows={3}
                placeholder="Hãy chia sẻ cảm nhận về dịch vụ đỗ xe, tiện ích trạm sạc hoặc nhân viên hỗ trợ..."
                className="w-full rounded-xl border border-slate-200 focus:ring-2 focus:ring-blue-600 p-3 text-xs text-slate-800 placeholder:text-slate-400 focus:outline-hidden transition resize-none"
              />
            </div>

            {/* Actions */}
            <div className="flex items-center justify-end gap-3 pt-2 border-t border-slate-100">
              <button
                type="button"
                onClick={() => setActiveFeedbackBooking(null)}
                className="px-4 py-2.5 rounded-xl border border-slate-200 text-slate-600 hover:bg-slate-100 text-xs font-semibold transition-colors"
              >
                Hủy
              </button>
              <button
                type="button"
                onClick={handleSubmitFeedback}
                className="px-5 py-2.5 rounded-xl bg-blue-600 hover:bg-blue-700 text-white font-bold text-xs shadow-xs hover:shadow transition-all flex items-center justify-center gap-2"
              >
                <span className="material-symbols-outlined text-[16px]">send</span>
                <span>Gửi đánh giá</span>
              </button>
            </div>
          </div>
        </div>
      )}

      {/* QR Code Entrance Modal */}
      {activeQrBooking && (
        <div className="fixed inset-0 bg-slate-900/60 backdrop-blur-xs z-50 flex items-center justify-center p-4 animate-in fade-in">
          <div className="bg-white rounded-2xl shadow-2xl p-6 sm:p-7 w-full max-w-sm border border-slate-200 flex flex-col items-center text-center">
            <div className="flex items-center justify-between w-full mb-3">
              <span className="text-xs font-bold text-blue-600">{activeQrBooking.code}</span>
              <button
                type="button"
                onClick={() => setActiveQrBooking(null)}
                className="text-slate-400 hover:text-slate-700 p-1"
              >
                <span className="material-symbols-outlined text-[20px]">close</span>
              </button>
            </div>

            <h3 className="text-lg font-bold text-slate-900">Mã QR Vé Vào Bãi</h3>
            <p className="text-xs text-slate-500 mt-1 mb-4">
              Quét mã tại trụ cảm biến cổng vào hoặc camera ANPR sẽ tự nhận diện biển số <strong>{activeQrBooking.plate}</strong>
            </p>

            {/* Simulated QR Code Visual */}
            <div className="p-4 bg-white rounded-2xl border-2 border-slate-900 shadow-inner flex flex-col items-center mb-4">
              <div className="w-48 h-48 bg-slate-900 flex items-center justify-center rounded-xl p-2 relative">
                {/* SVG QR Code Pattern */}
                <svg className="w-full h-full text-white" viewBox="0 0 100 100" fill="currentColor">
                  {/* Outer corner squares */}
                  <rect x="5" y="5" width="25" height="25" rx="3" />
                  <rect x="10" y="10" width="15" height="15" fill="#0f172a" />
                  <rect x="13" y="13" width="9" height="9" />

                  <rect x="70" y="5" width="25" height="25" rx="3" />
                  <rect x="75" y="10" width="15" height="15" fill="#0f172a" />
                  <rect x="78" y="13" width="9" height="9" />

                  <rect x="5" y="70" width="25" height="25" rx="3" />
                  <rect x="10" y="75" width="15" height="15" fill="#0f172a" />
                  <rect x="13" y="78" width="9" height="9" />

                  {/* Middle random dots pattern */}
                  <rect x="35" y="10" width="8" height="8" />
                  <rect x="50" y="15" width="12" height="6" />
                  <rect x="40" y="35" width="20" height="20" rx="4" fill="#3b82f6" />
                  <rect x="10" y="45" width="8" height="15" />
                  <rect x="25" y="50" width="15" height="8" />
                  <rect x="70" y="45" width="18" height="8" />
                  <rect x="65" y="70" width="10" height="18" />
                  <rect x="40" y="75" width="15" height="12" />
                </svg>
              </div>
            </div>

            <div className="w-full p-3 bg-slate-50 rounded-xl text-left text-xs flex flex-col gap-1 mb-4 border border-slate-200">
              <div className="flex justify-between">
                <span className="text-slate-500">Bãi đỗ:</span>
                <span className="font-bold text-slate-800">{activeQrBooking.location}</span>
              </div>
              <div className="flex justify-between">
                <span className="text-slate-500">Vị trí:</span>
                <span className="font-bold text-emerald-600">{activeQrBooking.spot}</span>
              </div>
              <div className="flex justify-between">
                <span className="text-slate-500">Thời gian:</span>
                <span className="font-semibold text-slate-700">{activeQrBooking.timeRange}</span>
              </div>
            </div>

            <button
              type="button"
              onClick={() => setActiveQrBooking(null)}
              className="w-full py-2.5 rounded-xl bg-slate-100 text-slate-700 font-bold text-xs hover:bg-slate-200 transition-colors"
            >
              Đóng
            </button>
          </div>
        </div>
      )}

      {/* Toast Notification */}
      {showToast && (
        <div className="fixed bottom-6 right-6 z-50 animate-in fade-in slide-in-from-bottom-5">
          <div className="bg-slate-900 text-white px-5 py-3.5 rounded-xl shadow-xl flex items-center gap-3 border border-slate-700">
            <div className="w-7 h-7 rounded-full bg-emerald-500/20 text-emerald-400 flex items-center justify-center shrink-0">
              <span className="material-symbols-outlined text-[18px]">check_circle</span>
            </div>
            <div>
              <p className="font-bold text-xs">Đã ghi nhận đánh giá!</p>
              <p className="text-[11px] text-slate-300">Cảm ơn bạn đã đóng góp phản hồi cho ParkMaster.</p>
            </div>
          </div>
        </div>
      )}
    </div>
  );
};
