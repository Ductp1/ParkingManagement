import React from 'react';

export type TimelineState = 'done' | 'current' | 'upcoming' | 'danger';

export interface TimelineEntry {
  key: string;
  title: string;
  meta?: string;
  state: TimelineState;
}

const DOT: Record<TimelineState, string> = {
  done: 'bg-(--admin-status-success)',
  current: 'bg-(--admin-brand-navy-900)',
  upcoming: 'bg-(--admin-border-default)',
  danger: 'bg-(--admin-status-danger)',
};

/** Shell/Timeline item: rail chấm 10px + vạch 2px, tiêu đề body/strong, dòng phụ body/small. */
export const Timeline: React.FC<{ entries: TimelineEntry[] }> = ({ entries }) => (
  <ol className="m-0 flex list-none flex-col p-0">
    {entries.map((entry, i) => (
      <li key={entry.key} className="flex gap-2.5">
        <div className="flex w-2.5 shrink-0 flex-col items-center gap-0.5 pt-1">
          <span className={`size-2.5 shrink-0 rounded-full ${DOT[entry.state]}`} />
          {i < entries.length - 1 && <span className="w-0.5 flex-1 bg-(--admin-border-default)" />}
        </div>
        <div className="flex min-w-0 flex-col gap-0.5 pb-3">
          <span
            className={`adm-type-body-strong ${entry.state === 'upcoming' ? 'text-(--admin-text-secondary)' : 'text-(--admin-text-primary)'}`}
          >
            {entry.title}
          </span>
          <span className="adm-type-body-small text-(--admin-text-secondary)">{entry.meta ?? '—'}</span>
        </div>
      </li>
    ))}
  </ol>
);
