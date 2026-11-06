import { useEffect } from 'react';
import { restoreSession, clearSession } from '@/api/authApi';
import { useAppDispatch } from '@/store';
import { setUser, setToken, setLoading } from '@/store/slices/authSlice';

export function SessionBootstrap() {
  const dispatch = useAppDispatch();
  useEffect(() => {
    let disposed = false;
    if (!sessionStorage.getItem('parkmaster_token')) { dispatch(setLoading(false)); return; }
    restoreSession().then(user => {
      if (!disposed) {
        dispatch(setToken(sessionStorage.getItem('parkmaster_token')));
        dispatch(setUser(user));
      }
    }).catch(() => {
      if (!disposed) { clearSession(); dispatch(setToken(null)); dispatch(setUser(null)); }
    }).finally(() => { if (!disposed) dispatch(setLoading(false)); });
    return () => { disposed = true; };
  }, [dispatch]);
  return null;
}
