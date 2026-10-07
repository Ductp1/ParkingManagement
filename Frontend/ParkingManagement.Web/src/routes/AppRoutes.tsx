import React from 'react';
import { Routes, Route, Navigate } from 'react-router-dom';
import { AppLayout } from '@/components/layout/AppLayout';
import { ProtectedRoute } from './ProtectedRoute';
import { DriverHomePage } from '@/areas/driver/pages/DriverHomePage';
import { VehiclesPage } from '@/areas/driver/pages/VehiclesPage';
import { LotDetailPage } from '@/areas/driver/pages/LotDetailPage';
import { OwnerHomePage } from '@/areas/owner/pages/OwnerHomePage';
import { GateConsolePage } from '@/areas/gate/pages/GateConsolePage';
import { AdminDashboardPage } from '@/areas/admin/pages/AdminDashboardPage';

export const AppRoutes: React.FC = () => {
  return (
    <Routes>
      <Route element={<AppLayout />}>
        {/* Root redirect to driver portal */}
        <Route path="/" element={<Navigate to="/driver" replace />} />

        {/* 🚗 1. Driver Portal (/driver/*) */}
        <Route path="/driver">
          <Route index element={<DriverHomePage />} />
          <Route path="vehicles" element={<VehiclesPage />} />
          <Route path="parking-lots" element={<LotDetailPage />} />
          <Route path="parking-lots/:id" element={<LotDetailPage />} />
          <Route
            path="history"
            element={
              <div className="max-w-4xl mx-auto py-16 text-center text-slate-500">
                <span className="material-symbols-outlined text-[48px] text-slate-400 mb-2">history</span>
                <h2 className="text-xl font-bold text-slate-800">Lịch sử đặt chỗ</h2>
                <p className="text-sm mt-1">Danh sách vé xe và phiên gửi xe trước đây.</p>
              </div>
            }
          />
          <Route
            path="support"
            element={
              <div className="max-w-4xl mx-auto py-16 text-center text-slate-500">
                <span className="material-symbols-outlined text-[48px] text-slate-400 mb-2">support_agent</span>
                <h2 className="text-xl font-bold text-slate-800">Trung tâm hỗ trợ CSKH 24/7</h2>
                <p className="text-sm mt-1">Hotline: 1900 6868 hoặc gửi ticket khiếu nại (US-088).</p>
              </div>
            }
          />
        </Route>

        {/* 🏢 2. Owner Portal (/owner/*) */}
        <Route element={<ProtectedRoute allowedRoles={['Owner', 'Admin', 'Driver']} />}>
          <Route path="/owner" element={<OwnerHomePage />} />
        </Route>

        {/* 🚧 3. Gate Console Portal (/gate/*) */}
        <Route element={<ProtectedRoute allowedRoles={['Staff', 'Admin', 'Driver']} />}>
          <Route path="/gate" element={<GateConsolePage />} />
        </Route>

        {/* ⚙️ 4. Admin Portal (/admin/*) */}
        <Route element={<ProtectedRoute allowedRoles={['Admin', 'Driver']} />}>
          <Route path="/admin" element={<AdminDashboardPage />} />
        </Route>

        {/* 404 Fallback */}
        <Route
          path="*"
          element={
            <div className="max-w-4xl mx-auto py-20 text-center">
              <h1 className="text-6xl font-extrabold text-blue-600">404</h1>
              <p className="text-lg text-slate-600 mt-2 font-medium">Trang không tồn tại</p>
              <Navigate to="/driver" replace />
            </div>
          }
        />
      </Route>
    </Routes>
  );
};
