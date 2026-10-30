import { LoginPage } from '@/areas/auth/LoginPage';
import { RegisterPage } from '@/areas/auth/RegisterPage';
import { ForgotPasswordPage } from '@/areas/auth/ForgotPasswordPage';
import { ProfilePage } from '@/areas/account/ProfilePage';
import { LandingPage } from '@/areas/public/LandingPage';
import React from 'react';
import { Routes, Route, Navigate } from 'react-router-dom';
import { AppLayout } from '@/components/layout/AppLayout';
import { ProtectedRoute } from './ProtectedRoute';
import { DriverHomePage } from '@/areas/driver/pages/DriverHomePage';
import { VehiclesPage } from '@/areas/driver/pages/VehiclesPage';
import { LotDetailPage } from '@/areas/driver/pages/LotDetailPage';
import { OwnerHomePage } from '@/areas/owner/pages/OwnerHomePage';
import { OwnerKybPage } from '@/areas/owner/pages/OwnerKybPage';
import { GateConsolePage } from '@/areas/gate/pages/GateConsolePage';
import { AdminRoutes } from '@/areas/admin/AdminRoutes';

import { BookingHistoryPage } from '@/areas/driver/pages/BookingHistoryPage';
import { SupportPage } from '@/areas/driver/pages/SupportPage';
import { KycPage } from '@/areas/driver/pages/KycPage';
import { PaymentResultPage } from '@/areas/driver/pages/PaymentResultPage';

export const AppRoutes: React.FC = () => {
  return (
    <Routes>
      {/* 🌐 Public Landing Page (Valet Parking Yards / ParkMaster) */}
      <Route path="/" element={<LandingPage />} />
      <Route path="/login" element={<LoginPage />} />
      <Route path="/register" element={<RegisterPage />} />
      <Route path="/forgot-password" element={<ForgotPasswordPage />} />
      <Route path="/payment/callback" element={<PaymentResultPage />} />
      <Route element={<AppLayout />}>
        <Route element={<ProtectedRoute />}>
          <Route path="/account/profile" element={<ProfilePage />} />
        </Route>

        {/* 🚗 1. Driver Portal (/driver/*) */}
        <Route element={<ProtectedRoute allowedRoles={['Driver', 'Admin']} />}>
          <Route path="/driver">
            <Route index element={<DriverHomePage />} />
            <Route path="vehicles" element={<VehiclesPage />} />
            <Route path="parking-lots" element={<LotDetailPage />} />
            <Route path="parking-lots/:id" element={<LotDetailPage />} />
            <Route path="history" element={<BookingHistoryPage />} />
            <Route path="support" element={<SupportPage />} />
            <Route path="kyc" element={<KycPage />} />
            <Route path="payment-result" element={<PaymentResultPage />} />
          </Route>
        </Route>

        {/* 🏢 2. Owner Portal (/owner/*) */}
        <Route element={<ProtectedRoute allowedRoles={['Owner', 'Admin']} />}>
          <Route path="/owner" element={<OwnerHomePage />} />
          <Route path="/owner/kyb" element={<OwnerKybPage />} />
          <Route path="/owner/*" element={<OwnerHomePage />} />
        </Route>

        {/* 🚧 3. Gate Console Portal (/gate/*) */}
        <Route element={<ProtectedRoute allowedRoles={['Staff', 'Admin']} />}>
          <Route path="/gate" element={<GateConsolePage />} />
          <Route path="/gate/*" element={<GateConsolePage />} />
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

      {/* ⚙️ 4. Admin Portal (/admin/*) – shell riêng (sidebar + header), không dùng Navbar/Footer của AppLayout */}
      <Route element={<ProtectedRoute allowedRoles={['Admin']} />}>
        <Route path="/admin/*" element={<AdminRoutes />} />
      </Route>
    </Routes>
  );
};
