import React from 'react';
import { AdminIcon } from './AdminIcon';
import { scaleIcon } from './adminIcons';
import { AdminButton } from './AdminButton';

export type StateBlockType = 'empty' | 'error' | 'noPermission' | 'loading' | 'unavailable';

interface StateBlockProps {
  type: StateBlockType;
  title?: string;
  message?: string;
  onRetry?: () => void;
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
};

/** Shell/State block: Empty / Error + Retry / No permission / Loading skeleton. */
export const StateBlock: React.FC<StateBlockProps> = ({ type, title, message, onRetry, bare, className }) => {
  const frame = `${bare ? '' : 'border border-(--admin-border-default)'} bg-(--admin-surface-card) ${className ?? ''}`;

  if (type === 'loading') {
    return (
      <div className={`flex flex-col gap-3 p-4 ${frame}`} aria-busy="true" aria-label="Loading">
        {['100%', '76%', '100%', '88%', '65%', '100%'].map((w, i) => (
          <span key={i} className="block h-3.5 animate-pulse rounded-[2px] bg-(--admin-surface-selected)" style={{ width: w }} />
        ))}
      </div>
    );
  }

  const copy = DEFAULT_COPY[type];
  const icon = type === 'error' ? 'alert' : type === 'noPermission' ? 'ban' : 'inbox';
  return (
    <div className={`flex min-h-[220px] flex-col items-center justify-center gap-2 px-6 py-8 text-center ${frame}`}>
      <span className="text-(--admin-text-secondary)">
        <AdminIcon icon={scaleIcon(icon, 32)} />
      </span>
      <p className="adm-type-body-strong-13 m-0 text-(--admin-text-primary)">{title ?? copy.title}</p>
      <p className="adm-type-body-regular-12 m-0 max-w-[560px] text-(--admin-text-secondary)">{message ?? copy.message}</p>
      {type === 'error' && onRetry && (
        <AdminButton variant="secondary" icon="refresh" onClick={onRetry} className="mt-1">
          Retry
        </AdminButton>
      )}
    </div>
  );
};
