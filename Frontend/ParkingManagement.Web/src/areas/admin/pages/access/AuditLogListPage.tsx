import React, { useState } from 'react';
import { Link } from 'react-router-dom';
import { adminAuditLogsApi } from '@/api/adminAuditLogsApi';
import type { AuditLogFacetDto, AuditLogListQuery, AuditLogSummaryDto } from '@/types/adminAuditLogs';
import { ListScreen, QueryState } from '../../components/PageLayout';
import { FilterColumn, FilterPanel } from '../../components/FilterColumn';
import { SearchBox, ToolbarRow, ToolbarSelect } from '../../components/Toolbar';
import { CellMono, CellMuted, CellText, CellTwoLine, CellView, DataTable, type DataTableColumn } from '../../components/DataTable';
import { Pagination } from '../../components/Pagination';
import { AdminButton } from '../../components/AdminButton';
import { AdminDrawer } from '../../components/AdminDrawer';
import { AdminIcon } from '../../components/AdminIcon';
import { ADMIN_ICONS } from '../../components/adminIcons';
import { useAdminQuery } from '../../hooks/useAdminQuery';
import { useAdminToast } from '../../hooks/useAdminToast';
import { DEFAULT_PAGE_SIZE, useListParams } from '../../hooks/useListParams';
import { formatDateTime, formatDateTimeIct, formatNumber } from '../../utils/format';
import { saveFile } from '../operations/lot-contracts/lotContractFormat';
import { AUDIT_LOG_BASE, OWN_SERVICE, auditUserText } from './auditLogFormat';
import { AuditChangesTable, AuditEntryList, AuditOriginNote, AuditRelatedLinks } from './AuditLogDetailParts';

/** Ngày (YYYY-MM-DD) theo giờ ICT → ISO UTC; to = 00:00 ngày kế tiếp vì API coi to là mốc không bao gồm. */
const ictDayStartUtc = (day: string, addDays = 0): string | null => {
  if (!/^\d{4}-\d{2}-\d{2}$/.test(day)) return null;
  const d = new Date(`${day}T00:00:00+07:00`);
  if (Number.isNaN(d.getTime())) return null;
  d.setUTCDate(d.getUTCDate() + addDays);
  return d.toISOString();
};

const positiveInt = (value: string): number | null => (/^[1-9]\d*$/.test(value) ? Number(value) : null);

const facetOptions = (facets: AuditLogFacetDto[] | undefined) => [
  { value: '', label: 'All' },
  ...(facets ?? []).map((f) => ({ value: f.value, label: `${f.value} (${formatNumber(f.count)})` })),
];

/** Tham số URL do màn khác truyền sang (user, KYB, đối tác…): không có ô nhập riêng nên hiện thành chip gỡ được. */
const LINKED_FILTERS = [
  { param: 'entityId', label: (v: string) => `Entity ID ${v}` },
  { param: 'userId', label: (v: string) => `User U#${v}` },
  { param: 'ownerProfileId', label: (v: string) => `Owner #${v}` },
] as const;

/** Ô chọn ngày cùng kiểu với SearchBox (32px, viền border/default). */
const DayBox: React.FC<{ label: string; value: string; onChange: (value: string) => void }> = ({ label, value, onChange }) => (
  <label className="flex h-8 items-center gap-2 rounded-[2px] border border-(--admin-border-default) bg-(--admin-surface-card) px-2.5 text-(--admin-text-secondary) adm-tr-colors focus-within:border-(--admin-brand-navy-900)">
    <span className="adm-type-body-regular-12">{label}</span>
    <input
      type="date"
      aria-label={`${label} (ICT)`}
      value={value}
      onChange={(e) => onChange(e.target.value)}
      className="adm-type-body-regular-12 bg-transparent text-(--admin-text-primary) outline-none"
    />
  </label>
);

const FilterChip: React.FC<{ label: string; onRemove: () => void }> = ({ label, onRemove }) => (
  <span className="adm-motion-pop adm-type-body-regular-12 inline-flex h-8 items-center gap-1.5 rounded-[2px] border border-(--admin-brand-navy-800) bg-(--admin-surface-selected) pr-1.5 pl-2.5 text-(--admin-brand-navy-900)">
    {label}
    <button
      type="button"
      aria-label={`Remove filter ${label}`}
      onClick={onRemove}
      className="flex h-5 w-5 items-center justify-center rounded-[2px] adm-tr-colors hover:bg-(--admin-surface-card)"
    >
      <AdminIcon icon={ADMIN_ICONS.x} />
    </button>
  </span>
);

