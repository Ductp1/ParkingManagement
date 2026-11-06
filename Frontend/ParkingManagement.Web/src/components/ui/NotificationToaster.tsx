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
      className="toast-slide-in pointer-events-auto w-[360px] max-w-[calc(100vw-2rem)] bg-white rounded-2xl shadow-2xl border border-slate-200 p-4 flex gap-3 cursor-pointer hover:border-blue-300 transition-colors"
    >
      <div className="w-10 h-10 rounded-xl bg-blue-100 text-blue-600 flex items-center justify-center shrink-0">
        <span className="material-symbols-outlined text-[22px]">notifications_active</span>
      </div>
      <div className="flex-1 min-w-0">
        <p className="text-sm font-bold text-slate-900">{toast.title}</p>
        <p className="text-xs text-slate-500 line-clamp-2 mt-0.5">{toast.body}</p>
        <span className="text-[10px] font-semibold text-blue-600 mt-1.5 flex items-center gap-1 uppercase tracking-wider">
          <span>Vừa nhận • Real-time</span>
          {hasBooking && (
            <span className="inline-flex items-center gap-0.5">
              <span>• Xem đặt chỗ</span>
              <span className="material-symbols-outlined text-[12px]">arrow_forward</span>
            </span>
          )}
        </span>
      </div>
      <button
        type="button"
        aria-label="Đóng thông báo"
        onClick={(e) => {
          e.stopPropagation(); // Chỉ đóng toast, không điều hướng
          dispatch(dismissToast(toast.id));
        }}
        className="p-1 h-fit rounded-lg text-slate-400 hover:text-slate-700 hover:bg-slate-100 cursor-pointer"
      >
        <span className="material-symbols-outlined text-[18px]">close</span>
      </button>
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
