import React from 'react';
import { Link } from 'react-router-dom';
import type { AuditFieldChangeDto, AuditLogDetailDto } from '@/types/adminAuditLogs';
import { Banner } from '../../components/Banner';
import { DetailList, DetailRow } from '../../components/DetailRow';
import { formatDateTimeIct } from '../../utils/format';
import {
  OWN_SERVICE,
  auditChangeTone,
  auditEntityLink,
  auditLogListLink,
  auditUserLink,
  auditUserText,
  auditValueText,
  type AuditChangeTone,
} from './auditLogFormat';

const TONE: Record<AuditChangeTone, { label: string; dot: string; row: string }> = {
  added: {
    label: 'Added',
    dot: 'bg-(--admin-status-success)',
    row: 'bg-[color-mix(in_srgb,var(--admin-status-success)_6%,transparent)]',
  },
  removed: {
    label: 'Removed',
    dot: 'bg-(--admin-status-danger)',
    row: 'bg-[color-mix(in_srgb,var(--admin-status-danger)_6%,transparent)]',
  },
  modified: {
    label: 'Modified',
    dot: 'bg-(--admin-status-warning)',
    row: 'bg-[color-mix(in_srgb,var(--admin-status-warning)_6%,transparent)]',
  },
};

const InlineLink: React.FC<{ to: string | null; children: React.ReactNode }> = ({ to, children }) =>
  to ? (
    <Link to={to} className="adm-type-link-default text-(--admin-brand-navy-900) hover:underline">
      {children}
    </Link>
  ) : (
    <>{children}</>
  );

/** AdminService ghi trực tiếp (US-096 / US-098 / US-116); service khác gửi qua sự kiện AuditRecorded (T-802). */
export const AuditOriginNote: React.FC<{ log: AuditLogDetailDto }> = ({ log }) =>
  log.sourceService === OWN_SERVICE ? (
    <Banner tone="info">Recorded directly by AdminService together with the change. Append-only: it cannot be edited or deleted.</Banner>
  ) : (
    <Banner tone="info">
      Sent by {log.sourceService} as an AuditRecorded event and stored by AdminService at the time the action happened. Append-only: it cannot be
      edited or deleted.
    </Banner>
  );

/** Bảng trường thay đổi: chấm màu + nền nhạt theo loại, giá trị cũ gạch ngang khi bị sửa / xóa. Dòng hiện so le. */
export const AuditChangesTable: React.FC<{ changes: AuditFieldChangeDto[] }> = ({ changes }) =>
  changes.length === 0 ? (
    <p className="adm-type-body-regular-12 adm-motion-fade m-0 px-3 py-4 text-(--admin-text-secondary)">
      No field changes – old and new values are the same or were not recorded.
    </p>
  ) : (
    <div className="overflow-x-auto">
      <table className="w-full border-collapse">
        <thead>
          <tr>
            {['Field', 'Change', 'Old value', 'New value'].map((h) => (
              <th
                key={h}
                scope="col"
                className="adm-type-table-header h-7 border-r border-(--admin-table-header-border) bg-(--admin-table-header) px-3 text-left whitespace-nowrap uppercase text-(--admin-text-on-navy) last:border-r-0"
              >
                {h}
              </th>
            ))}
          </tr>
        </thead>
        <tbody>
          {changes.map((c, i) => {
            const tone = auditChangeTone(c.kind);
            const t = TONE[tone];
            return (
              <tr
                key={c.field}
                className={`adm-motion-rise border-b border-(--admin-border-default) ${t.row}`}
                style={{ '--adm-delay': `calc(var(--stagger-step) * ${Math.min(i, 7)})` } as React.CSSProperties}
              >
                <td className="adm-type-body-mono px-3 py-2 align-top break-all text-(--admin-text-primary)">{c.field}</td>
                <td className="px-3 py-2 align-top whitespace-nowrap">
                  <span className="adm-type-status-label inline-flex items-center gap-1.5 text-(--admin-text-primary)">
                    <span className={`h-2 w-2 rounded-full ${t.dot}`} aria-hidden="true" />
                    {t.label}
                  </span>
                </td>
                <td className="adm-type-body-mono px-3 py-2 align-top break-all text-(--admin-text-secondary)">
                  <span className={tone === 'added' ? '' : 'line-through decoration-(--admin-status-danger)/60'}>
                    {tone === 'added' ? '—' : auditValueText(c.oldValue)}
                  </span>
                </td>
                <td className="adm-type-body-mono px-3 py-2 align-top break-all text-(--admin-text-primary)">
                  {tone === 'removed' ? '—' : auditValueText(c.newValue)}
                </td>
              </tr>
            );
          })}
        </tbody>
      </table>
    </div>
  );

