import React, { useEffect, useState } from 'react';
import { useNavigate, useSearchParams } from 'react-router-dom';
import { useAppSelector } from '@/store';
import { BookingTableRow, type BookingItem } from '../components/BookingTableRow';

const DEFAULT_BOOKINGS: BookingItem[] = [
  {
    id: '1',
    code: '#PKM-2026-8842',
    plate: '51K - 889.32',
    carModel: 'Mercedes-Benz EQE SUV',
    location: 'Landmark 81 Central Garage',
    spot: 'Hầm B2 · Ô B-04 (EV 120kW)',
    timeRange: '15:00 - 18:00',
    dateLabel: 'Hôm nay, 09/11/2026',
    services: ['Sạc EV siêu nhanh 120kW', 'Bảo hiểm trầy xước'],
    totalPrice: 156000,
    paymentMethod: 'Ví ParkMaster',
    status: 'upcoming',
    statusText: 'Sắp diễn ra',
    notes: 'Ưu tiên sạc pin đầy trước 17:30',
  },
  {
    id: '2',
    code: '#PKM-2026-7219',
    plate: '51K - 889.32',
    carModel: 'Mercedes-Benz EQE SUV',
    location: 'Bitexco Financial Tower',
    spot: 'Hầm B1 · Ô A-12',
    timeRange: '09:00 - 12:30',
    dateLabel: 'Hôm qua, 08/11/2026',
    services: ['Sạc EV tiêu chuẩn', 'Rửa xe bọt tuyết'],
    totalPrice: 185000,
    paymentMethod: 'Thanh toán VNPAY-QR',
    status: 'completed',
    statusText: 'Đã hoàn thành',
    rating: 5,
    reviewed: true,
  },
  {
    id: '3',
    code: '#PKM-2026-6105',
    plate: '51K - 889.32',
    carModel: 'Mercedes-Benz EQE SUV',
    location: 'Vincom Center Đồng Khởi',
    spot: 'Hầm B3 · Ô C-08',
    timeRange: 'Hủy lúc 08:15 · Thay đổi lịch trình',
    dateLabel: '06/11/2026',
    services: ['Đỗ xe tiêu chuẩn'],
    totalPrice: 0,
    paymentMethod: 'Đã hoàn 100% về Ví ParkMaster',
    status: 'cancelled',
    statusText: 'Đã hủy (Hoàn 100%)',
  },
];

const loadSavedBookings = (): BookingItem[] => {
  try {
    const raw = localStorage.getItem('parkmaster_driver_bookings');
    if (raw) {
      const parsed = JSON.parse(raw);
      if (Array.isArray(parsed) && parsed.length > 0) return parsed;
    }
  } catch {
    // fallback
  }
  return DEFAULT_BOOKINGS;
};

const RATING_DESCRIPTIONS: Record<number, string> = {
  1: 'Rất không hài lòng (1 sao)',
  2: 'Chưa hài lòng (2 sao)',
  3: 'Tạm ổn (3 sao)',
  4: 'Hài lòng (4 sao)',
  5: 'Rất hài lòng (5 sao)',
};

