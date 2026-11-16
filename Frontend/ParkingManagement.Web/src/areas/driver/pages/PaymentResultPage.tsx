import React, { useEffect, useState } from 'react';
import { useLocation, useNavigate, useSearchParams } from 'react-router-dom';
import { QRCodeSVG } from 'qrcode.react';
import { bookingApi } from '@/api/bookingApi';
import { paymentApi } from '@/api/paymentApi';
import type { BookingDetailDto } from '@/types/booking';
import type { InvoiceDto, PaymentDto, VnpayReturnResultDto } from '@/types/payment';

type ViewStatus = 'loading' | 'success' | 'failed' | 'pending' | 'error';

const extractApiError = (err: unknown, fallback: string): string => {
  const axiosErr = err as { response?: { data?: { detail?: string; title?: string; message?: string } } };
  return (
    axiosErr?.response?.data?.detail ||
    axiosErr?.response?.data?.title ||
    axiosErr?.response?.data?.message ||
    (err instanceof Error ? err.message : fallback)
  );
};

export const PaymentResultPage: React.FC = () => {
  const navigate = useNavigate();
  const location = useLocation();
  const [searchParams] = useSearchParams();

  const [viewStatus, setViewStatus] = useState<ViewStatus>('loading');
  const [statusMessage, setStatusMessage] = useState<string>('Đang xác thực giao dịch với hệ thống…');
  const [vnpayResult, setVnpayResult] = useState<VnpayReturnResultDto | null>(null);
  const [booking, setBooking] = useState<BookingDetailDto | null>(null);
  const [payments, setPayments] = useState<PaymentDto[]>([]);
  const [invoice, setInvoice] = useState<InvoiceDto | null>(null);
  const [errorText, setErrorText] = useState<string | null>(null);

  useEffect(() => {
    let active = true;

    const loadResult = async () => {
      setViewStatus('loading');
      setErrorText(null);

      const hasVnpayParams = Array.from(searchParams.keys()).some((k) => k.startsWith('vnp_'));
      const codeParam = searchParams.get('code');

      try {
        let resolvedBookingCode = codeParam || '';

        if (hasVnpayParams) {
          const vnpRes = await paymentApi.processVnpayReturn(location.search);
          if (!active) return;
          setVnpayResult(vnpRes);
          setStatusMessage(vnpRes.message);
          resolvedBookingCode = vnpRes.bookingCode || resolvedBookingCode;
          setViewStatus(vnpRes.isSuccess ? 'success' : 'failed');
        }

        if (resolvedBookingCode) {
          const bookingDetail = await bookingApi.getByCode(resolvedBookingCode);
          if (!active) return;
          setBooking(bookingDetail);

          const [paymentList, invoiceRes] = await Promise.allSettled([
            paymentApi.listByBooking(bookingDetail.id),
            paymentApi.getInvoiceByBooking(bookingDetail.id),
          ]);
          if (!active) return;

          if (paymentList.status === 'fulfilled') {
            setPayments(paymentList.value);
          }
          if (invoiceRes.status === 'fulfilled') {
            setInvoice(invoiceRes.value);
          }

          if (!hasVnpayParams) {
            const latestSucceeded =
              paymentList.status === 'fulfilled' &&
              paymentList.value.some((p) => p.status === 'Succeeded');

            if (latestSucceeded || bookingDetail.status === 'Confirmed' || bookingDetail.status === 'Completed') {
              setViewStatus('success');
              setStatusMessage('Đơn đặt chỗ đã được xác nhận.');
            } else if (bookingDetail.status === 'PendingPayment') {
              setViewStatus('pending');
              setStatusMessage('Đơn đặt chỗ đang chờ hoàn tất thanh toán.');
            } else if (
              bookingDetail.status === 'Cancelled' ||
              bookingDetail.status === 'CancelledNoRefund' ||
              bookingDetail.status === 'Expired' ||
              bookingDetail.status === 'Rejected'
            ) {
              setViewStatus('failed');
              setStatusMessage(`Đơn hiện ở trạng thái: ${bookingDetail.status}`);
            } else {
              setViewStatus('pending');
              setStatusMessage(`Trạng thái đơn: ${bookingDetail.status}`);
            }
          }
        } else if (!hasVnpayParams) {
          setViewStatus('error');
          setErrorText('Thiếu mã đặt chỗ (?code=...) hoặc tham số callback VNPAY.');
        }
      } catch (err) {
        if (!active) return;
        setViewStatus('error');
        setErrorText(extractApiError(err, 'Không thể tải kết quả thanh toán từ hệ thống.'));
      }
    };

    void loadResult();
    return () => {
      active = false;
    };
  }, [location.search, searchParams]);

  const latestPayment = payments[0];
  const displayAmount = vnpayResult?.amount ?? latestPayment?.amount ?? booking?.totalAmount ?? 0;
  const displayCode = vnpayResult?.bookingCode ?? booking?.code ?? '—';

  const handleDownloadInvoice = () => {
    if (invoice?.pdfUrl) {
      window.open(invoice.pdfUrl, '_blank');
      return;
    }
    const content = invoice
      ? `HOA DON DIEN TU PARKMASTER\nSo HD: ${invoice.invoiceNumber}\nKhach hang: ${invoice.buyerName}\nMa dat cho: ${displayCode}\nTam tinh: ${invoice.subTotal.toLocaleString('vi-VN')} VND\nVAT: ${invoice.vatAmount.toLocaleString('vi-VN')} VND\nTong cong: ${invoice.total.toLocaleString('vi-VN')} VND\nNgay phat hanh: ${new Date(invoice.issuedAtUtc).toLocaleString('vi-VN')}\nTrang thai: ${invoice.status}`
      : `BIEN NHAN DAT CHO PARKMASTER\nMa ve: ${displayCode}\nBai do: ${booking?.parkingLotName || '—'}\nTong tien: ${displayAmount.toLocaleString('vi-VN')} VND\nTrang thai: ${statusMessage}`;

    const blob = new Blob([content], { type: 'text/plain;charset=utf-8' });
    const url = URL.createObjectURL(blob);
    const a = document.createElement('a');
    a.href = url;
    a.download = `ParkMaster_${invoice?.invoiceNumber || displayCode}.txt`;
    a.click();
    URL.revokeObjectURL(url);
  };

  return (
    <div className="min-h-screen bg-slate-50 flex flex-col font-sans">
      <section className="bg-white border-b border-slate-200">
        <div className="max-w-4xl mx-auto px-4 sm:px-6 py-6 flex flex-col gap-1">
          <div className="inline-flex items-center gap-2 px-3 py-1 rounded-full bg-blue-100 text-blue-800 w-fit text-[11px] font-bold tracking-wider uppercase">
            <span className="w-2 h-2 rounded-full bg-blue-600 animate-pulse" />
            KẾT QUẢ THANH TOÁN & HÓA ĐƠN ĐIỆN TỬ
          </div>
          <h1 className="text-2xl font-extrabold text-slate-900 tracking-tight">
            Biên nhận thanh toán & Vé đỗ xe
          </h1>
        </div>
      </section>

      <main className="max-w-4xl mx-auto px-4 sm:px-6 py-8 flex-1 w-full">
        {viewStatus === 'loading' ? (
          <div className="bg-white rounded-3xl border border-slate-200 p-12 text-center shadow-xs">
            <div className="w-12 h-12 rounded-full border-4 border-blue-200 border-t-blue-600 animate-spin mx-auto mb-4" />
            <p className="text-sm font-bold text-slate-700">Đang xác thực giao dịch và tải thông tin vé…</p>
          </div>
        ) : viewStatus === 'error' ? (
          <div className="bg-white rounded-3xl border border-rose-200 p-8 text-center shadow-xs space-y-4">
            <span className="material-symbols-outlined text-[42px] text-rose-500">error</span>
            <h2 className="text-lg font-bold text-slate-900">Không thể xác thực giao dịch</h2>
            <p className="text-xs text-rose-600">{errorText}</p>
            <div className="flex justify-center gap-3">
              <button
                type="button"
                onClick={() => navigate('/driver/history')}
                className="px-5 py-2.5 rounded-xl bg-blue-600 text-white text-xs font-bold hover:bg-blue-700 cursor-pointer"
              >
                Về Lịch sử đặt chỗ
              </button>
            </div>
          </div>
        ) : (
          <div className="bg-white rounded-3xl border border-slate-200 shadow-sm overflow-hidden">
            <div
              className={`p-6 text-center border-b ${
                viewStatus === 'success'
                  ? 'bg-emerald-50/70 border-emerald-100'
                  : viewStatus === 'pending'
                  ? 'bg-amber-50/70 border-amber-100'
                  : 'bg-rose-50/70 border-rose-100'
              }`}
            >
              <span
                className={`material-symbols-outlined text-[48px] ${
                  viewStatus === 'success'
                    ? 'text-emerald-600'
                    : viewStatus === 'pending'
                    ? 'text-amber-500'
                    : 'text-rose-600'
                }`}
              >
                {viewStatus === 'success' ? 'check_circle' : viewStatus === 'pending' ? 'schedule' : 'cancel'}
              </span>
              <h2 className="text-xl font-extrabold text-slate-900 mt-2">
                {viewStatus === 'success'
                  ? 'Thanh toán / Đặt chỗ thành công!'
                  : viewStatus === 'pending'
                  ? 'Đang chờ thanh toán'
                  : 'Thanh toán không thành công'}
              </h2>
              <p className="text-xs text-slate-600 mt-1">{statusMessage}</p>
              <div className="text-3xl font-extrabold text-slate-900 mt-3">
                {displayAmount.toLocaleString('vi-VN')} đ
              </div>
            </div>

            <div className="p-6 sm:p-8 grid grid-cols-1 md:grid-cols-2 gap-6">
              <div className="space-y-3 text-xs">
                <h3 className="font-bold text-slate-900 uppercase tracking-wider text-[11px]">Chi tiết giao dịch & Vé</h3>
                <div className="p-4 rounded-2xl bg-slate-50 border border-slate-200 space-y-2">
                  <div className="flex justify-between">
                    <span className="text-slate-500">Mã đặt chỗ:</span>
                    <span className="font-bold text-blue-600">{displayCode}</span>
                  </div>
                  {booking && (
                    <>
                      <div className="flex justify-between">
                        <span className="text-slate-500">Bãi đỗ:</span>
                        <span className="font-semibold text-slate-800">{booking.parkingLotName}</span>
                      </div>
                      <div className="flex justify-between">
                        <span className="text-slate-500">Vị trí:</span>
                        <span className="font-semibold text-slate-800">{booking.slotCode || 'Tự động'}</span>
                      </div>
                      <div className="flex justify-between">
                        <span className="text-slate-500">Trạng thái vé:</span>
                        <span className="font-bold text-slate-800">{booking.status}</span>
                      </div>
                    </>
                  )}
                  {(vnpayResult?.paymentCode || latestPayment?.paymentCode) && (
                    <div className="flex justify-between">
                      <span className="text-slate-500">Mã giao dịch:</span>
                      <span className="font-mono font-semibold text-slate-800">
                        {vnpayResult?.paymentCode || latestPayment?.paymentCode}
                      </span>
                    </div>
                  )}
                </div>

                {invoice && (
                  <div className="p-4 rounded-2xl bg-blue-50/60 border border-blue-200 space-y-1.5">
                    <div className="font-bold text-blue-900">Hóa đơn điện tử: {invoice.invoiceNumber}</div>
                    <div className="flex justify-between">
                      <span className="text-slate-600">Tạm tính:</span>
                      <span>{invoice.subTotal.toLocaleString('vi-VN')} đ</span>
                    </div>
                    <div className="flex justify-between">
                      <span className="text-slate-600">VAT:</span>
                      <span>{invoice.vatAmount.toLocaleString('vi-VN')} đ</span>
                    </div>
                    <div className="flex justify-between font-bold text-blue-800 border-t border-blue-200 pt-1">
                      <span>Tổng cộng:</span>
                      <span>{invoice.total.toLocaleString('vi-VN')} đ</span>
                    </div>
                  </div>
                )}
              </div>

              <div className="flex flex-col items-center justify-center p-5 rounded-2xl bg-slate-50 border border-slate-200 text-center">
                {booking?.qrToken ? (
                  <>
                    <span className="text-xs font-bold text-slate-800 mb-3">Mã QR Check-in tại cổng</span>
                    <div className="p-3 bg-white rounded-2xl border border-slate-200 shadow-xs">
                      <QRCodeSVG value={booking.qrToken} size={156} level="M" />
                    </div>
                    <span className="text-[11px] text-slate-500 mt-2">Đưa mã này trước camera/máy quét tại barie</span>
                  </>
                ) : (
                  <div className="text-xs text-slate-500">
                    <span className="material-symbols-outlined text-[32px] text-slate-400 mb-1 block">qr_code_2</span>
                    Mã QR check-in sẽ hiển thị sau khi đơn được xác nhận.
                  </div>
                )}
              </div>
            </div>

            <div className="px-6 sm:px-8 pb-6 flex flex-wrap gap-3">
              <button
                type="button"
                onClick={handleDownloadInvoice}
                className="px-4 py-2.5 rounded-xl bg-slate-100 hover:bg-slate-200 text-slate-800 font-bold text-xs flex items-center gap-1.5 cursor-pointer"
              >
                <span className="material-symbols-outlined text-[16px]">download</span>
                <span>{invoice?.pdfUrl ? 'Tải hóa đơn PDF' : 'Tải biên nhận / hóa đơn'}</span>
              </button>
              <button
                type="button"
                onClick={() => navigate('/driver/history')}
                className="px-5 py-2.5 rounded-xl bg-blue-600 hover:bg-blue-700 text-white font-bold text-xs cursor-pointer"
              >
                Xem Lịch sử đặt chỗ
              </button>
              <button
                type="button"
                onClick={() => navigate('/driver/find-lot')}
                className="px-4 py-2.5 rounded-xl border border-slate-200 text-slate-700 hover:bg-slate-50 font-bold text-xs cursor-pointer"
              >
                Đặt chỗ mới
              </button>
            </div>
          </div>
        )}
      </main>
    </div>
  );
};
