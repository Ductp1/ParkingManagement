import React from 'react';
import { Outlet } from 'react-router-dom';
import { MainNavbar } from './MainNavbar';
import { MainFooter } from './MainFooter';
import { useNotificationHub } from '@/realtime/useNotificationHub';
import { NotificationToaster } from '@/components/ui/NotificationToaster';

export const AppLayout: React.FC = () => {
  // Kết nối SignalR /hubs/notify: nhận thông báo real-time trên mọi trang (không cần F5).
  useNotificationHub();

  return (
    <div className="min-h-screen flex flex-col bg-slate-50 text-slate-900 font-sans">
      <MainNavbar />
      <main className="flex-1 w-full">
        <Outlet />
      </main>
      <MainFooter />
      {/* Popup thông báo real-time (tự ẩn sau >= 10s) */}
      <NotificationToaster />
    </div>
  );
};
