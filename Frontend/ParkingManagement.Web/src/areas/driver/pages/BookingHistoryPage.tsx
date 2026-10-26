import React, { useEffect, useState } from 'react';
import { useNavigate, useSearchParams } from 'react-router-dom';

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
    location: 'Landmark 81 Garage',
    spot: 'Vị trí B-04 • Tầng Hầm B2',
    timeRange: '14:00 - 18:00 (Bắt đầu sau 3h 30m)',
    dateLabel: 'Hôm nay',
    services: ['Sạc nhanh EV 120kW'],
    totalPrice: 175000,
    paymentMethod: 'Ví MoMo AutoPay',
    status: 'upcoming',
    statusText: 'Sắp diễn ra (Trước hạn 1h)',
  },
  {
    id: '6',
    code: '#PKM-2025-9901',
    plate: '51K-889.32',
    carModel: 'Mercedes EQE SUV',
    location: 'Crescent Mall Phú Mỹ Hưng',
    spot: 'Vị trí P-12 • Tầng Hầm B1',
    timeRange: '11:15 - 15:15 (Bắt đầu sau 35 phút)',
    dateLabel: 'Hôm nay',
    services: ['Đỗ xe tiêu chuẩn'],
    totalPrice: 100000,
    paymentMethod: 'Ví MoMo AutoPay',
    status: 'upcoming',
    statusText: 'Sắp diễn ra (Cận giờ < 1h)',
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
    timeRange: 'Hủy lúc 09:30 • Thay đổi lịch trình di chuyển',
    dateLabel: '05/10/2025',
    services: ['Đỗ xe tiêu chuẩn'],
    totalPrice: 0,
    paymentMethod: 'Đã hoàn 100% (90.000 đ) về Ví ParkMaster',
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

  // Đến từ thông báo (/driver/history?booking=<id|code>) → highlight đúng đặt chỗ của thông báo đó.
  const [searchParams] = useSearchParams();
  const highlightParam = searchParams.get('booking');
  const highlightId = highlightParam
    ? (bookings.find((b) => b.id === highlightParam || b.code === highlightParam)?.id ?? null)
    : null;

  useEffect(() => {
    if (!highlightId) return;
    setActiveTab('all'); // Đảm bảo đặt chỗ cần highlight không bị ẩn bởi tab lọc.
    const timer = window.setTimeout(() => {
      document
        .getElementById(`booking-row-${highlightId}`)
        ?.scrollIntoView({ behavior: 'smooth', block: 'center' });
    }, 150);
    return () => window.clearTimeout(timer);
  }, [highlightId]);

  // Modals state
  const [activeFeedbackBooking, setActiveFeedbackBooking] = useState<BookingItem | null>(null);
  const [ratingValue, setRatingValue] = useState<number>(5);
  const [selectedTags, setSelectedTags] = useState<string[]>(['✓ Vị trí dễ tìm']);
  const [commentText, setCommentText] = useState<string>('');
  const [showToast, setShowToast] = useState<boolean>(false);

  // QR Modal
  const [activeQrBooking, setActiveQrBooking] = useState<BookingItem | null>(null);

  // UI-10: Cancellation & Refund State
  const [activeCancelBooking, setActiveCancelBooking] = useState<BookingItem | null>(null);
  const [activeRefundDetailBooking, setActiveRefundDetailBooking] = useState<BookingItem | null>(null);
  const [cancelReason, setCancelReason] = useState<string>('schedule_change');
  const [customReason, setCustomReason] = useState<string>('');
  const [refundMethod, setRefundMethod] = useState<'wallet' | 'original'>('wallet');
  const [isCancelling, setIsCancelling] = useState<boolean>(false);
  const [cancelPolicyScenario, setCancelPolicyScenario] = useState<'auto' | 'before_1h' | 'after_1h'>('auto');
  const [cancelToast, setCancelToast] = useState<{ title: string; message: string; refundAmount: number } | null>(null);

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
    if (selectedTags.includes(tag)) {
      setSelectedTags(selectedTags.filter((t) => t !== tag));
    } else {
      setSelectedTags([...selectedTags, tag]);
    }
  };

  const handleSubmitFeedback = () => {
    if (!activeFeedbackBooking) return;
    setBookings((prev) =>
      prev.map((b) =>
        b.id === activeFeedbackBooking.id
          ? { ...b, reviewed: true, rating: ratingValue }
          : b
      )
    );
    setActiveFeedbackBooking(null);
    setShowToast(true);
    setTimeout(() => setShowToast(false), 3500);
  };

  // UI-10: Cancellation Calculation
  const isFullRefund =
    cancelPolicyScenario === 'before_1h' ||
    (cancelPolicyScenario === 'auto' && activeCancelBooking?.id !== '6');

  const originalPrice = activeCancelBooking?.totalPrice || 0;
  const penaltyRate = isFullRefund ? 0 : 0.3;
  const penaltyFee = Math.round(originalPrice * penaltyRate);
  const refundAmount = originalPrice - penaltyFee;

  const handleOpenCancelModal = (booking: BookingItem) => {
    setActiveCancelBooking(booking);
    setCancelPolicyScenario('auto');
    setCancelReason('schedule_change');
    setCustomReason('');
    setRefundMethod('wallet');
  };

  const handleConfirmCancel = () => {
    if (!activeCancelBooking) return;
    setIsCancelling(true);

    setTimeout(() => {
      const reasonLabel =
        cancelReason === 'schedule_change'
          ? 'Thay đổi lịch trình'
          : cancelReason === 'found_other'
          ? 'Tìm thấy bãi khác tiện hơn'
          : cancelReason === 'vehicle_issue'
          ? 'Xe gặp sự cố'
          : customReason || 'Lý do cá nhân';

      const updatedStatusText = isFullRefund
        ? 'Đã hủy (Hoàn 100%)'
        : 'Đã hủy (Hoàn 70% do cận giờ)';

      const updatedRefundNote = `Đã hoàn ${refundAmount.toLocaleString('vi-VN')} đ về ${
        refundMethod === 'wallet' ? 'Ví ParkMaster' : 'tài khoản gốc'
      }`;

      setBookings((prev) =>
        prev.map((b) =>
          b.id === activeCancelBooking.id
            ? {
                ...b,
                status: 'cancelled',
                statusText: updatedStatusText,
                totalPrice: penaltyFee,
                paymentMethod: updatedRefundNote,
                timeRange: `Hủy lúc ${new Date().toLocaleTimeString('vi-VN', { hour: '2-digit', minute: '2-digit' })} • ${reasonLabel}`,
              }
            : b
        )
      );

      setIsCancelling(false);
      setActiveCancelBooking(null);
      setActiveTab('cancelled');

      setCancelToast({
        title: 'Hủy giữ chỗ thành công!',
        message: `Đã hoàn ${refundAmount.toLocaleString('vi-VN')} đ (${isFullRefund ? '100%' : '70% do cận giờ < 1h'}) vào ${
          refundMethod === 'wallet' ? 'Ví ParkMaster (Nhận ngay)' : 'tài khoản nguồn (1-3 ngày làm việc)'
        }.`,
        refundAmount,
      });

      setTimeout(() => {
        setCancelToast(null);
      }, 5500);
    }, 800);
  };

  // Filter bookings
  const filteredBookings = bookings.filter((b) => {
    if (activeTab === 'upcoming' && b.status !== 'upcoming') return false;
    if (activeTab === 'completed' && b.status !== 'completed') return false;
    if (activeTab === 'cancelled' && b.status !== 'cancelled') return false;

    if (searchQuery.trim()) {
      const q = searchQuery.toLowerCase();
      const matchCode = b.code.toLowerCase().includes(q);
      const matchLocation = b.location.toLowerCase().includes(q);
      const matchPlate = b.plate.toLowerCase().includes(q);
      if (!matchCode && !matchLocation && !matchPlate) return false;
    }

    return true;
  });

  return (
    <div className="min-h-screen bg-slate-50 flex flex-col font-sans">
      {/* 1. Sub-Header Banner */}
      <section className="bg-white border-b border-slate-200">
        <div className="max-w-7xl mx-auto px-4 sm:px-6 lg:px-8 py-8 flex flex-col md:flex-row md:items-end justify-between gap-6">
          <div className="flex flex-col gap-2">
            <div className="inline-flex items-center gap-2 px-3 py-1 rounded-full bg-blue-100 text-blue-800 w-fit text-xs font-bold tracking-wider uppercase">
              <span className="w-2 h-2 rounded-full bg-blue-600 animate-pulse"></span>
              QUẢN LÝ LƯỢT ĐẶT CHỖ & CHÍNH SÁCH HOÀN TIỀN 1 GIỜ (UI-10)
            </div>
            <h1 className="text-3xl font-extrabold text-slate-900 tracking-tight">
              Lịch sử đặt chỗ
            </h1>
            <p className="text-slate-500 text-sm max-w-2xl leading-relaxed">
              Theo dõi lịch sử gửi xe, lấy mã QR check-in tại barie, đánh giá dịch vụ hoặc quản lý hủy giữ chỗ với chính sách hoàn tiền tự động trước 1 giờ.
            </p>
          </div>

          <div className="flex items-center gap-3 self-start md:self-auto">
            <button
              type="button"
              onClick={() => navigate('/driver/parking-lots')}
              className="px-5 py-2.5 rounded-xl bg-blue-600 hover:bg-blue-700 text-white font-bold text-xs shadow-sm hover:shadow transition-all flex items-center gap-2 cursor-pointer"
            >
              <span className="material-symbols-outlined text-[18px]">add_location_alt</span>
              <span>Đặt chỗ bãi xe mới</span>
            </button>
          </div>
        </div>
      </section>

      {/* 2. Main Content Container */}
      <main className="max-w-7xl mx-auto px-4 sm:px-6 lg:px-8 py-8 flex flex-col gap-6 flex-1 w-full">
        {/* 3. KPI / Stats Cards */}
        <div className="grid grid-cols-2 lg:grid-cols-4 gap-4">
          <div className="bg-white p-5 rounded-2xl border border-slate-200 shadow-xs flex items-center justify-between">
            <div className="flex flex-col">
              <span className="text-xs font-semibold text-slate-500 uppercase tracking-wider">Tổng lượt đỗ</span>
              <span className="text-2xl font-extrabold text-slate-900 mt-1">28</span>
              <span className="text-[11px] text-emerald-600 font-medium mt-0.5">↑ 4 lượt trong tháng này</span>
            </div>
            <div className="w-12 h-12 rounded-xl bg-blue-50 text-blue-600 flex items-center justify-center shrink-0">
              <span className="material-symbols-outlined text-[24px]">local_parking</span>
            </div>
          </div>

          <div className="bg-white p-5 rounded-2xl border border-slate-200 shadow-xs flex items-center justify-between">
            <div className="flex flex-col">
              <span className="text-xs font-semibold text-slate-500 uppercase tracking-wider">Sắp diễn ra</span>
              <span className="text-2xl font-extrabold text-blue-600 mt-1">
                {bookings.filter((b) => b.status === 'upcoming').length}
              </span>
              <span className="text-[11px] text-slate-500 font-medium mt-0.5">Sẵn sàng nhận diện ANPR</span>
            </div>
            <div className="w-12 h-12 rounded-xl bg-blue-50 text-blue-600 flex items-center justify-center shrink-0">
              <span className="material-symbols-outlined text-[24px]">schedule</span>
            </div>
          </div>

          <div className="bg-white p-5 rounded-2xl border border-slate-200 shadow-xs flex items-center justify-between">
            <div className="flex flex-col">
              <span className="text-xs font-semibold text-slate-500 uppercase tracking-wider">Lượt đỗ EV đã sạc</span>
              <span className="text-2xl font-extrabold text-emerald-600 mt-1">12</span>
              <span className="text-[11px] text-emerald-600 font-medium mt-0.5">380 kWh điện tái tạo</span>
            </div>
            <div className="w-12 h-12 rounded-xl bg-emerald-50 text-emerald-600 flex items-center justify-center shrink-0">
              <span className="material-symbols-outlined text-[24px]">bolt</span>
            </div>
          </div>

          <div className="bg-white p-5 rounded-2xl border border-slate-200 shadow-xs flex items-center justify-between">
            <div className="flex flex-col">
              <span className="text-xs font-semibold text-slate-500 uppercase tracking-wider">Đã hủy / Hoàn tiền</span>
              <span className="text-2xl font-extrabold text-slate-700 mt-1">
                {bookings.filter((b) => b.status === 'cancelled').length}
              </span>
              <span className="text-[11px] text-slate-500 font-medium mt-0.5">Hoàn tiền 100% tự động</span>
            </div>
            <div className="w-12 h-12 rounded-xl bg-slate-100 text-slate-600 flex items-center justify-center shrink-0">
              <span className="material-symbols-outlined text-[24px]">published_with_changes</span>
            </div>
          </div>
        </div>

        {/* 4. Filter Toolbar */}
        <div className="bg-white p-4 rounded-2xl border border-slate-200 shadow-xs flex flex-col md:flex-row md:items-center justify-between gap-4">
          {/* Status Tabs */}
          <div className="flex items-center gap-1.5 overflow-x-auto pb-1 md:pb-0">
            <button
              type="button"
              onClick={() => setActiveTab('all')}
              className={`px-4 py-2 rounded-xl text-xs font-bold transition-all cursor-pointer whitespace-nowrap ${
                activeTab === 'all' ? 'bg-blue-600 text-white shadow-xs' : 'text-slate-600 hover:text-slate-900 bg-slate-50'
              }`}
            >
              Tất cả ({bookings.length})
            </button>
            <button
              type="button"
              onClick={() => setActiveTab('upcoming')}
              className={`px-4 py-2 rounded-xl text-xs font-bold transition-all cursor-pointer whitespace-nowrap flex items-center gap-1.5 ${
                activeTab === 'upcoming' ? 'bg-blue-600 text-white shadow-xs' : 'text-slate-600 hover:text-slate-900 bg-slate-50'
              }`}
            >
              <span className="w-2 h-2 rounded-full bg-blue-400"></span>
              <span>Sắp diễn ra ({bookings.filter((b) => b.status === 'upcoming').length})</span>
            </button>
            <button
              type="button"
              onClick={() => setActiveTab('completed')}
              className={`px-4 py-2 rounded-xl text-xs font-bold transition-all cursor-pointer whitespace-nowrap ${
                activeTab === 'completed' ? 'bg-blue-600 text-white shadow-xs' : 'text-slate-600 hover:text-slate-900 bg-slate-50'
              }`}
            >
              Đã hoàn thành ({bookings.filter((b) => b.status === 'completed').length})
            </button>
            <button
              type="button"
              onClick={() => setActiveTab('cancelled')}
              className={`px-4 py-2 rounded-xl text-xs font-bold transition-all cursor-pointer whitespace-nowrap flex items-center gap-1 ${
                activeTab === 'cancelled' ? 'bg-rose-600 text-white shadow-xs' : 'text-slate-600 hover:text-slate-900 bg-slate-50'
              }`}
            >
              <span className="material-symbols-outlined text-[14px]">cancel</span>
              <span>Đã hủy ({bookings.filter((b) => b.status === 'cancelled').length})</span>
            </button>
          </div>

          {/* Search Box */}
          <div className="relative w-full md:w-72">
            <span className="material-symbols-outlined absolute left-3 top-1/2 -translate-y-1/2 text-slate-400 text-[18px]">
              search
            </span>
            <input
              type="text"
              value={searchQuery}
              onChange={(e) => setSearchQuery(e.target.value)}
              placeholder="Tìm theo mã vé, tên bãi hoặc biển số..."
              className="w-full pl-9 pr-4 py-2 rounded-xl border border-slate-200 text-xs text-slate-800 placeholder:text-slate-400 focus:outline-hidden focus:ring-2 focus:ring-blue-600/20 focus:border-blue-600 transition-all bg-slate-50/50"
            />
          </div>
        </div>

        {/* 5. Booking Table */}
        <div className="bg-white rounded-2xl border border-slate-200 shadow-xs overflow-hidden">
          <div className="overflow-x-auto">
            <table className="w-full text-left border-collapse">
              <thead>
                <tr className="bg-slate-50/80 border-b border-slate-200 text-[11px] font-bold text-slate-500 uppercase tracking-wider">
                  <th className="py-3 px-3 sm:px-4">Mã đặt chỗ & Xe</th>
                  <th className="py-3 px-3 sm:px-4">Bãi đỗ & Vị trí</th>
                  <th className="py-3 px-3 sm:px-4">Thời gian</th>
                  <th className="py-3 px-3 sm:px-4">Dịch vụ</th>
                  <th className="py-3 px-2 sm:px-3">Tổng tiền</th>
                  <th className="py-3 px-2 sm:px-3">Trạng thái</th>
                  <th className="py-3 px-3 sm:px-4 text-right">Thao tác</th>
                </tr>
              </thead>
              <tbody className="divide-y divide-slate-100 text-xs">
                {filteredBookings.length === 0 ? (
                  <tr>
                    <td colSpan={7} className="py-12 text-center text-slate-400 text-xs">
                      Không tìm thấy lượt đặt chỗ nào phù hợp.
                    </td>
                  </tr>
                ) : (
                  filteredBookings.map((b) => {
                    const isUpcoming = b.status === 'upcoming';
                    const isCancelled = b.status === 'cancelled';

                    return (
                      <tr
                        key={b.id}
                        id={`booking-row-${b.id}`}
                        className={`transition-colors group ${
                          highlightId === b.id
                            ? 'bg-blue-50 ring-2 ring-inset ring-blue-500'
                            : `hover:bg-slate-50/80 ${isUpcoming ? 'bg-blue-50/30' : ''}`
                        }`}
                      >
                        <td className="py-3 px-3 sm:px-4 relative">
                          {isUpcoming && <div className="absolute left-0 top-0 bottom-0 w-1 bg-blue-600"></div>}
                          <div className="flex flex-col gap-1">
                            <div className="flex items-center gap-1.5 flex-wrap">
                              <span className={`font-bold font-mono text-xs ${isUpcoming ? 'text-blue-600' : isCancelled ? 'text-slate-400 line-through' : 'text-slate-800'}`}>
                                {b.code}
                              </span>
                              {isUpcoming && (
                                <span className="inline-flex items-center gap-0.5 px-1.5 py-0.5 rounded bg-blue-100 text-blue-700 text-[10px] font-bold">
                                  <span className="material-symbols-outlined text-[12px]">camera</span>
                                  ANPR
                                </span>
                              )}
                            </div>
                            {b.id === '6' && isUpcoming && (
                              <div className="inline-flex items-center gap-1 px-1.5 py-0.5 rounded bg-amber-50 text-amber-800 text-[10px] font-bold border border-amber-200 w-fit">
                                <span className="material-symbols-outlined text-[12px] text-amber-600">timer</span>
                                <span>Cận giờ &lt; 1h</span>
                              </div>
                            )}
                            <div className="flex items-center gap-1 text-slate-500 text-[11px]">
                              <span className="material-symbols-outlined text-[13px] text-slate-400">directions_car</span>
                              <span className="font-semibold text-slate-800">{b.plate}</span>
                              <span className="text-slate-300">·</span>
                              <span className="text-slate-600 truncate max-w-[110px]">{b.carModel}</span>
                            </div>
                          </div>
                        </td>

                        <td className="py-3 px-3 sm:px-4">
                          <div className="flex flex-col gap-0.5 min-w-0">
                            <span className="font-bold text-slate-900 truncate">{b.location}</span>
                            <span className="text-[11px] text-slate-500 truncate">{b.spot}</span>
                          </div>
                        </td>

                        <td className="py-3 px-3 sm:px-4">
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

                        <td className="py-3 px-3 sm:px-4">
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

                        <td className="py-3 px-3 sm:px-4">
                          <div className="flex flex-col gap-0.5">
                            <span className="font-bold text-slate-900">
                              {b.totalPrice > 0 ? `${b.totalPrice.toLocaleString('vi-VN')} đ` : '0 đ'}
                            </span>
                            <span className="text-[11px] text-slate-500">{b.paymentMethod}</span>
                          </div>
                        </td>

                        <td className="py-3 px-3 sm:px-4">
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
                            <span className="inline-flex items-center gap-1.5 px-3 py-1 rounded-full bg-rose-100 text-rose-700 text-[11px] font-bold">
                              <span className="w-1.5 h-1.5 rounded-full bg-rose-600"></span>
                              {b.statusText}
                            </span>
                          )}
                        </td>

                        <td className="py-3 px-3 sm:px-4 text-right">
                          <div className="inline-flex items-center gap-2 justify-end">
                            {isUpcoming ? (
                              <>
                                <button
                                  type="button"
                                  onClick={() => setActiveQrBooking(b)}
                                  className="inline-flex items-center gap-1 px-3 py-1.5 rounded-lg bg-blue-600 text-white hover:bg-blue-700 text-xs font-bold shadow-xs transition-all cursor-pointer"
                                >
                                  <span className="material-symbols-outlined text-[16px]">qr_code_2</span>
                                  <span>Vé QR</span>
                                </button>
                                <button
                                  type="button"
                                  onClick={() => handleOpenCancelModal(b)}
                                  title="Hủy giữ chỗ (Chính sách 1 giờ)"
                                  className="p-1.5 rounded-lg text-slate-400 hover:text-rose-600 hover:bg-rose-50 transition-colors cursor-pointer"
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
                                    className="inline-flex items-center gap-1 px-2.5 py-1.5 rounded-lg border border-amber-300 bg-amber-50 hover:bg-amber-100 text-amber-800 font-bold text-xs transition-all shadow-2xs cursor-pointer"
                                  >
                                    <span className="material-symbols-outlined text-[16px] text-amber-500">star</span>
                                    <span>Đánh giá</span>
                                  </button>
                                )}
                                <button
                                  type="button"
                                  onClick={() => setActiveQrBooking(b)}
                                  className="px-3 py-1.5 rounded-lg bg-slate-100 hover:bg-slate-200 text-slate-700 font-medium text-xs transition-colors cursor-pointer"
                                >
                                  Chi tiết
                                </button>
                              </>
                            ) : (
                              <>
                                <button
                                  type="button"
                                  onClick={() => setActiveRefundDetailBooking(b)}
                                  className="px-3 py-1.5 rounded-lg bg-slate-100 hover:bg-slate-200 text-slate-700 text-xs font-semibold cursor-pointer transition-colors"
                                >
                                  Chi tiết hoàn
                                </button>
                                <button
                                  type="button"
                                  onClick={() => navigate('/driver/parking-lots')}
                                  className="px-3 py-1.5 rounded-lg text-blue-600 hover:bg-blue-50 font-bold text-xs transition-colors cursor-pointer"
                                >
                                  Đặt lại
                                </button>
                              </>
                            )}
                          </div>
                        </td>
                      </tr>
                    );
                  })
                )}
              </tbody>
            </table>
          </div>
        </div>
      </main>

      {/* 6. UI-10: CANCELLATION & REFUND MODAL */}
      {activeCancelBooking && (
        <div className="fixed inset-0 bg-slate-900/60 backdrop-blur-xs z-50 flex items-center justify-center p-4 animate-in fade-in">
          <div className="bg-white rounded-3xl shadow-2xl p-6 sm:p-8 w-full max-w-xl border border-slate-200 max-h-[94vh] overflow-y-auto flex flex-col gap-6">
            {/* Header */}
            <div className="flex items-start justify-between gap-4 pb-4 border-b border-slate-100">
              <div className="flex items-center gap-3">
                <div className="w-12 h-12 rounded-2xl bg-rose-50 text-rose-600 flex items-center justify-center shrink-0 border border-rose-100">
                  <span className="material-symbols-outlined text-[26px]">event_busy</span>
                </div>
                <div>
                  <div className="inline-flex items-center gap-1.5 px-2.5 py-0.5 rounded-full bg-rose-100 text-rose-800 text-[10px] font-bold uppercase tracking-wider mb-1">
                    CHÍNH SÁCH HỦY GIỮ CHỖ 1 GIỜ (UI-10)
                  </div>
                  <h3 className="font-extrabold text-lg text-slate-900 leading-tight">
                    Xác nhận hủy đặt chỗ
                  </h3>
                  <p className="text-xs text-slate-500 mt-0.5">
                    Mã vé: <span className="font-bold text-slate-800">{activeCancelBooking.code}</span> • {activeCancelBooking.location}
                  </p>
                </div>
              </div>

              <button
                type="button"
                onClick={() => setActiveCancelBooking(null)}
                className="text-slate-400 hover:text-slate-700 p-1.5 rounded-xl hover:bg-slate-100 transition-colors cursor-pointer"
              >
                <span className="material-symbols-outlined text-[22px]">close</span>
              </button>
            </div>

            {/* Test Scenario Switcher */}
            <div className="p-3 bg-slate-100 rounded-2xl border border-slate-200 flex flex-col gap-2">
              <div className="text-[11px] font-bold text-slate-600 uppercase tracking-wider flex items-center justify-between">
                <span>Mô phỏng thời điểm hủy (Demo Test):</span>
                <span className="text-[10px] text-blue-600 font-semibold">Tự động theo giờ hoặc chọn thủ công</span>
              </div>
              <div className="grid grid-cols-2 gap-2">
                <button
                  type="button"
                  onClick={() => setCancelPolicyScenario('before_1h')}
                  className={`px-3 py-2 rounded-xl text-xs font-bold transition-all cursor-pointer text-left border ${
                    isFullRefund
                      ? 'bg-emerald-600 text-white border-emerald-600 shadow-xs'
                      : 'bg-white text-slate-700 border-slate-200 hover:bg-slate-50'
                  }`}
                >
                  <div className="flex items-center gap-1">
                    <span className="material-symbols-outlined text-[16px]">check_circle</span>
                    <span>Trước hạn ≥ 1 Giờ</span>
                  </div>
                  <div className="text-[10px] opacity-90 font-normal mt-0.5">Hoàn 100% (Miễn phí hủy)</div>
                </button>

                <button
                  type="button"
                  onClick={() => setCancelPolicyScenario('after_1h')}
                  className={`px-3 py-2 rounded-xl text-xs font-bold transition-all cursor-pointer text-left border ${
                    !isFullRefund
                      ? 'bg-amber-600 text-white border-amber-600 shadow-xs'
                      : 'bg-white text-slate-700 border-slate-200 hover:bg-slate-50'
                  }`}
                >
                  <div className="flex items-center gap-1">
                    <span className="material-symbols-outlined text-[16px]">warning</span>
                    <span>Cận giờ &lt; 1 Giờ</span>
                  </div>
                  <div className="text-[10px] opacity-90 font-normal mt-0.5">Phí 30% • Hoàn 70%</div>
                </button>
              </div>
            </div>

            {/* 1-Hour Policy Timeline Indicator */}
            <div className={`p-4 rounded-2xl border flex flex-col gap-3 ${
              isFullRefund ? 'bg-emerald-50/70 border-emerald-200' : 'bg-amber-50/70 border-amber-200'
            }`}>
              <div className="flex items-center justify-between">
                <span className={`text-xs font-bold uppercase tracking-wider inline-flex items-center gap-1.5 ${
                  isFullRefund ? 'text-emerald-800' : 'text-amber-800'
                }`}>
                  <span className={`w-2 h-2 rounded-full ${isFullRefund ? 'bg-emerald-500' : 'bg-amber-500'}`}></span>
                  {isFullRefund ? 'Đủ điều kiện hoàn tiền 100%' : 'Hủy cận giờ (< 1 giờ trước giờ đỗ)'}
                </span>
                <span className={`text-xs font-bold px-2 py-0.5 rounded-full ${
                  isFullRefund ? 'bg-emerald-100 text-emerald-800' : 'bg-amber-100 text-amber-800'
                }`}>
                  {isFullRefund ? 'Hoàn 100%' : 'Hoàn 70%'}
                </span>
              </div>

              {/* Graphical Timeline Bar */}
              <div className="flex flex-col gap-1.5 py-1">
                <div className="h-2 w-full bg-slate-200 rounded-full overflow-hidden flex">
                  <div className="w-1/2 bg-emerald-500 h-full" title="Vùng hủy miễn phí 100%"></div>
                  <div className="w-1/2 bg-amber-400 h-full" title="Vùng cận giờ phí 30%"></div>
                </div>
                <div className="flex items-center justify-between text-[10px] text-slate-500 font-medium">
                  <span>Đặt thành công</span>
                  <span className="font-bold text-slate-700">Mốc 1 giờ trước giờ đỗ</span>
                  <span>Bắt đầu ca đỗ</span>
                </div>
              </div>

              <p className={`text-xs leading-relaxed ${isFullRefund ? 'text-emerald-900' : 'text-amber-900'}`}>
                {isFullRefund
                  ? 'Bạn đang yêu cầu hủy trước giờ đỗ hơn 1 tiếng. Theo chính sách cam kết của ParkMaster, toàn bộ số tiền thanh toán sẽ được hoàn trả đầy đủ không mất phí.'
                  : 'Thời điểm hiện tại đã dưới 1 giờ so với thời gian bắt đầu ca đỗ. Bãi đỗ xe đã chuẩn bị vị trí slot và kích hoạt hệ thống barie, do đó áp dụng phí giữ chỗ 30%.'}
              </p>
            </div>

            {/* Financial Breakdown Card */}
            <div className="rounded-2xl border border-slate-200 divide-y divide-slate-100 overflow-hidden text-xs bg-slate-50/50">
              <div className="flex justify-between items-center p-3.5">
                <span className="text-slate-500">Số tiền đã thanh toán:</span>
                <span className="font-bold text-slate-900">{originalPrice.toLocaleString('vi-VN')} đ</span>
              </div>
              <div className="flex justify-between items-center p-3.5">
                <span className="text-slate-500">Phí hủy cận giờ (nếu &lt; 1h):</span>
                <span className={`font-bold ${penaltyFee > 0 ? 'text-rose-600' : 'text-slate-500'}`}>
                  {penaltyFee > 0 ? `-${penaltyFee.toLocaleString('vi-VN')} đ (30%)` : '0 đ (Miễn phí)'}
                </span>
              </div>
              <div className="flex justify-between items-center p-4 bg-emerald-50/80">
                <div className="flex flex-col">
                  <span className="font-bold text-emerald-900 text-xs">Số tiền thực nhận hoàn lại:</span>
                  <span className="text-[10px] text-emerald-700">Tự động đối soát và cộng vào tài khoản</span>
                </div>
                <span className="text-xl font-extrabold text-emerald-600">
                  {refundAmount.toLocaleString('vi-VN')} đ
                </span>
              </div>
            </div>

            {/* Refund Method Selector */}
            <div className="flex flex-col gap-2">
              <label className="text-xs font-bold text-slate-700 uppercase tracking-wider">
                Phương thức nhận tiền hoàn trả:
              </label>
              <div className="grid grid-cols-1 sm:grid-cols-2 gap-3">
                <label
                  onClick={() => setRefundMethod('wallet')}
                  className={`p-3.5 rounded-2xl border-2 flex items-start gap-3 cursor-pointer transition-all ${
                    refundMethod === 'wallet'
                      ? 'border-blue-600 bg-blue-50/50 shadow-xs'
                      : 'border-slate-200 hover:border-slate-300 bg-white'
                  }`}
                >
                  <input
                    type="radio"
                    name="refundMethod"
                    checked={refundMethod === 'wallet'}
                    onChange={() => setRefundMethod('wallet')}
                    className="mt-1 text-blue-600"
                  />
                  <div className="flex flex-col">
                    <span className="text-xs font-bold text-slate-900 flex items-center gap-1.5">
                      <span>Ví ParkMaster</span>
                      <span className="px-1.5 py-0.2 rounded bg-emerald-100 text-emerald-800 text-[10px] font-extrabold">
                        Nhận ngay
                      </span>
                    </span>
                    <span className="text-[11px] text-slate-500 mt-0.5">
                      Cộng tiền tức thì trong 30 giây + Voucher 5%
                    </span>
                  </div>
                </label>

                <label
                  onClick={() => setRefundMethod('original')}
                  className={`p-3.5 rounded-2xl border-2 flex items-start gap-3 cursor-pointer transition-all ${
                    refundMethod === 'original'
                      ? 'border-blue-600 bg-blue-50/50 shadow-xs'
                      : 'border-slate-200 hover:border-slate-300 bg-white'
                  }`}
                >
                  <input
                    type="radio"
                    name="refundMethod"
                    checked={refundMethod === 'original'}
                    onChange={() => setRefundMethod('original')}
                    className="mt-1 text-blue-600"
                  />
                  <div className="flex flex-col">
                    <span className="text-xs font-bold text-slate-900">Nguồn tiền thanh toán gốc</span>
                    <span className="text-[11px] text-slate-500 mt-0.5">
                      Hoàn về Ví MoMo / Thẻ Visa (1-3 ngày làm việc)
                    </span>
                  </div>
                </label>
              </div>
            </div>

            {/* Cancellation Reason Radio List */}
            <div className="flex flex-col gap-2">
              <label className="text-xs font-bold text-slate-700 uppercase tracking-wider">
                Lý do hủy giữ chỗ:
              </label>
              <div className="grid grid-cols-1 gap-2 text-xs">
                {[
                  { id: 'schedule_change', label: 'Thay đổi lịch trình / Không còn nhu cầu đỗ xe' },
                  { id: 'found_other', label: 'Tìm thấy bãi khác tiện đường hoặc rẻ hơn' },
                  { id: 'vehicle_issue', label: 'Phương tiện gặp sự cố kỹ thuật đột xuất' },
                  { id: 'other', label: 'Lý do khác' },
                ].map((item) => (
                  <label
                    key={item.id}
                    className={`p-2.5 rounded-xl border flex items-center gap-3 cursor-pointer transition-all ${
                      cancelReason === item.id ? 'border-blue-500 bg-blue-50/40 text-blue-900 font-semibold' : 'border-slate-200 text-slate-700 hover:bg-slate-50'
                    }`}
                  >
                    <input
                      type="radio"
                      name="cancelReason"
                      value={item.id}
                      checked={cancelReason === item.id}
                      onChange={(e) => setCancelReason(e.target.value)}
                      className="text-blue-600"
                    />
                    <span>{item.label}</span>
                  </label>
                ))}

                {cancelReason === 'other' && (
                  <input
                    type="text"
                    value={customReason}
                    onChange={(e) => setCustomReason(e.target.value)}
                    placeholder="Nhập lý do cụ thể của bạn..."
                    className="mt-1 px-3.5 py-2 rounded-xl border border-slate-200 text-xs focus:ring-2 focus:ring-blue-600/20 focus:border-blue-600"
                  />
                )}
              </div>
            </div>

            {/* Actions */}
            <div className="flex items-center justify-end gap-3 pt-4 border-t border-slate-100">
              <button
                type="button"
                onClick={() => setActiveCancelBooking(null)}
                className="px-5 py-2.5 rounded-xl border border-slate-200 text-slate-700 hover:bg-slate-100 text-xs font-bold transition-colors cursor-pointer"
              >
                Giữ lại lượt đỗ
              </button>
              <button
                type="button"
                disabled={isCancelling}
                onClick={handleConfirmCancel}
                className="px-6 py-2.5 rounded-xl bg-rose-600 hover:bg-rose-700 disabled:bg-rose-400 text-white font-bold text-xs shadow-sm hover:shadow transition-all flex items-center gap-2 cursor-pointer"
              >
                {isCancelling ? (
                  <>
                    <span className="material-symbols-outlined text-[18px] animate-spin">refresh</span>
                    <span>Đang xử lý hủy & hoàn tiền…</span>
                  </>
                ) : (
                  <>
                    <span className="material-symbols-outlined text-[18px]">check_circle</span>
                    <span>Xác nhận hủy chỗ đỗ</span>
                  </>
                )}
              </button>
            </div>
          </div>
        </div>
      )}

      {/* 7. REFUND DETAIL MODAL (Xem chi tiết khi đã hủy) */}
      {activeRefundDetailBooking && (
        <div className="fixed inset-0 bg-slate-900/60 backdrop-blur-xs z-50 flex items-center justify-center p-4 animate-in fade-in">
          <div className="bg-white rounded-3xl shadow-2xl p-6 sm:p-7 w-full max-w-md border border-slate-200 flex flex-col gap-5">
            <div className="flex items-center justify-between pb-3 border-b border-slate-100">
              <div className="flex items-center gap-2.5">
                <div className="w-10 h-10 rounded-xl bg-emerald-50 text-emerald-600 flex items-center justify-center">
                  <span className="material-symbols-outlined text-[24px]">receipt_long</span>
                </div>
                <div>
                  <h3 className="font-bold text-base text-slate-900">Biên nhận hoàn tiền</h3>
                  <p className="text-xs text-slate-500">{activeRefundDetailBooking.code}</p>
                </div>
              </div>
              <button
                type="button"
                onClick={() => setActiveRefundDetailBooking(null)}
                className="text-slate-400 hover:text-slate-700 p-1"
              >
                <span className="material-symbols-outlined text-[20px]">close</span>
              </button>
            </div>

            <div className="rounded-2xl bg-emerald-50/70 border border-emerald-100 p-4 text-center flex flex-col items-center">
              <span className="text-[11px] font-bold text-emerald-800 uppercase tracking-wider">Trạng thái giao dịch</span>
              <span className="text-lg font-extrabold text-emerald-600 mt-0.5">Hoàn tiền thành công</span>
              <p className="text-xs text-emerald-700 mt-1">{activeRefundDetailBooking.paymentMethod}</p>
            </div>

            <div className="rounded-xl border border-slate-200 divide-y divide-slate-100 text-xs">
              <div className="flex justify-between p-3">
                <span className="text-slate-500">Bãi đỗ:</span>
                <span className="font-bold text-slate-800">{activeRefundDetailBooking.location}</span>
              </div>
              <div className="flex justify-between p-3">
                <span className="text-slate-500">Phương tiện:</span>
                <span className="font-medium text-slate-800">{activeRefundDetailBooking.plate}</span>
              </div>
              <div className="flex justify-between p-3">
                <span className="text-slate-500">Thời gian & Lý do:</span>
                <span className="font-medium text-slate-800 text-right">{activeRefundDetailBooking.timeRange}</span>
              </div>
              <div className="flex justify-between p-3 bg-slate-50">
                <span className="text-slate-500">Mã giao dịch đối soát:</span>
                <span className="font-mono font-bold text-slate-700">REF-{Math.floor(100000 + Math.random() * 900000)}</span>
              </div>
            </div>

            <button
              type="button"
              onClick={() => setActiveRefundDetailBooking(null)}
              className="w-full py-2.5 rounded-xl bg-slate-900 text-white font-bold text-xs hover:bg-slate-800 transition-colors"
            >
              Đóng biên nhận
            </button>
          </div>
        </div>
      )}

      {/* 8. Interactive Feedback Modal */}
      {activeFeedbackBooking && (
        <div className="fixed inset-0 bg-slate-900/60 backdrop-blur-xs z-50 flex items-center justify-center p-4 animate-in fade-in">
          <div className="bg-white rounded-2xl shadow-2xl p-6 sm:p-7 w-full max-w-lg border border-slate-200 max-h-[92vh] overflow-y-auto">
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
                className="px-5 py-2.5 rounded-xl bg-blue-600 hover:bg-blue-700 text-white font-bold text-xs shadow-xs hover:shadow transition-all flex items-center justify-center gap-2 cursor-pointer"
              >
                <span className="material-symbols-outlined text-[16px]">send</span>
                <span>Gửi đánh giá</span>
              </button>
            </div>
          </div>
        </div>
      )}

      {/* 9. QR Code Entrance Modal */}
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

            <div className="p-4 bg-white rounded-2xl border-2 border-slate-900 shadow-inner flex flex-col items-center mb-4">
              <div className="w-48 h-48 bg-slate-900 flex items-center justify-center rounded-xl p-2 relative">
                <svg className="w-full h-full text-white" viewBox="0 0 100 100" fill="currentColor">
                  <rect x="5" y="5" width="25" height="25" rx="3" />
                  <rect x="10" y="10" width="15" height="15" fill="#0f172a" />
                  <rect x="13" y="13" width="9" height="9" />
                  <rect x="70" y="5" width="25" height="25" rx="3" />
                  <rect x="75" y="10" width="15" height="15" fill="#0f172a" />
                  <rect x="78" y="13" width="9" height="9" />
                  <rect x="5" y="70" width="25" height="25" rx="3" />
                  <rect x="10" y="75" width="15" height="15" fill="#0f172a" />
                  <rect x="13" y="78" width="9" height="9" />
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

      {/* 10. Cancellation Toast */}
      {cancelToast && (
        <div className="fixed bottom-6 right-6 z-50 animate-in fade-in slide-in-from-bottom-5">
          <div className="bg-slate-900 text-white px-5 py-4 rounded-2xl shadow-2xl flex items-center gap-3.5 border border-slate-700 max-w-md">
            <div className="w-8 h-8 rounded-full bg-emerald-500/20 text-emerald-400 flex items-center justify-center shrink-0">
              <span className="material-symbols-outlined text-[20px]">check_circle</span>
            </div>
            <div className="flex flex-col">
              <p className="font-bold text-xs text-white">{cancelToast.title}</p>
              <p className="text-[11px] text-slate-300 leading-relaxed mt-0.5">{cancelToast.message}</p>
            </div>
          </div>
        </div>
      )}

      {/* 11. Feedback Toast */}
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