/** Tên service + nhãn nhỏ cho dòng đến từ service khác qua sự kiện AuditRecorded (T-802). */
const ServiceCell: React.FC<{ service: string }> = ({ service }) => (
  <CellTwoLine primary={service} secondary={service === OWN_SERVICE ? 'Direct' : 'AuditRecorded event'} />
);

const columns: DataTableColumn<AuditLogSummaryDto>[] = [
  { key: 'at', header: 'At (ICT)', width: 140, render: (r) => <CellMuted>{formatDateTime(r.createdAtUtc)}</CellMuted> },
  { key: 'service', header: 'Service', width: 160, render: (r) => <ServiceCell service={r.sourceService} /> },
  {
    key: 'user',
    header: 'User',
    width: 110,
    render: (r) => <CellTwoLine primary={auditUserText(r.userId)} secondary={r.ownerProfileId !== null ? `Owner #${r.ownerProfileId}` : undefined} />,
  },
  { key: 'action', header: 'Action', width: 220, render: (r) => <CellMono>{r.action}</CellMono> },
  { key: 'entity', header: 'Entity', width: 150, render: (r) => <CellText>{r.entityName}</CellText> },
  { key: 'entityId', header: 'Entity ID', width: 100, render: (r) => <CellMono>{r.entityId ?? '—'}</CellMono> },
  { key: 'reason', header: 'Reason', render: (r) => <CellMuted>{r.reason ?? '—'}</CellMuted> },
  { key: 'actions', header: '', width: 74, render: (r) => <CellView to={`${AUDIT_LOG_BASE}/${r.id}`} /> },
];

/** Xem nhanh một dòng trong slide-over, giữ nguyên danh sách + bộ lọc phía sau (?log= trên URL nên F5 / Back vẫn giữ). */
const AuditLogPreview: React.FC<{ id: number; onClose: () => void }> = ({ id, onClose }) => {
  const query = useAdminQuery(() => adminAuditLogsApi.getById(id), [id]);
  const log = query.data;
  return (
    <AdminDrawer
      open
      width={560}
      title={`Audit log #${id}`}
      description={log ? `${log.action} · ${formatDateTimeIct(log.createdAtUtc)}` : undefined}
      actions={
        <Link
          to={`${AUDIT_LOG_BASE}/${id}`}
          className="adm-type-link-default inline-flex h-8 items-center rounded-[2px] px-2 text-(--admin-brand-navy-900) adm-tr-colors hover:bg-(--admin-surface-selected)"
        >
          Open full page
        </Link>
      }
      onClose={onClose}
    >
      {!log ? (
        <QueryState
          status={query.status}
          error={query.error}
          onRetry={query.refetch}
          copy={{ emptyTitle: 'Audit entry not found', emptyMessage: 'It may have been archived.', errorTitle: 'Could not load the audit entry', service: 'AdminService' }}
        />
      ) : (
        <div className="adm-motion-fade flex flex-col gap-4">
          <AuditOriginNote log={log} />
          <section className="flex flex-col border border-(--admin-border-default)">
            <h3 className="adm-type-panel-title m-0 border-b border-(--admin-border-default) px-3 py-2 uppercase text-(--admin-text-secondary)">
              Changed fields
            </h3>
            <AuditChangesTable changes={log.changes} />
          </section>
          <section className="flex flex-col border border-(--admin-border-default) p-3">
            <AuditEntryList log={log} />
          </section>
          <AuditRelatedLinks log={log} />
        </div>
      )}
    </AdminDrawer>
  );
};

/**
 * Access & Configuration / Audit Log (US-100): GET /api/v1/admin/audit-logs – lọc, tìm, phân trang trên server, mới nhất trước.
 * Gồm audit AdminService ghi trực tiếp và audit service khác gửi qua sự kiện AuditRecorded (T-802). Chỉ đọc: không có sửa / xóa.
 */
