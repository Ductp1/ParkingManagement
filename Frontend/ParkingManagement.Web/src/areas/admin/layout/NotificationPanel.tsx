import React from 'react';
import type { NotificationDto, NotificationInboxDto } from '@/types/notification';
import type { AdminQueryResult } from '../hooks/useAdminQuery';
import { QueryState } from '../components/PageLayout';
import { formatShortDateTime } from '../utils/format';

interface NotificationPanelProps {
  inbox: AdminQueryResult<NotificationInboxDto>;
  onRead: (n: NotificationDto) => void;
  onReadAll: () => void;
}

/** Shell/Notification panel (Figma 142:334): dropdown 360 dưới chuông – InApp Notifications của admin. */
export const NotificationPanel: React.FC<NotificationPanelProps> = ({ inbox, onRead, onReadAll }) => {
  const items = inbox.data?.items.items ?? [];
  return (
    <div className="adm-dropdown absolute top-10 right-0 z-40 flex w-[360px] flex-col rounded-[4px] border border-(--admin-border-default) bg-(--admin-surface-card) shadow-[0_4px_12px_rgb(0_0_0/0.12)]">
      <div className="flex h-10 items-center justify-between border-b border-(--admin-border-default) px-3">
        <span className="adm-type-panel-title uppercase text-(--admin-text-secondary)">Notifications</span>
        {(inbox.data?.unreadCount ?? 0) > 0 && (
          <button type="button" onClick={onReadAll} className="adm-type-link-small text-(--admin-brand-navy-900) hover:underline">
            Mark all as read
          </button>
        )}
      </div>
      <div className="max-h-[360px] overflow-y-auto">
        {inbox.status === 'ready' && items.length > 0 ? (
          items.map((n) => (
            <button
              key={n.id}
              type="button"
              onClick={() => onRead(n)}
              className={`flex w-full gap-2.5 border-b border-(--admin-border-default) px-3 py-2.5 text-left last:border-b-0 ${n.isRead ? '' : 'bg-(--admin-surface-selected)'}`}
            >
              <span className={`mt-1.5 size-2 shrink-0 rounded-full ${n.isRead ? 'bg-(--admin-border-default)' : 'bg-(--admin-brand-navy-900)'}`} />
              <span className="flex min-w-0 flex-col gap-0.5">
                <span className="adm-type-body-strong text-(--admin-text-primary)">{n.title}</span>
                <span className="adm-type-body-regular-12 text-(--admin-text-secondary)">{n.body}</span>
                <span className="adm-type-body-small text-(--admin-text-placeholder)">{formatShortDateTime(n.createdAtUtc)}</span>
              </span>
            </button>
          ))
        ) : (
          <QueryState
            status={inbox.status === 'ready' ? 'empty' : inbox.status}
            error={inbox.error}
            onRetry={inbox.refetch}
            copy={{ emptyTitle: 'No notifications', emptyMessage: 'New admin notifications appear here.', errorTitle: 'Could not load notifications', service: 'NotificationService' }}
          />
        )}
      </div>
    </div>
  );
};