export const BookingHistoryPage: React.FC = () => {
  const navigate = useNavigate();
  const { vehicles } = useAppSelector((state) => state.vehicle);
  const { user } = useAppSelector((state) => state.auth);

  const [bookings, setBookings] = useState<BookingItem[]>(loadSavedBookings);
  const [activeTab, setActiveTab] = useState<'all' | 'upcoming' | 'completed' | 'cancelled'>('all');
  const [searchQuery, setSearchQuery] = useState('');

  // Đến từ thông báo (/driver/history?booking=<id|code>) để highlight
  const [searchParams] = useSearchParams();
  const highlightParam = searchParams.get('booking');
  const highlightId = highlightParam
    ? (bookings.find((b) => b.id === highlightParam || b.code === highlightParam)?.id ?? null)
    : null;

  useEffect(() => {
    if (!highlightId) return;
    setActiveTab('all');
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
  const [selectedTags, setSelectedTags] = useState<string[]>(['🎯 Vị trí dễ tìm']);
  const [commentText, setCommentText] = useState<string>('');
  const [showToast, setShowToast] = useState<{ title: string; message: string } | null>(null);

  // QR Modal
  const [activeQrBooking, setActiveQrBooking] = useState<BookingItem | null>(null);

  // UI-10: Cancellation & Refund State
  const [activeCancelBooking, setActiveCancelBooking] = useState<BookingItem | null>(null);
  const [activeRefundDetailBooking, setActiveRefundDetailBooking] = useState<BookingItem | null>(null);
  const [cancelReason, setCancelReason] = useState<string>('schedule_change');
  const [customReason, setCustomReason] = useState<string>('');
  const [refundMethod, setRefundMethod] = useState<'wallet' | 'original'>('wallet');
  const [isCancelling, setIsCancelling] = useState<boolean>(false);
  const [cancelToast, setCancelToast] = useState<{ title: string; message: string; refundAmount: number } | null>(null);

  // UI-11: Sửa booking & Gia hạn giờ đỗ
  const [activeExtendBooking, setActiveExtendBooking] = useState<BookingItem | null>(null);
  const [extendHours, setExtendHours] = useState<number>(1);
  const [extendPaymentMethod, setExtendPaymentMethod] = useState<'wallet' | 'vnpay' | 'gate'>('wallet');
  const [isSubmittingExtend, setIsSubmittingExtend] = useState<boolean>(false);

  const [activeEditBooking, setActiveEditBooking] = useState<BookingItem | null>(null);
  const [editPlate, setEditPlate] = useState<string>('');
  const [editTimeShift, setEditTimeShift] = useState<string>('0');
  const [editNotes, setEditNotes] = useState<string>('');
  const [isSubmittingEdit, setIsSubmittingEdit] = useState<boolean>(false);

  // UI-13: Hóa đơn điện tử VAT (E-Invoice)
  const [activeInvoiceBooking, setActiveInvoiceBooking] = useState<BookingItem | null>(null);

  const availableTags = [
    '🎯 Vị trí dễ tìm',
    '⚡ Sạc EV nhanh',
    '🛡️ Bảo vệ nhiệt tình',
    '🚗 Ra vào nhanh chóng',
    '🔒 An ninh đảm bảo',
  ];

  const handleOpenFeedback = (booking: BookingItem) => {
    setActiveFeedbackBooking(booking);
    setRatingValue(booking.rating || 5);
    setCommentText('');
    setSelectedTags(['🎯 Vị trí dễ tìm']);
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
    const updated = bookings.map((b) =>
      b.id === activeFeedbackBooking.id
        ? { ...b, reviewed: true, rating: ratingValue }
        : b
    );
    setBookings(updated);
    try {
      localStorage.setItem('parkmaster_driver_bookings', JSON.stringify(updated));
    } catch (e) {
      console.error('Failed to save feedback', e);
    }
    setActiveFeedbackBooking(null);
    setShowToast({
      title: 'Đã ghi nhận đánh giá!',
      message: 'Cảm ơn bạn đã đóng góp phản hồi giúp ParkMaster nâng cao chất lượng phục vụ.',
    });
    setTimeout(() => setShowToast(null), 3500);
  };

  // UI-10: Cancellation Calculation
  const isFullRefund = true;
  const originalPrice = activeCancelBooking?.totalPrice || 0;
  const penaltyRate = isFullRefund ? 0 : 0.3;
  const penaltyFee = Math.round(originalPrice * penaltyRate);
  const refundAmount = originalPrice - penaltyFee;

  const handleOpenCancelModal = (booking: BookingItem) => {
    setActiveCancelBooking(booking);
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

      const updated = bookings.map((b) =>
        b.id === activeCancelBooking.id
          ? {
              ...b,
              status: 'cancelled' as const,
              statusText: updatedStatusText,
              totalPrice: penaltyFee,
              paymentMethod: updatedRefundNote,
              timeRange: `Hủy lúc ${new Date().toLocaleTimeString('vi-VN', { hour: '2-digit', minute: '2-digit' })} · ${reasonLabel}`,
            }
          : b
      );

      setBookings(updated);
      try {
        localStorage.setItem('parkmaster_driver_bookings', JSON.stringify(updated));
      } catch (err) {
        console.error('Failed to save bookings', err);
      }

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
    }, 700);
  };

  // UI-11: Xử lý Gia hạn giờ đỗ
  const handleOpenExtendModal = (b: BookingItem) => {
    setActiveExtendBooking(b);
    setExtendHours(1);
    setExtendPaymentMethod('wallet');
  };

  const handleConfirmExtend = () => {
    if (!activeExtendBooking) return;
    setIsSubmittingExtend(true);

    setTimeout(() => {
      const hourlyRate = 30000;
      const extraFee = extendHours * hourlyRate;
      const [startStr, endStr] = activeExtendBooking.timeRange.split(' - ');
      let newEndTime = '19:00';
      if (endStr) {
        const parts = endStr.split(':');
        const h = parseInt(parts[0], 10) || 18;
        const m = parts[1] || '00';
        newEndTime = `${String(h + Math.ceil(extendHours)).padStart(2, '0')}:${m}`;
      }
      const newTimeRange = `${startStr || '15:00'} - ${newEndTime}`;

      const updated = bookings.map((b) =>
        b.id === activeExtendBooking.id
          ? {
              ...b,
              timeRange: newTimeRange,
              totalPrice: b.totalPrice + extraFee,
              services: [...b.services, `Gia hạn thêm +${extendHours}h`],
            }
          : b
      );

      setBookings(updated);
      try {
        localStorage.setItem('parkmaster_driver_bookings', JSON.stringify(updated));
      } catch (err) {
        console.error(err);
      }

      setIsSubmittingExtend(false);
      setActiveExtendBooking(null);
      setShowToast({
        title: 'Gia hạn giờ đỗ thành công!',
        message: `Đã gia hạn thêm ${extendHours} giờ cho vé ${activeExtendBooking.code}. Giờ trả xe mới: ${newEndTime}.`,
      });
      setTimeout(() => setShowToast(null), 4000);
    }, 600);
  };

  // UI-11: Xử lý Sửa thông tin đặt chỗ trước check-in
  const handleOpenEditModal = (b: BookingItem) => {
    setActiveEditBooking(b);
    setEditPlate(b.plate);
    setEditTimeShift('0');
    setEditNotes(b.notes || '');
  };

  const handleConfirmEdit = () => {
    if (!activeEditBooking) return;
    setIsSubmittingEdit(true);

    setTimeout(() => {
      let updatedTimeRange = activeEditBooking.timeRange;
      if (editTimeShift === '+30') {
        updatedTimeRange = '15:30 - 18:30';
      } else if (editTimeShift === '+60') {
        updatedTimeRange = '16:00 - 19:00';
      } else if (editTimeShift === '-30') {
        updatedTimeRange = '14:30 - 17:30';
      }

      const updated = bookings.map((b) =>
        b.id === activeEditBooking.id
          ? {
              ...b,
              plate: editPlate,
              timeRange: updatedTimeRange,
              notes: editNotes,
            }
          : b
      );

      setBookings(updated);
      try {
        localStorage.setItem('parkmaster_driver_bookings', JSON.stringify(updated));
      } catch (err) {
        console.error(err);
      }

      setIsSubmittingEdit(false);
      setActiveEditBooking(null);
      setShowToast({
        title: 'Cập nhật vé thành công!',
        message: `Thông tin biển số và giờ đến cho vé ${activeEditBooking.code} đã được cập nhật đồng bộ với cổng.`,
      });
      setTimeout(() => setShowToast(null), 4000);
    }, 500);
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
              QUẢN LÝ LƯỢT ĐẶT CHỖ, GIA HẠN & HÓA ĐƠN ĐIỆN TỬ (UI-10, UI-11, UI-13)
            </div>
            <h1 className="text-3xl font-extrabold text-slate-900 tracking-tight">
              Lịch sử đặt chỗ
            </h1>
            <p className="text-slate-500 text-sm max-w-2xl leading-relaxed">
              Theo dõi lịch sử gửi xe, lấy mã QR check-in, sửa thông tin đặt chỗ trước giờ đến, gia hạn thêm thời gian đỗ, hoặc xuất hóa đơn VAT điện tử nhanh chóng.
            </p>
          </div>

          <div className="flex items-center gap-3 self-start md:self-auto">
            <button
              type="button"
              onClick={() => navigate('/driver/find-lot')}
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
              <span className="text-[11px] text-emerald-600 font-medium mt-0.5">+4 lượt trong tháng này</span>
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
              <span className="text-[11px] text-slate-500 font-medium mt-0.5">Chính sách hoàn 100% &lt;1h</span>
            </div>
            <div className="w-12 h-12 rounded-xl bg-slate-100 text-slate-600 flex items-center justify-center shrink-0">
              <span className="material-symbols-outlined text-[24px]">replay</span>
            </div>
          </div>
        </div>

        {/* 4. Controls & Filters */}
        <div className="bg-white p-4 rounded-2xl border border-slate-200 shadow-xs flex flex-col md:flex-row items-center justify-between gap-4">
          <div className="flex items-center gap-1.5 p-1 bg-slate-100 rounded-xl w-full md:w-auto overflow-x-auto">
            <button
              type="button"
              onClick={() => setActiveTab('all')}
              className={`px-4 py-1.5 rounded-lg text-xs font-bold transition-all cursor-pointer whitespace-nowrap ${
                activeTab === 'all' ? 'bg-white text-slate-900 shadow-xs' : 'text-slate-600 hover:text-slate-900'
              }`}
            >
              Tất cả ({bookings.length})
            </button>
            <button
              type="button"
              onClick={() => setActiveTab('upcoming')}
              className={`px-4 py-1.5 rounded-lg text-xs font-bold transition-all cursor-pointer whitespace-nowrap ${
                activeTab === 'upcoming' ? 'bg-white text-blue-600 shadow-xs' : 'text-slate-600 hover:text-slate-900'
              }`}
            >
              Sắp diễn ra ({bookings.filter((b) => b.status === 'upcoming').length})
            </button>
            <button
              type="button"
              onClick={() => setActiveTab('completed')}
              className={`px-4 py-1.5 rounded-lg text-xs font-bold transition-all cursor-pointer whitespace-nowrap ${
                activeTab === 'completed' ? 'bg-white text-emerald-600 shadow-xs' : 'text-slate-600 hover:text-slate-900'
              }`}
            >
              Đã hoàn thành ({bookings.filter((b) => b.status === 'completed').length})
            </button>
            <button
              type="button"
              onClick={() => setActiveTab('cancelled')}
              className={`px-4 py-1.5 rounded-lg text-xs font-bold transition-all cursor-pointer whitespace-nowrap ${
                activeTab === 'cancelled' ? 'bg-white text-rose-600 shadow-xs' : 'text-slate-600 hover:text-slate-900'
              }`}
            >
              Đã hủy ({bookings.filter((b) => b.status === 'cancelled').length})
            </button>
          </div>

          <div className="relative w-full md:w-80">
            <span className="material-symbols-outlined absolute left-3 top-2.5 text-slate-400 text-[18px]">
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

        {/* 5. Booking Table or Empty State */}
        {bookings.length === 0 ? (
          <div className="bg-white rounded-3xl border border-slate-200 p-12 text-center flex flex-col items-center justify-center shadow-xs">
            <div className="w-16 h-16 rounded-2xl bg-blue-50 text-blue-600 flex items-center justify-center mb-4">
              <span className="material-symbols-outlined text-[32px]">receipt_long</span>
            </div>
            <h3 className="text-base font-bold text-slate-800 mb-1">Bạn chưa có lượt đặt chỗ nào</h3>
            <p className="text-xs text-slate-500 max-w-sm mb-6">
              Khi bạn đặt chỗ trước tại các bãi đỗ thông minh ParkMaster, toàn bộ vé điện tử và mã QR check-in sẽ được lưu trữ và hiển thị tại đây.
            </p>
            <button
              type="button"
              onClick={() => navigate('/driver/find-lot')}
              className="px-5 py-2.5 rounded-xl bg-blue-600 hover:bg-blue-700 text-white text-xs font-bold transition-all shadow-sm flex items-center gap-2 cursor-pointer"
            >
              <span className="material-symbols-outlined text-[16px]">near_me</span>
              <span>Tìm bãi đỗ xe ngay</span>
            </button>
          </div>
        ) : (
          <div className="bg-white rounded-2xl border border-slate-200 shadow-xs overflow-hidden">
            <div className="overflow-x-auto">
              <table className="w-full table-fixed min-w-[1350px] text-left border-separate border-spacing-0">
                <colgroup>
                  <col style={{ width: '190px' }} />
                  <col style={{ width: '200px' }} />
                  <col style={{ width: '180px' }} />
                  <col style={{ width: '210px' }} />
                  <col style={{ width: '170px' }} />
                  <col style={{ width: '140px' }} />
                  <col style={{ width: '260px' }} />
                </colgroup>
                <thead>
                  <tr className="bg-slate-50/80 text-[11px] font-bold text-slate-500 uppercase tracking-wider">
                    <th className="py-3.5 px-4 border-b border-slate-200">Mã đặt chỗ & Xe</th>
                    <th className="py-3.5 px-4 border-b border-slate-200">Bãi đỗ & Vị trí</th>
                    <th className="py-3.5 px-4 border-b border-slate-200">Thời gian</th>
                    <th className="py-3.5 pl-4 pr-6 border-b border-slate-200">Dịch vụ</th>
                    <th className="py-3.5 pl-2 pr-4 border-b border-slate-200">Tổng tiền</th>
                    <th className="py-3.5 px-3 border-b border-slate-200">Trạng thái</th>
                    <th className="py-3.5 px-4 text-right border-b border-slate-200">Thao tác</th>
                  </tr>
                </thead>
                <tbody className="text-xs">
                  {filteredBookings.length === 0 ? (
                    <tr>
                      <td colSpan={7} className="py-12 text-center text-slate-400 text-xs border-b border-slate-100">
                        Không tìm thấy lượt đặt chỗ nào phù hợp.
                      </td>
                    </tr>
                  ) : (
                    filteredBookings.map((b) => (
                      <BookingTableRow
                        key={b.id}
                        booking={b}
                        isHighlighted={highlightId === b.id}
                        onViewQr={setActiveQrBooking}
                        onExtend={handleOpenExtendModal}
                        onEdit={handleOpenEditModal}
                        onCancel={handleOpenCancelModal}
                        onInvoice={setActiveInvoiceBooking}
                        onFeedback={handleOpenFeedback}
                        onRefundDetail={setActiveRefundDetailBooking}
                      />
                    ))
                  )}
                </tbody>
              </table>
            </div>          </div>
        )}
      </main>

      {/* 6. UI-11: MODAL GIA HẠN GIỜ ĐỖ */}
      {activeExtendBooking && (
        <div className="fixed inset-0 bg-slate-900/60 backdrop-blur-xs z-50 flex items-center justify-center p-4 animate-in fade-in">
          <div className="bg-white rounded-3xl shadow-2xl p-6 sm:p-7 w-full max-w-md border border-slate-200">
            <div className="flex items-center justify-between pb-4 border-b border-slate-100">
              <div className="flex items-center gap-2">
                <div className="w-9 h-9 rounded-xl bg-indigo-50 text-indigo-600 flex items-center justify-center font-bold">
                  <span className="material-symbols-outlined text-[20px]">more_time</span>
                </div>
                <div>
                  <h3 className="text-base font-bold text-slate-900">Gia hạn thời gian đỗ (UI-11)</h3>
                  <p className="text-xs text-slate-500">Vé: {activeExtendBooking.code}</p>
                </div>
              </div>
              <button
                type="button"
                onClick={() => setActiveExtendBooking(null)}
                className="text-slate-400 hover:text-slate-700 p-1 rounded-full cursor-pointer"
              >
                <span className="material-symbols-outlined text-[20px]">close</span>
              </button>
            </div>

            <div className="py-4 space-y-4 text-xs">
              <div className="p-3 bg-slate-50 rounded-xl border border-slate-200 space-y-1">
                <div className="flex justify-between text-slate-600">
                  <span>Bãi đỗ:</span>
                  <span className="font-semibold text-slate-800">{activeExtendBooking.location}</span>
                </div>
                <div className="flex justify-between text-slate-600">
                  <span>Vị trí hiện tại:</span>
                  <span className="font-bold text-emerald-600">{activeExtendBooking.spot}</span>
                </div>
                <div className="flex justify-between text-slate-600">
                  <span>Khung giờ ban đầu:</span>
                  <span className="font-bold text-slate-800">{activeExtendBooking.timeRange}</span>
                </div>
              </div>

              <div>
                <label className="block font-bold text-slate-700 mb-2">Chọn thời gian muốn gia hạn thêm:</label>
                <div className="grid grid-cols-3 gap-2">
                  {[
                    { h: 0.5, label: '+30 phút', fee: 15000 },
                    { h: 1, label: '+1 giờ', fee: 30000 },
                    { h: 2, label: '+2 giờ', fee: 60000 },
                  ].map((opt) => (
                    <button
                      key={opt.h}
                      type="button"
                      onClick={() => setExtendHours(opt.h)}
                      className={`p-3 rounded-xl border text-center transition-all cursor-pointer ${
                        extendHours === opt.h
                          ? 'border-indigo-600 bg-indigo-50/70 text-indigo-900 font-bold ring-2 ring-indigo-600/20'
                          : 'border-slate-200 bg-white hover:bg-slate-50 text-slate-700'
                      }`}
                    >
                      <div className="text-sm font-bold">{opt.label}</div>
                      <div className="text-[11px] text-slate-500 mt-0.5">+{opt.fee.toLocaleString()} đ</div>
                    </button>
                  ))}
                </div>
              </div>

              <div>
                <label className="block font-bold text-slate-700 mb-1.5">Phương thức thanh toán phụ phí:</label>
                <select
                  value={extendPaymentMethod}
                  onChange={(e) => setExtendPaymentMethod(e.target.value as any)}
                  className="w-full p-2.5 rounded-xl border border-slate-300 text-xs bg-white text-slate-800 focus:ring-2 focus:ring-indigo-500 outline-none"
                >
                  <option value="wallet">Ví ParkMaster (Số dư: 450.000 đ - Khuyên dùng)</option>
                  <option value="vnpay">Cổng VNPAY-QR</option>
                  <option value="gate">Thanh toán trực tiếp tại bốt ra (Pay at Gate)</option>
                </select>
              </div>

              <div className="p-3 bg-indigo-50/50 rounded-xl border border-indigo-100 flex items-center justify-between font-bold text-xs">
                <span className="text-indigo-900">Phụ phí gia hạn phát sinh:</span>
                <span className="text-base text-indigo-700">{(extendHours * 30000).toLocaleString('vi-VN')} đ</span>
              </div>
            </div>

            <div className="flex gap-2.5 pt-3 border-t border-slate-100">
              <button
                type="button"
                onClick={() => setActiveExtendBooking(null)}
                className="flex-1 py-2.5 rounded-xl border border-slate-200 text-slate-600 hover:bg-slate-50 text-xs font-semibold transition-colors cursor-pointer"
              >
                Hủy bỏ
              </button>
              <button
                type="button"
                disabled={isSubmittingExtend}
                onClick={handleConfirmExtend}
                className="flex-1 py-2.5 rounded-xl bg-indigo-600 hover:bg-indigo-700 disabled:opacity-50 text-white font-bold text-xs shadow-sm flex items-center justify-center gap-1.5 transition-colors cursor-pointer"
              >
                {isSubmittingExtend ? 'Đang gia hạn…' : 'Xác nhận gia hạn'}
              </button>
            </div>
          </div>
        </div>
      )}

      {/* 7. UI-11: MODAL SỬA BOOKING TRƯỚC CHECK-IN */}
      {activeEditBooking && (
        <div className="fixed inset-0 bg-slate-900/60 backdrop-blur-xs z-50 flex items-center justify-center p-4 animate-in fade-in">
          <div className="bg-white rounded-3xl shadow-2xl p-6 sm:p-7 w-full max-w-md border border-slate-200">
            <div className="flex items-center justify-between pb-4 border-b border-slate-100">
              <div className="flex items-center gap-2">
                <div className="w-9 h-9 rounded-xl bg-blue-50 text-blue-600 flex items-center justify-center font-bold">
                  <span className="material-symbols-outlined text-[20px]">edit_calendar</span>
                </div>
                <div>
                  <h3 className="text-base font-bold text-slate-900">Sửa thông tin đặt chỗ (UI-11)</h3>
                  <p className="text-xs text-slate-500">Vé: {activeEditBooking.code}</p>
                </div>
              </div>
              <button
                type="button"
                onClick={() => setActiveEditBooking(null)}
                className="text-slate-400 hover:text-slate-700 p-1 rounded-full cursor-pointer"
              >
                <span className="material-symbols-outlined text-[20px]">close</span>
              </button>
            </div>

            <div className="py-4 space-y-4 text-xs">
              <div>
                <label className="block font-bold text-slate-700 mb-1">Phương tiện đỗ (Biển số xe):</label>
                <select
                  value={editPlate}
                  onChange={(e) => setEditPlate(e.target.value)}
                  className="w-full p-2.5 rounded-xl border border-slate-300 text-xs bg-white text-slate-800 focus:ring-2 focus:ring-blue-500 outline-none"
                >
                  <option value={activeEditBooking.plate}>{activeEditBooking.plate} ({activeEditBooking.carModel})</option>
                  {vehicles
                    .filter((v) => v.plateNumber !== activeEditBooking.plate)
                    .map((v) => (
                      <option key={v.id} value={v.plateNumber}>
                        {v.plateNumber} · {v.brand} {v.model}
                      </option>
                    ))}
                </select>
                <p className="text-[11px] text-slate-400 mt-1">Biển số này sẽ được hệ thống camera ANPR tại cổng nhận diện tự động mở barie.</p>
              </div>

              <div>
                <label className="block font-bold text-slate-700 mb-1">Dời giờ check-in dự kiến:</label>
                <div className="grid grid-cols-3 gap-2">
                  {[
                    { id: '-30', label: 'Sớm 30p' },
                    { id: '0', label: 'Giữ nguyên' },
                    { id: '+30', label: 'Lùi 30p' },
                  ].map((item) => (
                    <button
                      key={item.id}
                      type="button"
                      onClick={() => setEditTimeShift(item.id)}
                      className={`p-2.5 rounded-xl border text-center transition-all cursor-pointer font-bold ${
                        editTimeShift === item.id
                          ? 'border-blue-600 bg-blue-50 text-blue-700'
                          : 'border-slate-200 bg-white text-slate-600 hover:bg-slate-50'
                      }`}
                    >
                      {item.label}
                    </button>
                  ))}
                </div>
              </div>

              <div>
                <label className="block font-bold text-slate-700 mb-1">Ghi chú cho ban quản lý bãi:</label>
                <input
                  type="text"
                  value={editNotes}
                  onChange={(e) => setEditNotes(e.target.value)}
                  placeholder="Ví dụ: Ưu tiên sạc pin, xe gầm thấp..."
                  className="w-full p-2.5 rounded-xl border border-slate-300 text-xs bg-white text-slate-800 focus:ring-2 focus:ring-blue-500 outline-none"
                />
              </div>
            </div>

            <div className="flex gap-2.5 pt-3 border-t border-slate-100">
              <button
                type="button"
                onClick={() => setActiveEditBooking(null)}
                className="flex-1 py-2.5 rounded-xl border border-slate-200 text-slate-600 hover:bg-slate-50 text-xs font-semibold transition-colors cursor-pointer"
              >
                Hủy bỏ
              </button>
              <button
                type="button"
                disabled={isSubmittingEdit}
                onClick={handleConfirmEdit}
                className="flex-1 py-2.5 rounded-xl bg-blue-600 hover:bg-blue-700 disabled:opacity-50 text-white font-bold text-xs shadow-sm flex items-center justify-center gap-1.5 transition-colors cursor-pointer"
              >
                {isSubmittingEdit ? 'Đang lưu…' : 'Lưu thay đổi'}
              </button>
            </div>
          </div>
        </div>
      )}

      {/* 8. UI-13: MODAL HÓA ĐƠN ĐIỆN TỬ VAT (E-INVOICE) */}
      {activeInvoiceBooking && (
        <div className="fixed inset-0 bg-slate-900/60 backdrop-blur-xs z-50 flex items-center justify-center p-4 animate-in fade-in">
          <div className="bg-white rounded-3xl shadow-2xl p-6 sm:p-8 w-full max-w-xl border border-slate-200 max-h-[90vh] overflow-y-auto">
            <div className="flex items-center justify-between pb-4 border-b border-slate-200">
              <div className="flex items-center gap-2">
                <span className="material-symbols-outlined text-blue-600 text-[24px]">receipt_long</span>
                <div>
                  <h3 className="text-base font-extrabold text-slate-900">Hóa đơn giá trị gia tăng điện tử (UI-13)</h3>
                  <p className="text-[11px] text-slate-500">Ký hiệu: 1C26TPM · Số HĐ: #HD-2026-{activeInvoiceBooking.code.replace(/[^0-9]/g, '') || '99214'}</p>
                </div>
              </div>
              <button
                type="button"
                onClick={() => setActiveInvoiceBooking(null)}
                className="text-slate-400 hover:text-slate-700 p-1 rounded-full cursor-pointer"
              >
                <span className="material-symbols-outlined text-[20px]">close</span>
              </button>
            </div>

            {/* Bố cục hóa đơn VAT chuẩn */}
            <div className="my-5 p-5 rounded-2xl bg-slate-50/80 border border-slate-200 text-xs text-slate-700 space-y-4">
              <div className="flex flex-col sm:flex-row justify-between pb-3 border-b border-slate-200 gap-3">
                <div>
                  <p className="font-extrabold text-slate-900 text-sm">CÔNG TY CỔ PHẦN CÔNG NGHỆ PARKMASTER</p>
                  <p className="text-slate-500 mt-0.5">MST: 0318999888 · Hotline: 1900 8899</p>
                  <p className="text-slate-500">Tầng 12, Tòa nhà Innovation, TP. Hồ Chí Minh</p>
                </div>
                <div className="sm:text-right shrink-0">
                  <span className="inline-flex items-center gap-1 px-2 py-0.5 rounded-full bg-emerald-100 text-emerald-800 text-[10px] font-bold">
                    <span className="material-symbols-outlined text-[12px]">verified</span>
                    Đã ký số điện tử
                  </span>
                  <p className="text-[11px] text-slate-500 mt-1">Ngày lập: {activeInvoiceBooking.dateLabel}</p>
                </div>
              </div>

              <div className="grid grid-cols-2 gap-3 text-xs">
                <div>
                  <span className="text-slate-400 block text-[11px]">Khách hàng / Người mua:</span>
                  <span className="font-bold text-slate-800">{user?.fullName || 'Bùi Phạm Chí Nhân'}</span>
                </div>
                <div>
                  <span className="text-slate-400 block text-[11px]">Mã định danh / CCCD:</span>
                  <span className="font-mono text-slate-800">079201004812</span>
                </div>
                <div>
                  <span className="text-slate-400 block text-[11px]">Phương tiện đỗ:</span>
                  <span className="font-bold text-slate-800">{activeInvoiceBooking.plate} ({activeInvoiceBooking.carModel})</span>
                </div>
                <div>
                  <span className="text-slate-400 block text-[11px]">Bãi đỗ xe:</span>
                  <span className="font-semibold text-slate-800">{activeInvoiceBooking.location}</span>
                </div>
              </div>

              {/* Bảng kê chi phí */}
              <div className="border border-slate-200 rounded-xl overflow-hidden bg-white">
                <table className="w-full text-left text-xs">
                  <thead className="bg-slate-100 font-bold text-slate-600 text-[11px]">
                    <tr>
                      <th className="p-2.5">Nội dung dịch vụ</th>
                      <th className="p-2.5 text-center">ĐVT</th>
                      <th className="p-2.5 text-right">Thành tiền</th>
                    </tr>
                  </thead>
                  <tbody className="divide-y divide-slate-100">
                    <tr>
                      <td className="p-2.5">Dịch vụ trông giữ xe thông minh theo lượt</td>
                      <td className="p-2.5 text-center text-slate-500">Lượt</td>
                      <td className="p-2.5 text-right font-medium">{Math.round(activeInvoiceBooking.totalPrice * 0.92).toLocaleString('vi-VN')} đ</td>
                    </tr>
                    {activeInvoiceBooking.services.length > 0 && (
                      <tr>
                        <td className="p-2.5 text-slate-600">Dịch vụ cộng thêm ({activeInvoiceBooking.services.join(', ')})</td>
                        <td className="p-2.5 text-center text-slate-500">Gói</td>
                        <td className="p-2.5 text-right font-medium">Bao gồm</td>
                      </tr>
                    )}
                    <tr className="bg-slate-50/50">
                      <td colSpan={2} className="p-2 text-slate-500 text-right">Thuế suất GTGT (8%):</td>
                      <td className="p-2 text-right font-medium text-slate-700">{Math.round(activeInvoiceBooking.totalPrice * 0.08).toLocaleString('vi-VN')} đ</td>
                    </tr>
                    <tr className="bg-slate-100/70 font-bold text-slate-900">
                      <td colSpan={2} className="p-2.5 text-right">Tổng thanh toán:</td>
                      <td className="p-2.5 text-right text-blue-600 font-extrabold">{activeInvoiceBooking.totalPrice.toLocaleString('vi-VN')} đ</td>
                    </tr>
                  </tbody>
                </table>
              </div>

              <div className="text-[11px] text-slate-500 flex items-center justify-between pt-1">
                <span>Hình thức thanh toán: {activeInvoiceBooking.paymentMethod}</span>
                <span className="font-mono text-slate-400">Tra cứu tại: https://tracuu.parkmaster.vn</span>
              </div>
            </div>

            <div className="flex gap-2.5 pt-2">
              <button
                type="button"
                onClick={() => window.print()}
                className="flex-1 py-2.5 rounded-xl border border-slate-300 text-slate-700 hover:bg-slate-50 text-xs font-bold transition-colors flex items-center justify-center gap-1.5 cursor-pointer"
              >
                <span className="material-symbols-outlined text-[16px]">print</span>
                <span>In hóa đơn</span>
              </button>
              <button
                type="button"
                onClick={() => {
                  const invoiceContent = `HOA DON DIEN TU PARKMASTER\nSo HD: #HD-2026-${activeInvoiceBooking.code}\nKhach hang: ${user?.fullName || 'Bui Pham Chi Nhan'}\nBien so: ${activeInvoiceBooking.plate}\nBai do: ${activeInvoiceBooking.location}\nTong tien: ${activeInvoiceBooking.totalPrice.toLocaleString('vi-VN')} VND\nTrang thai: Da thanh toan & Ky so hop le.`;
                  const blob = new Blob([invoiceContent], { type: 'text/plain' });
                  const url = URL.createObjectURL(blob);
                  const a = document.createElement('a');
                  a.href = url;
                  a.download = `HoaDon_ParkMaster_${activeInvoiceBooking.code}.txt`;
                  a.click();
                  URL.revokeObjectURL(url);
                }}
                className="flex-1 py-2.5 rounded-xl bg-blue-600 hover:bg-blue-700 text-white font-bold text-xs shadow-sm flex items-center justify-center gap-1.5 transition-colors cursor-pointer"
              >
                <span className="material-symbols-outlined text-[16px]">download</span>
                <span>Tải hóa đơn (PDF/TXT)</span>
              </button>
            </div>
          </div>
        </div>
      )}

      {/* 9. Cancellation Modal */}
      {activeCancelBooking && (
        <div className="fixed inset-0 bg-slate-900/60 backdrop-blur-xs z-50 flex items-center justify-center p-4 animate-in fade-in">
          <div className="bg-white rounded-3xl shadow-2xl p-6 sm:p-7 w-full max-w-md border border-slate-200">
            <div className="flex items-center justify-between pb-4 border-b border-slate-100">
              <div className="flex items-center gap-2">
                <div className="w-9 h-9 rounded-xl bg-rose-50 text-rose-600 flex items-center justify-center font-bold">
                  <span className="material-symbols-outlined text-[20px]">event_busy</span>
                </div>
                <div>
                  <h3 className="text-base font-bold text-slate-900">Xác nhận hủy giữ chỗ</h3>
                  <p className="text-xs text-slate-500">Mã vé: {activeCancelBooking.code}</p>
                </div>
              </div>
              <button
                type="button"
                onClick={() => setActiveCancelBooking(null)}
                className="text-slate-400 hover:text-slate-700 p-1 rounded-full cursor-pointer"
              >
                <span className="material-symbols-outlined text-[20px]">close</span>
              </button>
            </div>

            <div className="py-4 space-y-4 text-xs">
              <div className="p-3 bg-slate-50 rounded-xl border border-slate-200 space-y-1">
                <div className="flex justify-between text-slate-600">
                  <span>Bãi đỗ:</span>
                  <span className="font-semibold text-slate-800">{activeCancelBooking.location}</span>
                </div>
                <div className="flex justify-between text-slate-600">
                  <span>Biển số xe:</span>
                  <span className="font-semibold text-slate-800">{activeCancelBooking.plate}</span>
                </div>
                <div className="flex justify-between text-slate-600">
                  <span>Khung giờ:</span>
                  <span className="font-semibold text-slate-800">{activeCancelBooking.timeRange}</span>
                </div>
              </div>

              <div>
                <label className="block font-bold text-slate-700 mb-1.5">Lý do hủy đặt chỗ:</label>
                <select
                  value={cancelReason}
                  onChange={(e) => setCancelReason(e.target.value)}
                  className="w-full p-2.5 rounded-xl border border-slate-300 text-xs bg-white text-slate-800 focus:ring-2 focus:ring-rose-500 outline-none"
                >
                  <option value="schedule_change">Thay đổi lịch trình công việc</option>
                  <option value="found_other">Tìm thấy vị trí đỗ khác thuận tiện hơn</option>
                  <option value="vehicle_issue">Phương tiện gặp sự cố kỹ thuật</option>
                  <option value="other">Lý do khác...</option>
                </select>
              </div>

              {cancelReason === 'other' && (
                <div>
                  <input
                    type="text"
                    value={customReason}
                    onChange={(e) => setCustomReason(e.target.value)}
                    placeholder="Nhập lý do chi tiết..."
                    className="w-full p-2.5 rounded-xl border border-slate-300 text-xs bg-white text-slate-800 focus:ring-2 focus:ring-rose-500 outline-none"
                  />
                </div>
              )}

              <div>
                <label className="block font-bold text-slate-700 mb-1.5">Hình thức nhận tiền hoàn:</label>
                <div className="grid grid-cols-2 gap-2">
                  <button
                    type="button"
                    onClick={() => setRefundMethod('wallet')}
                    className={`p-3 rounded-xl border text-left transition-all cursor-pointer ${
                      refundMethod === 'wallet'
                        ? 'border-blue-600 bg-blue-50/70 text-blue-900 font-bold ring-2 ring-blue-600/20'
                        : 'border-slate-200 bg-white hover:bg-slate-50 text-slate-700'
                    }`}
                  >
                    <div className="text-xs font-bold">Ví ParkMaster</div>
                    <div className="text-[10px] text-emerald-600 mt-0.5">Nhận ngay tức thì</div>
                  </button>
                  <button
                    type="button"
                    onClick={() => setRefundMethod('original')}
                    className={`p-3 rounded-xl border text-left transition-all cursor-pointer ${
                      refundMethod === 'original'
                        ? 'border-blue-600 bg-blue-50/70 text-blue-900 font-bold ring-2 ring-blue-600/20'
                        : 'border-slate-200 bg-white hover:bg-slate-50 text-slate-700'
                    }`}
                  >
                    <div className="text-xs font-bold">Tài khoản gốc</div>
                    <div className="text-[10px] text-slate-500 mt-0.5">1-3 ngày làm việc</div>
                  </button>
                </div>
              </div>

              <div className="p-3 bg-emerald-50 rounded-xl border border-emerald-200 flex items-center justify-between font-bold text-xs">
                <span className="text-emerald-900">Số tiền được hoàn (100%):</span>
                <span className="text-base text-emerald-700">{refundAmount.toLocaleString('vi-VN')} đ</span>
              </div>
            </div>

            <div className="flex gap-2.5 pt-3 border-t border-slate-100">
              <button
                type="button"
                onClick={() => setActiveCancelBooking(null)}
                className="flex-1 py-2.5 rounded-xl border border-slate-200 text-slate-600 hover:bg-slate-50 text-xs font-semibold transition-colors cursor-pointer"
              >
                Giữ lại vé
              </button>
              <button
                type="button"
                disabled={isCancelling}
                onClick={handleConfirmCancel}
                className="flex-1 py-2.5 rounded-xl bg-rose-600 hover:bg-rose-700 disabled:opacity-50 text-white font-bold text-xs shadow-sm flex items-center justify-center gap-1.5 transition-colors cursor-pointer"
              >
                {isCancelling ? 'Đang xử lý…' : 'Xác nhận hủy vé'}
              </button>
            </div>
          </div>
        </div>
      )}

      {/* 10. Feedback Rating Modal */}
      {activeFeedbackBooking && (
        <div className="fixed inset-0 bg-slate-900/60 backdrop-blur-xs z-50 flex items-center justify-center p-4 animate-in fade-in">
          <div className="bg-white rounded-3xl shadow-2xl p-6 sm:p-7 w-full max-w-md border border-slate-200">
            <div className="flex items-center justify-between pb-3 border-b border-slate-100">
              <h3 className="text-base font-bold text-slate-900">Đánh giá lượt gửi xe (UI-16)</h3>
              <button
                type="button"
                onClick={() => setActiveFeedbackBooking(null)}
                className="text-slate-400 hover:text-slate-700 p-1 cursor-pointer"
              >
                <span className="material-symbols-outlined text-[20px]">close</span>
              </button>
            </div>

            <div className="py-4 text-center">
              <p className="text-xs text-slate-500 mb-2">Bạn cảm thấy trải nghiệm đỗ xe tại <strong>{activeFeedbackBooking.location}</strong> thế nào?</p>
              <div className="flex items-center justify-center gap-2 mb-2">
                {[1, 2, 3, 4, 5].map((star) => (
                  <button
                    key={star}
                    type="button"
                    onClick={() => setRatingValue(star)}
                    className="p-1 hover:scale-110 transition-transform cursor-pointer"
                  >
                    <span className={`material-symbols-outlined text-[32px] ${
                      star <= ratingValue ? 'text-amber-400 fill-amber-400' : 'text-slate-200'
                    }`}>
                      star
                    </span>
                  </button>
                ))}
              </div>
              <span className="text-xs font-bold text-amber-600">{RATING_DESCRIPTIONS[ratingValue]}</span>
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
                className="px-4 py-2.5 rounded-xl border border-slate-200 text-slate-600 hover:bg-slate-100 text-xs font-semibold transition-colors cursor-pointer"
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

      {/* 11. QR Code Entrance Modal */}
      {activeQrBooking && (
        <div className="fixed inset-0 bg-slate-900/60 backdrop-blur-xs z-50 flex items-center justify-center p-4 animate-in fade-in">
          <div className="bg-white rounded-2xl shadow-2xl p-6 sm:p-7 w-full max-w-sm border border-slate-200 flex flex-col items-center text-center">
            <div className="flex items-center justify-between w-full mb-3">
              <span className="text-xs font-bold text-blue-600">{activeQrBooking.code}</span>
              <button
                type="button"
                onClick={() => setActiveQrBooking(null)}
                className="text-slate-400 hover:text-slate-700 p-1 cursor-pointer"
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
              className="w-full py-2.5 rounded-xl bg-slate-100 text-slate-700 font-bold text-xs hover:bg-slate-200 transition-colors cursor-pointer"
            >
              Đóng
            </button>
          </div>
        </div>
      )}

      {/* 12. Refund Detail Modal */}
      {activeRefundDetailBooking && (
        <div className="fixed inset-0 bg-slate-900/60 backdrop-blur-xs z-50 flex items-center justify-center p-4 animate-in fade-in">
          <div className="bg-white rounded-3xl shadow-2xl p-6 sm:p-7 w-full max-w-sm border border-slate-200 text-center">
            <div className="w-12 h-12 rounded-full bg-emerald-100 text-emerald-600 flex items-center justify-center mx-auto mb-3">
              <span className="material-symbols-outlined text-[24px]">price_check</span>
            </div>
            <h3 className="text-base font-bold text-slate-900">Chi tiết hoàn tiền</h3>
            <p className="text-xs text-slate-500 mt-1 mb-4">Vé {activeRefundDetailBooking.code} đã hoàn tất hoàn tiền</p>
            <div className="p-3 bg-slate-50 rounded-xl border border-slate-200 text-xs text-left space-y-1.5 mb-5">
              <div className="flex justify-between">
                <span className="text-slate-500">Trạng thái:</span>
                <span className="font-bold text-emerald-600">Đã hoàn tiền 100%</span>
              </div>
              <div className="flex justify-between">
                <span className="text-slate-500">Kênh nhận:</span>
                <span className="font-medium text-slate-700">Ví ParkMaster</span>
              </div>
              <div className="flex justify-between">
                <span className="text-slate-500">Thời gian hoàn:</span>
                <span className="font-medium text-slate-700">Tức thì</span>
              </div>
            </div>
            <button
              type="button"
              onClick={() => setActiveRefundDetailBooking(null)}
              className="w-full py-2.5 rounded-xl bg-slate-100 text-slate-700 font-bold text-xs hover:bg-slate-200 transition-colors cursor-pointer"
            >
              Đóng
            </button>
          </div>
        </div>
      )}

      {/* Toast notifications */}
      {showToast && (
        <div className="fixed bottom-6 right-6 z-50 animate-in fade-in slide-in-from-bottom-5">
          <div className="bg-slate-900 text-white px-5 py-3.5 rounded-xl shadow-xl flex items-center gap-3 border border-slate-700 max-w-md">
            <div className="w-7 h-7 rounded-full bg-emerald-500/20 text-emerald-400 flex items-center justify-center shrink-0">
              <span className="material-symbols-outlined text-[18px]">check_circle</span>
            </div>
            <div>
              <p className="font-bold text-xs">{showToast.title}</p>
              <p className="text-[11px] text-slate-300">{showToast.message}</p>
            </div>
          </div>
        </div>
      )}

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
    </div>
  );
};
