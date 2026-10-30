import React, { useState, useEffect } from 'react';
import { Link, useNavigate, useSearchParams } from 'react-router-dom';

type PaymentStatus = 'success' | 'failed' | 'pending';
type PaymentMethodType = 'vnpay' | 'momo' | 'wallet' | 'etc';

export const PaymentResultPage: React.FC = () => {
  const navigate = useNavigate();
  const [searchParams] = useSearchParams();

  // URL Query params or default demo state
  const initialStatus = (searchParams.get('status') as PaymentStatus) || 'success';
  const initialMethod = (searchParams.get('method') as PaymentMethodType) || 'vnpay';
  const bookingCode = searchParams.get('code') || '#PKM-2025-8842';
  const amountParam = searchParams.get('amount');
  const amount = amountParam ? parseInt(amountParam, 10) : 175000;

  const [status, setStatus] = useState<PaymentStatus>(initialStatus);
  const [method, setMethod] = useState<PaymentMethodType>(initialMethod);
  const [failReason, setFailReason] = useState<'timeout' | 'insufficient_funds' | 'cancelled'>('insufficient_funds');
  const [isDownloadingReceipt, setIsDownloadingReceipt] = useState(false);
  const [downloadSuccess, setDownloadSuccess] = useState(false);

  // Auto transition for 'pending' state
  useEffect(() => {
    if (status === 'pending') {
      const timer = setTimeout(() => {
        setStatus('success');
      }, 3000);
      return () => clearTimeout(timer);
    }
  }, [status]);

  const handleDownloadReceipt = () => {
    setIsDownloadingReceipt(true);
    setTimeout(() => {
      setIsDownloadingReceipt(false);
      setDownloadSuccess(true);
      setTimeout(() => setDownloadSuccess(false), 4000);
    }, 1000);
  };

  const getMethodName = (m: PaymentMethodType) => {
    switch (m) {
      case 'vnpay':
        return 'Cổng thanh toán VNPAY-QR (Sandbox)';
      case 'momo':
        return 'Ví điện tử MoMo AutoPay';
      case 'wallet':
        return 'Ví ParkMaster AutoPay';
      case 'etc':
        return 'Tài khoản thu phí không dừng VETC/ePass';
      default:
        return 'Cổng thanh toán trực tuyến';
    }
  };

  return (
    <div className="min-h-screen bg-slate-50 flex flex-col font-sans">
      {/* 1. Header Banner & State Switcher */}
      <section className="bg-white border-b border-slate-200">
        <div className="max-w-4xl mx-auto px-4 sm:px-6 py-6 flex flex-col sm:flex-row sm:items-center justify-between gap-4">
          <div className="flex flex-col gap-1">
            <div className="inline-flex items-center gap-2 px-3 py-1 rounded-full bg-blue-100 text-blue-800 w-fit text-[11px] font-bold tracking-wider uppercase">
              <span className="w-2 h-2 rounded-full bg-blue-600 animate-pulse"></span>
              KẾT QUẢ THANH TOÁN VNPAY / MOMO (UI-12)
            </div>
            <h1 className="text-2xl font-extrabold text-slate-900 tracking-tight">
              Biên nhận thanh toán & Vé đỗ xe
            </h1>
          </div>

          {/* Interactive Demo State Switcher */}
          <div className="flex flex-col gap-1.5 bg-slate-100 p-1.5 rounded-2xl border border-slate-200 self-start sm:self-auto">
            <span className="text-[10px] font-bold text-slate-500 uppercase tracking-wider px-2">
              Mô phỏng kịch bản cổng thanh toán:
            </span>
            <div className="flex items-center gap-1">
              <button
                type="button"
                onClick={() => { setStatus('success'); setMethod('vnpay'); }}
                className={`px-3 py-1.5 rounded-xl text-xs font-bold transition-all cursor-pointer ${
                  status === 'success' && method === 'vnpay'
                    ? 'bg-blue-600 text-white shadow-xs'
                    : 'text-slate-600 hover:text-slate-900 bg-white'
                }`}
              >
                VNPAY Thành công
              </button>
              <button
                type="button"
                onClick={() => { setStatus('success'); setMethod('momo'); }}
                className={`px-3 py-1.5 rounded-xl text-xs font-bold transition-all cursor-pointer ${
                  status === 'success' && method === 'momo'
                    ? 'bg-pink-600 text-white shadow-xs'
                    : 'text-slate-600 hover:text-slate-900 bg-white'
                }`}
              >
                MoMo Thành công
              </button>
              <button
                type="button"
                onClick={() => setStatus('failed')}
                className={`px-3 py-1.5 rounded-xl text-xs font-bold transition-all cursor-pointer ${
                  status === 'failed'
                    ? 'bg-rose-600 text-white shadow-xs'
                    : 'text-slate-600 hover:text-slate-900 bg-white'
                }`}
              >
                Thất bại
              </button>
              <button
                type="button"
                onClick={() => setStatus('pending')}
                className={`px-3 py-1.5 rounded-xl text-xs font-bold transition-all cursor-pointer ${
                  status === 'pending'
                    ? 'bg-amber-500 text-white shadow-xs'
                    : 'text-slate-600 hover:text-slate-900 bg-white'
                }`}
              >
                Đang xử lý
              </button>
            </div>
          </div>
        </div>
      </section>

      {/* 2. Main Content Container */}
      <main className="max-w-3xl mx-auto px-4 sm:px-6 py-8 flex flex-col gap-6 flex-1 w-full">
        {/* CASE 1: SUCCESS STATE */}
        {status === 'success' && (
          <div className="flex flex-col gap-6 animate-in fade-in slide-in-from-bottom-3 duration-300">
            {/* Status Hero Card */}
            <div className="bg-white rounded-3xl border border-emerald-200/80 p-6 sm:p-8 shadow-sm flex flex-col items-center text-center relative overflow-hidden">
              <div className="absolute -top-12 -right-12 w-36 h-36 bg-emerald-100/50 rounded-full blur-2xl pointer-events-none"></div>

              <div className="w-16 h-16 rounded-2xl bg-emerald-500 text-white flex items-center justify-center shadow-lg shadow-emerald-500/20 mb-4 animate-bounce">
                <span className="material-symbols-outlined text-[36px]">check_circle</span>
              </div>

              <span className="inline-flex items-center gap-1.5 px-3 py-1 rounded-full bg-emerald-100 text-emerald-800 text-xs font-extrabold uppercase tracking-wider mb-2">
                Giao dịch hợp lệ • Đã khóa chỗ 100%
              </span>

              <h2 className="text-2xl sm:text-3xl font-extrabold text-slate-900 tracking-tight">
                Thanh toán giữ chỗ thành công!
              </h2>
              <p className="text-slate-500 text-xs sm:text-sm mt-1.5 max-w-lg leading-relaxed">
                Hệ thống camera ANPR tại cổng bãi đỗ đã tự động đồng bộ biển số xe. Bạn chỉ cần lái xe đến cổng, barie sẽ tự động nhận diện và mở cửa.
              </p>

              {/* Amount Display */}
              <div className="my-5 p-4 rounded-2xl bg-emerald-50/70 border border-emerald-100 w-full max-w-md flex items-center justify-between">
                <div className="flex flex-col text-left">
                  <span className="text-[11px] font-bold text-emerald-800 uppercase tracking-wider">
                    Số tiền thanh toán
                  </span>
                  <span className="text-xs text-slate-500">{getMethodName(method)}</span>
                </div>
                <span className="text-2xl font-extrabold text-emerald-600 font-mono">
                  {amount.toLocaleString('vi-VN')} đ
                </span>
              </div>
            </div>

            {/* Smart Digital Parking Pass */}
            <div className="bg-white rounded-3xl border border-slate-200/80 shadow-md overflow-hidden">
              {/* Pass Header */}
              <div className="bg-slate-900 text-white p-5 sm:p-6 flex items-center justify-between border-b border-slate-800">
                <div className="flex items-center gap-3">
                  <div className="w-10 h-10 rounded-xl bg-blue-600 flex items-center justify-center text-white font-black text-sm">
                    PM
                  </div>
                  <div>
                    <h3 className="font-extrabold text-base tracking-tight">ParkMaster Digital Pass</h3>
                    <p className="text-[11px] text-slate-400 font-mono">ID: {bookingCode}</p>
                  </div>
                </div>
                <span className="px-3 py-1 rounded-full bg-emerald-500/20 text-emerald-400 text-xs font-bold border border-emerald-500/30">
                  Hợp lệ
                </span>
              </div>

              {/* Pass Body */}
              <div className="p-6 sm:p-8 flex flex-col md:flex-row items-center gap-8">
                {/* QR Code Section */}
                <div className="flex flex-col items-center shrink-0">
                  <div className="p-4 bg-white rounded-2xl border-2 border-slate-900 shadow-inner flex flex-col items-center">
                    <div className="w-40 h-40 bg-slate-900 flex items-center justify-center rounded-xl p-2 relative">
                      {/* SVG QR Code Pattern */}
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
                  <span className="text-[11px] text-slate-500 font-semibold mt-2.5 text-center">
                    Quét tại trụ barie nếu camera bận
                  </span>
                </div>

                {/* Info List */}
                <div className="grid grid-cols-1 sm:grid-cols-2 gap-4 flex-1 w-full text-xs">
                  <div className="p-3.5 rounded-2xl bg-slate-50 border border-slate-200/80 flex flex-col gap-0.5">
                    <span className="text-slate-400 font-medium">Bãi đỗ xe</span>
                    <span className="font-bold text-slate-900 text-sm">Landmark 81 Garage</span>
                    <span className="text-[11px] text-slate-500">720A Điện Biên Phủ, P.22, Bình Thạnh</span>
                  </div>

                  <div className="p-3.5 rounded-2xl bg-slate-50 border border-slate-200/80 flex flex-col gap-0.5">
                    <span className="text-slate-400 font-medium">Vị trí ô đỗ</span>
                    <span className="font-extrabold text-emerald-600 text-sm">Ô B-04 • Tầng Hầm B2</span>
                    <span className="text-[11px] text-slate-500">Trạm sạc EV nhanh 120kW sẵn sàng</span>
                  </div>

                  <div className="p-3.5 rounded-2xl bg-slate-50 border border-slate-200/80 flex flex-col gap-0.5">
                    <span className="text-slate-400 font-medium">Khung giờ giữ chỗ</span>
                    <span className="font-bold text-slate-900 text-sm">14:00 - 18:00 (Hôm nay)</span>
                    <span className="text-[11px] text-blue-600 font-semibold">Miễn phí 15 phút grace period</span>
                  </div>

                  <div className="p-3.5 rounded-2xl bg-slate-50 border border-slate-200/80 flex flex-col gap-0.5">
                    <span className="text-slate-400 font-medium">Phương tiện nhận diện</span>
                    <span className="font-bold text-slate-900 text-sm flex items-center gap-1.5">
                      <span>51K-889.32</span>
                      <span className="px-1.5 py-0.2 rounded bg-blue-100 text-blue-700 text-[10px] font-bold">
                        ANPR
                      </span>
                    </span>
                    <span className="text-[11px] text-slate-500">Mercedes EQE SUV (Xe điện)</span>
                  </div>

                  <div className="p-3.5 rounded-2xl bg-slate-50 border border-slate-200/80 flex flex-col gap-0.5 sm:col-span-2">
                    <span className="text-slate-400 font-medium">Mã giao dịch đối soát</span>
                    <span className="font-mono font-bold text-slate-800 text-xs">
                      {method === 'vnpay' ? 'VNP-20261026-9842109' : 'MOMO-20261026-784210'}
                    </span>
                  </div>
                </div>
              </div>

              {/* Pass Footer Actions */}
              <div className="bg-slate-50 px-6 py-4 border-t border-slate-200 flex flex-wrap items-center justify-between gap-3">
                <button
                  type="button"
                  disabled={isDownloadingReceipt}
                  onClick={handleDownloadReceipt}
                  className="px-4 py-2.5 rounded-xl border border-slate-200 hover:bg-white text-slate-700 text-xs font-bold transition-all shadow-xs flex items-center gap-2 cursor-pointer"
                >
                  {isDownloadingReceipt ? (
                    <>
                      <span className="material-symbols-outlined text-[16px] animate-spin">sync</span>
                      <span>Đang xuất hóa đơn…</span>
                    </>
                  ) : (
                    <>
                      <span className="material-symbols-outlined text-[16px]">download</span>
                      <span>Tải biên lai thanh toán</span>
                    </>
                  )}
                </button>

                <div className="flex items-center gap-3">
                  <button
                    type="button"
                    onClick={() => navigate('/driver/history')}
                    className="px-5 py-2.5 rounded-xl bg-slate-900 hover:bg-slate-800 text-white text-xs font-bold transition-all shadow-sm flex items-center gap-2 cursor-pointer"
                  >
                    <span className="material-symbols-outlined text-[16px]">receipt_long</span>
                    <span>Quản lý trong Lịch sử</span>
                  </button>
                  <Link
                    to="/driver/parking-lots"
                    className="px-5 py-2.5 rounded-xl bg-blue-600 hover:bg-blue-700 text-white text-xs font-bold transition-all shadow-sm flex items-center gap-2"
                  >
                    <span>Chỉ đường đến bãi</span>
                    <span className="material-symbols-outlined text-[16px]">navigation</span>
                  </Link>
                </div>
              </div>
            </div>
          </div>
        )}

        {/* CASE 2: FAILED STATE */}
        {status === 'failed' && (
          <div className="bg-white rounded-3xl border border-rose-200 shadow-sm p-6 sm:p-10 flex flex-col gap-6 animate-in fade-in slide-in-from-bottom-3 duration-300">
            <div className="flex flex-col sm:flex-row sm:items-center justify-between gap-4 p-5 rounded-2xl bg-rose-50/80 border border-rose-100">
              <div className="flex items-center gap-4">
                <div className="w-14 h-14 rounded-2xl bg-rose-600 text-white flex items-center justify-center shadow-lg shadow-rose-600/20">
                  <span className="material-symbols-outlined text-[32px]">error</span>
                </div>
                <div>
                  <div className="inline-flex items-center gap-1.5 text-xs font-bold text-rose-700 uppercase tracking-wider">
                    <span className="w-2 h-2 rounded-full bg-rose-600"></span>
                    Thanh toán không thành công
                  </div>
                  <h2 className="text-xl font-bold text-slate-900 mt-0.5">
                    Giao dịch bị từ chối bởi cổng thanh toán
                  </h2>
                  <p className="text-slate-500 text-xs mt-1">
                    Mã lỗi: <span className="font-mono font-bold text-slate-800">ERR_PAYMENT_DECLINED_99</span>
                  </p>
                </div>
              </div>
              <span className="px-4 py-2 rounded-xl bg-rose-100 text-rose-800 text-xs font-extrabold uppercase tracking-wider self-start sm:self-auto border border-rose-200">
                Thất bại
              </span>
            </div>

            {/* Error Cause Selection for Demo */}
            <div className="p-4 rounded-2xl bg-slate-50 border border-slate-200 text-xs flex flex-col gap-2">
              <span className="font-bold text-slate-700">Nguyên nhân từ chối giao dịch:</span>
              <div className="grid grid-cols-1 sm:grid-cols-3 gap-2">
                <button
                  type="button"
                  onClick={() => setFailReason('insufficient_funds')}
                  className={`p-2.5 rounded-xl border text-left cursor-pointer transition-all ${
                    failReason === 'insufficient_funds'
                      ? 'border-rose-500 bg-rose-50 text-rose-900 font-bold'
                      : 'border-slate-200 bg-white text-slate-600 hover:bg-slate-50'
                  }`}
                >
                  Tài khoản không đủ số dư
                </button>
                <button
                  type="button"
                  onClick={() => setFailReason('timeout')}
                  className={`p-2.5 rounded-xl border text-left cursor-pointer transition-all ${
                    failReason === 'timeout'
                      ? 'border-rose-500 bg-rose-50 text-rose-900 font-bold'
                      : 'border-slate-200 bg-white text-slate-600 hover:bg-slate-50'
                  }`}
                >
                  Quá hạn giữ chỗ 15 phút
                </button>
                <button
                  type="button"
                  onClick={() => setFailReason('cancelled')}
                  className={`p-2.5 rounded-xl border text-left cursor-pointer transition-all ${
                    failReason === 'cancelled'
                      ? 'border-rose-500 bg-rose-50 text-rose-900 font-bold'
                      : 'border-slate-200 bg-white text-slate-600 hover:bg-slate-50'
                  }`}
                >
                  Người dùng hủy giao dịch
                </button>
              </div>

              <p className="text-slate-500 mt-2 leading-relaxed">
                {failReason === 'insufficient_funds' &&
                  'Tài khoản nguồn hoặc thẻ ngân hàng của bạn không đủ hạn mức thanh toán 175.000 đ. Vui lòng nạp thêm tiền hoặc chọn phương thức thanh toán khác.'}
                {failReason === 'timeout' &&
                  'Phiên giữ chỗ tạm thời (15 phút) đã hết hạn. Vị trí ô đỗ B-04 đã tự động mở lại cho người dùng khác theo quy định.'}
                {failReason === 'cancelled' &&
                  'Bạn đã hủy thao tác quét mã tại cổng thanh toán VNPAY/MoMo. Chưa có khoản tiền nào bị trừ khỏi tài khoản của bạn.'}
              </p>
            </div>

            <div className="flex items-center justify-between pt-4 border-t border-slate-200">
              <Link
                to="/driver"
                className="px-5 py-2.5 rounded-xl border border-slate-200 hover:bg-slate-50 text-slate-700 text-xs font-bold transition-all"
              >
                Về trang chủ
              </Link>
              <button
                type="button"
                onClick={() => navigate('/driver/parking-lots')}
                className="px-6 py-2.5 rounded-xl bg-blue-600 hover:bg-blue-700 text-white text-xs font-bold transition-all shadow-sm flex items-center gap-2 cursor-pointer"
              >
                <span className="material-symbols-outlined text-[18px]">replay</span>
                <span>Thử đặt chỗ & Thanh toán lại</span>
              </button>
            </div>
          </div>
        )}

        {/* CASE 3: PENDING STATE */}
        {status === 'pending' && (
          <div className="bg-white rounded-3xl border border-amber-200 shadow-sm p-8 sm:p-12 flex flex-col items-center text-center gap-5">
            <div className="w-16 h-16 rounded-2xl bg-amber-500 text-white flex items-center justify-center shadow-lg shadow-amber-500/20">
              <span className="material-symbols-outlined text-[36px] animate-spin">sync</span>
            </div>
            <h2 className="text-2xl font-extrabold text-slate-900 tracking-tight">
              Đang đối soát kết quả từ Cổng thanh toán…
            </h2>
            <p className="text-slate-500 text-xs sm:text-sm max-w-md leading-relaxed">
              Hệ thống đang tiếp nhận tín hiệu IPN bảo mật từ ngân hàng. Giao diện sẽ tự động cập nhật kết quả trong giây lát, vui lòng không tắt trình duyệt.
            </p>
            <div className="w-48 h-1.5 bg-slate-100 rounded-full overflow-hidden">
              <div className="w-full h-full bg-amber-500 animate-pulse"></div>
            </div>
          </div>
        )}

        {/* Toast for Receipt Download */}
        {downloadSuccess && (
          <div className="fixed bottom-6 right-6 z-50 animate-in fade-in slide-in-from-bottom-5">
            <div className="bg-slate-900 text-white px-5 py-4 rounded-2xl shadow-2xl flex items-center gap-3.5 border border-slate-700 max-w-md">
              <div className="w-8 h-8 rounded-full bg-emerald-500/20 text-emerald-400 flex items-center justify-center shrink-0">
                <span className="material-symbols-outlined text-[20px]">file_download_done</span>
              </div>
              <div className="flex flex-col">
                <p className="font-bold text-xs text-white">Đã tải biên lai điện tử!</p>
                <p className="text-[11px] text-slate-300 mt-0.5">
                  File biên nhận PDF đã được lưu trữ an toàn vào máy của bạn.
                </p>
              </div>
            </div>
          </div>
        )}
      </main>
    </div>
  );
};
