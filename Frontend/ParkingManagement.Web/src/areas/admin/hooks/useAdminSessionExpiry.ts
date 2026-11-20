import { useEffect, useRef } from 'react';
import { useLocation, useNavigate } from 'react-router-dom';
import { useAppDispatch } from '@/store';
import { logout } from '@/store/slices/authSlice';
import { clearSession } from '@/api/authApi';
import { setAdminUnauthorizedHandler } from '@/api/adminClientApi';
import { createSessionExpiryHandler, loginUrl } from '../utils/sessionExpiry';

/**
 * 401 từ bất kỳ API admin nào (query hay thao tác): xóa phiên rồi về /login?returnUrl=… – chỉ đường dẫn /admin, chỉ một lần.
 * logout() trước để LoginPage không coi là còn đăng nhập rồi đẩy ngược về /admin.
 * LoginPage hiện đọc location.state.from (chưa đọc query returnUrl) nên truyền cả hai.
 */
export const useAdminSessionExpiry = (): void => {
  const navigate = useNavigate();
  const dispatch = useAppDispatch();
  const { pathname, search, hash } = useLocation();
  const pathRef = useRef('');

  useEffect(() => {
    pathRef.current = `${pathname}${search}${hash}`;
  }, [pathname, search, hash]);

  useEffect(() => {
    setAdminUnauthorizedHandler(
      createSessionExpiryHandler({
        getPath: () => pathRef.current,
        onExpire: (returnUrl) => {
          clearSession();
          dispatch(logout());
          navigate(loginUrl(returnUrl), { replace: true, state: { from: { pathname: returnUrl } } });
        },
      }),
    );
    return () => setAdminUnauthorizedHandler(null);
  }, [dispatch, navigate]);
};