export const AuditLogListPage: React.FC = () => {
  const toast = useAdminToast();
  const params = useListParams();
  const page = params.page;
  const [exporting, setExporting] = useState(false);

  const fromDay = params.get('from');
  const toDay = params.get('to');
  const filter: AuditLogListQuery = {
    from: ictDayStartUtc(fromDay),
    to: ictDayStartUtc(toDay, 1),
    sourceService: params.get('service'),
    action: params.get('action'),
    entityName: params.get('entity'),
    entityId: params.get('entityId'),
    userId: positiveInt(params.get('userId')),
    ownerProfileId: positiveInt(params.get('ownerProfileId')),
    q: params.get('q'),
  };
  const filterKey = JSON.stringify(filter);
  const previewId = positiveInt(params.get('log'));
  // Mở / đóng slide-over không được đưa danh sách về trang 1.
  const setPreview = (id: number | null) => params.set({ log: id, page: page <= 1 ? null : page });

  const options = useAdminQuery(() => adminAuditLogsApi.filterOptions(), []);
  const query = useAdminQuery(
    () => adminAuditLogsApi.list(filter, page, DEFAULT_PAGE_SIZE),
    [filterKey, page],
    (d) => d.items.length === 0
  );
  const total = query.data?.totalCount ?? null;
  const facets = options.data;
  const hasFilter = Object.values(filter).some((v) => v !== null && v !== undefined && v !== '');
  const linkedChips = LINKED_FILTERS.filter((f) => params.get(f.param) !== '');

  const exportCsv = async () => {
    setExporting(true);
    try {
      saveFile(await adminAuditLogsApi.export(filter), 'audit-logs.csv');
      toast.show({ type: 'success', title: 'Export ready', message: 'The CSV uses the filters shown on screen (max 10,000 rows).' });
    } catch (err: unknown) {
      toast.show({ type: 'error', title: 'Export failed', message: err instanceof Error ? err.message : String(err) });
    } finally {
      setExporting(false);
    }
  };

  return (
    <ListScreen
      title="Audit Log"
      subtitle={
        total === null
          ? 'AuditLogs · append-only'
          : `${formatNumber(total)} entries · AdminService + events from other services · append-only · newest first`
      }
      actions={
        <AdminButton variant="onNavy" icon="download" onClick={exportCsv} disabled={exporting || query.status !== 'ready'}>
          {exporting ? 'Exporting…' : 'Export CSV'}
        </AdminButton>
      }
      filters={
        <FilterColumn>
          <FilterPanel
            title="Source service"
            value={filter.sourceService ?? ''}
            onChange={(v) => params.set({ service: v || null })}
            options={[
              { value: '', label: 'All', count: facets?.totalCount },
              ...(facets?.sourceServices ?? []).map((f) => ({ value: f.value, label: f.value, count: f.count })),
            ]}
          />
        </FilterColumn>
      }
      toolbar={
        <ToolbarRow>
          <SearchBox
            placeholder="Search action, entity, reason, user ID…"
            width={320}
            value={params.get('q')}
            onSearch={(q) => params.set({ q: q || null })}
          />
          <ToolbarSelect
            label="Action"
            width={230}
            value={filter.action ?? ''}
            options={facetOptions(facets?.actions)}
            onChange={(v) => params.set({ action: v || null })}
          />
          <ToolbarSelect
            label="Entity"
            width={190}
            value={filter.entityName ?? ''}
            options={facetOptions(facets?.entityNames)}
            onChange={(v) => params.set({ entity: v || null })}
          />
          <DayBox label="From" value={fromDay} onChange={(v) => params.set({ from: v || null })} />
          <DayBox label="To" value={toDay} onChange={(v) => params.set({ to: v || null })} />
          {linkedChips.map((f) => (
            <FilterChip key={f.param} label={f.label(params.get(f.param))} onRemove={() => params.set({ [f.param]: null })} />
          ))}
          {hasFilter && (
            <AdminButton
              variant="ghost"
              className="adm-motion-fade"
              onClick={() =>
                params.set({ service: null, action: null, entity: null, entityId: null, userId: null, ownerProfileId: null, from: null, to: null, q: null })
              }
            >
              Clear filters
            </AdminButton>
          )}
        </ToolbarRow>
      }
    >
      <DataTable
        animateRows
        busy={query.isFetching && query.data !== null}
        columns={columns}
        rows={query.data?.items ?? []}
        rowKey={(r) => r.id}
        selectedKey={previewId}
        onRowClick={(r) => setPreview(r.id)}
        placeholder={
          <QueryState
            status={query.status}
            error={query.error}
            onRetry={query.refetch}
            copy={{
              emptyTitle: 'No audit entries',
              emptyMessage: hasFilter ? 'No entry matches these filters.' : 'Nothing has been recorded yet.',
              errorTitle: 'Could not load audit log',
              service: 'AdminService',
            }}
          />
        }
        footer={
          query.data && <Pagination page={page} pageSize={query.data.pageSize} totalCount={query.data.totalCount} onPageChange={params.setPage} />
        }
      />
      {previewId !== null && <AuditLogPreview key={previewId} id={previewId} onClose={() => setPreview(null)} />}
    </ListScreen>
  );
};
