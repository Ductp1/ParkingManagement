import React, { useEffect } from 'react';
import { Outlet, useLocation } from 'react-router-dom';
import { useAppSelector, useAppDispatch } from '@/store';
import { setUser } from '@/store/slices/authSlice';
import type { UserRole, AuthUser } from '@/types/auth';

interface ProtectedRouteProps {
  allowedRoles?: UserRole[];
}

/** Tài khoản độc lập cho từng phân hệ (Khớp danh sách tài khoản seed từ UserService) */
const ROLE_ACCOUNTS: Record<UserRole, AuthUser> = {
  Driver: {
    id: 5,
    fullName: 'Nguyễn Văn An (Tài xế)',
    email: 'driver1@smartparking.vn',
    phone: '0900000005',
    role: 'Driver',
    roles: ['Driver'],
  },
  Owner: {
    id: 2,
    fullName: 'Công ty Vincom Parking (Chủ bãi)',
    email: 'owner.vincom@smartparking.vn',
    phone: '0900000002',
    role: 'Owner',
    roles: ['Owner'],
  },
  Staff: {
    id: 4,
    fullName: 'Lê Văn Cổng (Nhân viên trực cổng)',
    email: 'staff.vincom@smartparking.vn',
    phone: '0900000004',
    role: 'Staff',
    roles: ['Staff'],
  },
  Admin: {
    id: 1,
    fullName: 'Ban Quản Trị Smart Parking (Admin)',
    email: 'admin@smartparking.vn',
    phone: '0900000001',
    role: 'Admin',
    roles: ['Admin'],
  },
};

export const ProtectedRoute: React.FC<ProtectedRouteProps> = () => {
  const { user, isLoading } = useAppSelector((state) => state.auth);
  const location = useLocation();
  const dispatch = useAppDispatch();

  // Xác định vai trò tương ứng với đường dẫn URL mà người dùng gõ
  const getTargetRoleFromPath = (pathname: string): UserRole => {
    if (pathname.startsWith('/admin')) return 'Admin';
    if (pathname.startsWith('/owner')) return 'Owner';
    if (pathname.startsWith('/gate')) return 'Staff';
    return 'Driver';
  };

  const targetRole = getTargetRoleFromPath(location.pathname);

  // Cho phép truy cập trực tiếp qua URL: Tự động khởi tạo phiên tài khoản tương ứng với URL
  useEffect(() => {
    if (!user || user.role !== targetRole) {
      dispatch(setUser(ROLE_ACCOUNTS[targetRole]));
    }
  }, [location.pathname, targetRole, user, dispatch]);

  if (isLoading) {
    return (
      <div className="flex items-center justify-center p-16 text-slate-500 font-medium">
        <span className="material-symbols-outlined text-[24px] animate-spin mr-2">progress_activity</span>
        Đang khởi tạo phân hệ…
      </div>
    );
  }

  return <Outlet />;
};
