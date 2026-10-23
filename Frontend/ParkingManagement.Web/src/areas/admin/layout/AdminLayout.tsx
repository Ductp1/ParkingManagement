import React, { useState } from 'react';
import { Navigate, Outlet, useLocation, useNavigate } from 'react-router-dom';
import { useAppDispatch, useAppSelector } from '@/store';
import { setToken } from '@/store/slices/authSlice';
import { adminUsersApi } from '@/api/adminUsersApi';
import { adminCommunicationsApi } from '@/api/adminCommunicationsApi';
import type { NotificationDto } from '@/types/notification';
import { ADMIN_HOME_PATH, resolveAdminLocation } from '../navigation';
import type { AdminOutletContext } from '../hooks/useAdminBreadcrumb';
import { useAdminQuery } from '../hooks/useAdminQuery';
import { AdminSidebar } from './AdminSidebar';
import { AdminHeader } from './AdminHeader';
import { AdminToastProvider } from '../components/AdminToastProvider';
import { AdminDialog } from '../components/AdminDialog';
import { humanizeEnum } from '../utils/enumLabels';
import '../theme/admin.css';

/**
 * Khung Admin Portal (Figma frame 1440): Sidebar 220 | Header 92 + Main (padding 16, gap 16).
 * Token màu/chữ scope trong .admin-portal nên không ảnh hưởng area driver/owner/gate.
 * Người dùng hiện tại lấy từ GET /api/v1/users/me (fallback: user trong store khi API lỗi).
 */
export const AdminLayout: React.FC = () => {
  const { pathname } = useLocation();
  const navigate = useNavigate();
  const dispatch = useAppDispatch();
  const storeUser = useAppSelector((state) => state.auth.user);
  const [detailLabel, setDetailLabel] = useState<string | null>(null);
  const [signOutOpen, setSignOutOpen] = useState(false);

  const me = useAdminQuery(() => adminUsersApi.getCurrentUser(), []);
  const userId = me.data?.id ?? storeUser?.id ?? null;
  const inbox = useAdminQuery(userId ? () => adminCommunicationsApi.getInbox(userId) : null, [userId]);

  const location = resolveAdminLocation(pathname);
  if (!location) return <Navigate to={ADMIN_HOME_PATH} replace />;
  if (!location.tab) return <Navigate to={location.section.tabs[0].path} replace />;

  const outletContext: AdminOutletContext = { setDetailLabel };
  const name = me.data?.fullName ?? storeUser?.fullName ?? '';
  const email = me.data?.email ?? storeUser?.email ?? null;
  const roles = (me.data?.roles ?? (storeUser ? [storeUser.role] : [])).map(humanizeEnum).join(', ');

  const readOne = async (n: NotificationDto) => {
    if (!n.isRead && userId) {
      await adminCommunicationsApi.markAsRead(n.id, userId).catch(() => undefined);
      inbox.refetch();
    }
  };
  const readAll = async () => {
    if (!userId) return;
    const unread = inbox.data?.items.items.filter((n) => !n.isRead) ?? [];
    await Promise.allSettled(unread.map((n) => adminCommunicationsApi.markAsRead(n.id, userId)));
    inbox.refetch();
  };

  return (
    <AdminToastProvider>
      <div className="admin-portal flex min-h-screen">
        <AdminSidebar activeSection={location.section} userName={name} userEmail={email ?? undefined} />
        <div className="flex min-w-0 flex-1 flex-col">
          <AdminHeader
            section={location.section}
            activeTab={location.tab}
            detailLabel={detailLabel}
            inbox={inbox}
            onReadNotification={readOne}
            onReadAll={readAll}
            profile={{ name, email, roles, profilePath: userId ? `/admin/operations/users/${userId}` : null }}
            onSignOut={() => setSignOutOpen(true)}
          />
          <main className="flex min-w-0 flex-1 flex-col gap-4 p-4">
            <Outlet context={outletContext} />
          </main>
        </div>
      </div>
      <AdminDialog
        open={signOutOpen}
        title="Sign out?"
        description="Your session token is removed from this browser and you return to the signed-out page."
        onClose={() => setSignOutOpen(false)}
        onConfirm={() => {
          dispatch(setToken(null));
          navigate('/admin/signed-out', { replace: true });
        }}
        confirmLabel="Sign out"
        destructive
        footerNote="Unsaved changes in open dialogs will be lost."
      />
    </AdminToastProvider>
  );
};