export const AuditJsonBlock: React.FC<{ value: unknown }> = ({ value }) =>
  value === null || value === undefined ? (
    <span className="adm-type-body-regular-12 text-(--admin-text-secondary)">— not recorded</span>
  ) : (
    <pre className="adm-type-body-mono adm-motion-fade m-0 max-h-[360px] overflow-auto rounded-[2px] bg-(--admin-surface-page) p-3 whitespace-pre-wrap break-all text-(--admin-text-primary)">
      {JSON.stringify(value, null, 2)}
    </pre>
  );

/** Thông tin dòng audit + link tới bản ghi, người thực hiện và các audit liên quan. */
export const AuditEntryList: React.FC<{ log: AuditLogDetailDto }> = ({ log }) => {
  const entityLink = auditEntityLink(log.entityName, log.entityId);
  return (
    <DetailList>
      <DetailRow label="At">{formatDateTimeIct(log.createdAtUtc)}</DetailRow>
      <DetailRow label="Source service">{log.sourceService}</DetailRow>
      <DetailRow label="User">
        <InlineLink to={auditUserLink(log.userId)}>{auditUserText(log.userId)}</InlineLink>
      </DetailRow>
      <DetailRow label="Owner profile">{log.ownerProfileId !== null ? `#${log.ownerProfileId}` : '—'}</DetailRow>
      <DetailRow label="Action">
        <span className="adm-type-body-mono">{log.action}</span>
      </DetailRow>
      <DetailRow label="Entity">
        <InlineLink to={entityLink}>
          {log.entityName}
          {log.entityId ? ` #${log.entityId}` : ''}
        </InlineLink>
      </DetailRow>
      <DetailRow label="Reason">{log.reason ?? '—'}</DetailRow>
      <DetailRow label="IP address">{log.ipAddress ?? '—'}</DetailRow>
    </DetailList>
  );
};

/** Lối tắt sang danh sách đã lọc: cùng bản ghi / cùng người thực hiện / cùng chủ bãi. */
export const AuditRelatedLinks: React.FC<{ log: AuditLogDetailDto }> = ({ log }) => {
  const links = [
    log.entityId && { to: auditLogListLink({ entity: log.entityName, entityId: log.entityId }), label: `All entries of ${log.entityName} #${log.entityId}` },
    log.userId !== null && { to: auditLogListLink({ userId: log.userId }), label: `All entries by ${auditUserText(log.userId)}` },
    log.ownerProfileId !== null && { to: auditLogListLink({ ownerProfileId: log.ownerProfileId }), label: `All entries of owner #${log.ownerProfileId}` },
  ].filter((l): l is { to: string; label: string } => Boolean(l));
  if (links.length === 0) return <span className="adm-type-body-regular-12 text-(--admin-text-secondary)">—</span>;
  return (
    <ul className="m-0 flex list-none flex-col gap-2 p-0">
      {links.map((l) => (
        <li key={l.to}>
          <Link to={l.to} className="adm-type-link-default text-(--admin-brand-navy-900) hover:underline">
            {l.label} →
          </Link>
        </li>
      ))}
    </ul>
  );
};
