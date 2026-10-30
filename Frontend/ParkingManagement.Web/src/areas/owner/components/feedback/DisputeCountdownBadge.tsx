import React, { useState, useEffect } from 'react';
import { Clock, CheckCircle2, AlertTriangle, XCircle, ShieldCheck } from 'lucide-react';
import type { ComplaintStatus } from '@/types/feedback';

interface DisputeCountdownBadgeProps {
  status: ComplaintStatus;
  dueAtUtc?: string | null;
}

export const DisputeCountdownBadge: React.FC<DisputeCountdownBadgeProps> = ({ status, dueAtUtc }) => {
  const [timeLeft, setTimeLeft] = useState<{ hours: number; minutes: number; isOverdue: boolean } | null>(null);

  useEffect(() => {
    if (status !== 'AwaitingOwner' || !dueAtUtc) {
      setTimeLeft(null);
      return;
    }

    const calculateTimeLeft = () => {
      const now = new Date().getTime();
      const due = new Date(dueAtUtc).getTime();
      const diff = due - now;

      if (diff <= 0) {
        setTimeLeft({ hours: 0, minutes: 0, isOverdue: true });
        return;
      }

      const totalMinutes = Math.floor(diff / (1000 * 60));
      const hours = Math.floor(totalMinutes / 60);
      const minutes = totalMinutes % 60;

      setTimeLeft({ hours, minutes, isOverdue: false });
    };

    calculateTimeLeft();
    const interval = setInterval(calculateTimeLeft, 60000); // cập nhật mỗi phút
    return () => clearInterval(interval);
  }, [status, dueAtUtc]);

  if (status === 'OwnerResponded') {
    return (
      <span className="inline-flex items-center gap-1.5 px-2.5 py-1 rounded-full text-xs font-semibold bg-blue-50 text-blue-700 border border-blue-200">
        <CheckCircle2 className="w-3.5 h-3.5 text-blue-600" />
        Đã phản hồi
      </span>
    );
  }

  if (status === 'Escalated') {
    return (
      <span className="inline-flex items-center gap-1.5 px-2.5 py-1 rounded-full text-xs font-semibold bg-purple-50 text-purple-700 border border-purple-200">
        <AlertTriangle className="w-3.5 h-3.5 text-purple-600" />
        Chuyển nền tảng
      </span>
    );
  }

  if (status === 'Resolved') {
    return (
      <span className="inline-flex items-center gap-1.5 px-2.5 py-1 rounded-full text-xs font-semibold bg-emerald-50 text-emerald-700 border border-emerald-200">
        <ShieldCheck className="w-3.5 h-3.5 text-emerald-600" />
        Đã giải quyết
      </span>
    );
  }

  if (status === 'Rejected') {
    return (
      <span className="inline-flex items-center gap-1.5 px-2.5 py-1 rounded-full text-xs font-semibold bg-neutral-100 text-neutral-600 border border-neutral-200">
        <XCircle className="w-3.5 h-3.5 text-neutral-500" />
        Bị từ chối
      </span>
    );
  }

  // Trường hợp đang chờ chủ bãi:
  if (!timeLeft || timeLeft.isOverdue) {
    return (
      <span className="inline-flex items-center gap-1.5 px-2.5 py-1 rounded-full text-xs font-semibold bg-red-50 text-red-700 border border-red-200 animate-pulse">
        <AlertTriangle className="w-3.5 h-3.5 text-red-600" />
        Quá hạn 48h
      </span>
    );
  }

  const isUrgent = timeLeft.hours < 12;

  return (
    <span
      className={`inline-flex items-center gap-1.5 px-2.5 py-1 rounded-full text-xs font-semibold border ${
        isUrgent
          ? 'bg-rose-50 text-rose-700 border-rose-300'
          : 'bg-amber-50 text-amber-700 border-amber-300'
      }`}
      title={`Hạn phản hồi: ${new Date(dueAtUtc!).toLocaleString('vi-VN')}`}
    >
      <Clock className={`w-3.5 h-3.5 ${isUrgent ? 'text-rose-600 animate-spin' : 'text-amber-600'}`} />
      <span>
        Còn {timeLeft.hours}h {timeLeft.minutes}m
      </span>
    </span>
  );
};
