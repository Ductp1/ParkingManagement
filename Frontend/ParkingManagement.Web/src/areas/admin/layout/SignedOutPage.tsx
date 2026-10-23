import React from 'react';
import { useNavigate } from 'react-router-dom';
import { useAppDispatch } from '@/store';
import { logout } from '@/store/slices/authSlice';
import { AdminButton } from '../components/AdminButton';
import '../theme/admin.css';

/** Global / Shell / Signed out (Figma): màn sau khi đăng xuất; "Sign in again" xóa phiên trong store rồi về trang chủ. */
export const SignedOutPage: React.FC = () => {
  const dispatch = useAppDispatch();
  const navigate = useNavigate();
  return (
    <div className="admin-portal flex min-h-screen items-center justify-center">
      <div className="flex w-[360px] flex-col items-center gap-4 border border-(--admin-border-default) bg-(--admin-surface-card) p-8 text-center">
        <span className="adm-type-nav-logo uppercase text-(--admin-brand-navy-900)">Smart Parking Admin</span>
        <p className="adm-type-body-default m-0 text-(--admin-text-primary)">You have signed out.</p>
        <AdminButton
          onClick={() => {
            dispatch(logout());
            navigate('/', { replace: true });
          }}
        >
          Sign in again
        </AdminButton>
      </div>
    </div>
  );
};
