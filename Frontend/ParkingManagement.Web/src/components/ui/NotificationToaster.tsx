import React, { useEffect } from 'react';
import { useNavigate } from 'react-router-dom';
import { useAppDispatch, useAppSelector } from '@/store';
import { dismissToast, type NotificationToast } from '@/store/slices/toastSlice';
import { hasBookingRef, notificationTargetRoute } from '@/utils/notificationBooking';

/** Thời gian 1 toast hiển thị trước khi tự ẩn (yêu cầu demo: >= 10 giây). */
const TOAST_VISIBLE_MS = 15000;

const ToastCard: React.FC<{ toast: NotificationToast }> = ({ toast }) => {
  const dispatch = useAppDispatch();
  const navigate = useNavigate();
  const hasBooking = hasBookingRef(toast.dataJson);

  // Tự đóng sau 15s; người dùng có thể đóng sớm bằng nút X (không bao giờ tự ẩn trước 15s).
  useEffect(() => {
    const timer = window.setTimeout(() => dispatch(dismissToast(toast.id)), TOAST_VISIBLE_MS);
    return () => window.clearTimeout(timer);
  }, [dispatch, toast.id]);

  /** Bấm vào thân toast → mở thông tin đặt chỗ của thông báo đó (giống bấm trên chuông Navbar). */
  const handleOpenBooking = (): void => {
    dispatch(dismissToast(toast.id));
    navigate(notificationTargetRoute(toast.dataJson));
  };

  return (
    <div
      role="button"
      tabIndex={0}
      aria-label={hasBooking ? 'Mở thông tin đặt chỗ liên quan' : 'Mở trang đặt chỗ'}
      onClick={handleOpenBooking}
      onKeyDown={(e) => {
        if (e.key === 'Enter' || e.key === ' ') handleOpenBooking();
      }}
      className="toast-slide-in pointer-events-auto group w-[400px] max-w-[calc(100vw-2rem)] bg-white/95 backdrop-blur-md rounded-2xl shadow-xl shadow-slate-200/70 border border-slate-200 p-4 flex gap-3.5 cursor-pointer hover:border-blue-400 hover:shadow-2xl transition-all"
    >
      <div className="w-10 h-10 rounded-xl bg-blue-50 text-blue-600 border border-blue-100/80 flex items-center justify-center shrink-0 mt-0.5 shadow-xs">
        <span className="material-symbols-outlined text-[22px]">notifications_active</span>
      </div>
      <div className="flex-1 min-w-0">
        <div className="flex items-start justify-between gap-2">
          <p className="text-sm font-bold text-slate-900 leading-snug">{toast.title}</p>
          <button
            type="button"
            aria-label="Đóng thông báo"
            onClick={(e) => {
              e.stopPropagation(); // Chỉ đóng toast, không điều hướng
              dispatch(dismissToast(toast.id));
            }}
            className="-mr-1 -mt-1 p-1 rounded-lg text-slate-400 hover:text-slate-700 hover:bg-slate-100 transition-colors cursor-pointer shrink-0"
          >
            <span className="material-symbols-outlined text-[18px]">close</span>
          </button>
        </div>
        <p className="text-xs text-slate-600 line-clamp-2 mt-1 leading-relaxed">{toast.body}</p>
        <div className="flex items-center justify-between gap-2 mt-3 pt-2.5 border-t border-slate-100">
          <span className="inline-flex items-center gap-1.5 text-[10.5px] font-semibold text-blue-700 bg-blue-50 px-2 py-0.5 rounded-md border border-blue-100/80 whitespace-nowrap">
            <span className="w-1.5 h-1.5 rounded-full bg-blue-600 animate-pulse"></span>
            <span>Vừa nhận • Real-time</span>
          </span>
          {hasBooking && (
            <span className="inline-flex items-center gap-1 text-[11px] font-bold text-blue-600 group-hover:text-blue-700 group-hover:translate-x-0.5 transition-all whitespace-nowrap">
              <span>Xem đặt chỗ</span>
              <span className="material-symbols-outlined text-[14px]">arrow_forward</span>
            </span>
          )}
        </div>
      </div>
    </div>
  );
};

/** Stack popup thông báo real-time – góc phải dưới Navbar, hiển thị trên mọi trang. */
export const NotificationToaster: React.FC = () => {
  const { toasts } = useAppSelector((state) => state.toast);
  if (toasts.length === 0) return null;

  return (
    <div className="fixed top-20 right-6 z-[70] flex flex-col gap-3 pointer-events-none">
      {toasts.map((t) => (
        <ToastCard key={t.id} toast={t} />
      ))}
    </div>
  );
};
