import React from 'react';
import { AdminIcon } from './AdminIcon';
import { scaleIcon } from './adminIcons';
import { AdminButton } from './AdminButton';

export type StateBlockType = 'empty' | 'error' | 'noPermission' | 'loading' | 'unavailable' | 'notFound' | 'offline' | 'sessionExpired';

interface StateBlockProps {
  type: StateBlockType;
  title?: string;
  message?: string;
  onRetry?: () => void;
  /** Nhãn nút thử lại (mặc định "Retry"; màn tiếng Việt truyền "Thử lại"). */
  retryLabel?: string;
  /** Bỏ viền khi nằm trong panel/bảng đã có viền. */
  bare?: boolean;
  className?: string;
}

const DEFAULT_COPY: Record<Exclude<StateBlockType, 'loading'>, { title: string; message: string }> = {
  empty: { title: 'Nothing here yet', message: 'No records match the current filters.' },
  error: { title: 'Something went wrong', message: 'The service did not respond. Try again.' },
  noPermission: {
    title: 'You do not have access',
    message: 'This area requires the Admin role (UserRoles.Role = Admin). Ask an administrator to grant access.',
  },
  unavailable: {
    title: 'Data source not available yet',
    message: 'The backend endpoint for this screen is not implemented yet, so nothing is shown.',
  },
  notFound: { title: 'Not found', message: 'This record does not exist or has been removed.' },
  offline: { title: 'You are offline', message: 'Check your internet connection, then try again.' },
  sessionExpired: { title: 'Your session has ended', message: 'Taking you to the sign-in page…' },
};

const ICON: Record<Exclude<StateBlockType, 'loading'>, Parameters<typeof scaleIcon>[0]> = {
  empty: 'inbox',
  unavailable: 'inbox',
  error: 'alert',
  offline: 'alert',
  noPermission: 'ban',
  notFound: 'search',
  sessionExpired: 'lock',
};

/** Trạng thái có nút thử lại khi màn truyền onRetry. */
const RETRYABLE = new Set<StateBlockType>(['error', 'offline']);

/** Shell/State block: Empty / Error + Retry / Offline + Retry / Not found / No permission / Session ended / Loading skeleton. */
export const StateBlock: React.FC<StateBlockProps> = ({ type, title, message, onRetry, retryLabel = 'Retry', bare, className }) => {
  const frame = `${bare ? '' : 'border border-(--admin-border-default)'} bg-(--admin-surface-card) ${className ?? ''}`;

  if (type === 'loading') {
    return (
      <div className={`flex flex-col gap-3 p-4 ${frame}`} aria-busy="true" aria-label="Loading">
        {['100%', '76%', '100%', '88%', '65%', '100%'].map((w, i) => (
          <span key={i} className="adm-skeleton block h-3.5 rounded-[2px]" style={{ width: w }} />
        ))}
      </div>
    );
  }

  const copy = DEFAULT_COPY[type];
  const icon = ICON[type];
  const assertive = type === 'error' || type === 'offline' || type === 'sessionExpired';
  return (
    <div
      role={assertive ? 'alert' : 'status'}
      className={`adm-motion-fade flex min-h-[220px] flex-col items-center justify-center gap-2 px-6 py-8 text-center ${frame}`}
    >
      <span className="text-(--admin-text-secondary)" aria-hidden="true">
        <AdminIcon icon={scaleIcon(icon, 32)} />
      </span>
      <p className="adm-type-body-strong-13 m-0 text-(--admin-text-primary)">{title ?? copy.title}</p>
      <p className="adm-type-body-regular-12 m-0 max-w-[560px] text-(--admin-text-secondary)">{message ?? copy.message}</p>
      {RETRYABLE.has(type) && onRetry && (
        <AdminButton variant="secondary" icon="refresh" onClick={onRetry} className="mt-1">
          {retryLabel}
        </AdminButton>
      )}
    </div>
  );
};
