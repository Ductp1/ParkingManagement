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

import { BookingHistoryPage } from '@/areas/driver/pages/BookingHistoryPage';
import { SupportPage } from '@/areas/driver/pages/SupportPage';

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
          <Route path="history" element={<BookingHistoryPage />} />
          <Route path="support" element={<SupportPage />} />
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
