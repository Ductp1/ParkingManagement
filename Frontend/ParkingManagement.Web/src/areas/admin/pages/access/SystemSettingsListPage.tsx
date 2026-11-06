import React, { useMemo } from 'react';
import { useNavigate } from 'react-router-dom';
import { adminSettingsApi } from '@/api/adminSettingsApi';
import { SystemConfigChangeStatus } from '@/types/adminEnums';
import type { SystemConfigChangeDto, SystemConfigDto } from '@/types/adminSettings';
import { ListScreen, QueryState } from '../../components/PageLayout';
import { Banner } from '../../components/Banner';
import { SearchBox, ToolbarRow } from '../../components/Toolbar';
import { CellMono, CellMuted, CellText, CellView, DataTable, type DataTableColumn } from '../../components/DataTable';
import { useAdminQuery } from '../../hooks/useAdminQuery';
import { useListParams } from '../../hooks/useListParams';
import { formatDate, formatNumber } from '../../utils/format';

const BASE = '/admin/access/system-settings';

/** Thay đổi sắp tới của mỗi key: lấy trang 1 history, chọn bản Scheduled sớm nhất. */
const useNextChanges = (keys: string[]) =>
  useAdminQuery(
    keys.length === 0
      ? null
      : async () => {
          const results = await Promise.allSettled(keys.map((k) => adminSettingsApi.getHistory(k, 1, 20)));
          const map: Record<string, SystemConfigChangeDto | null> = {};
          results.forEach((r, i) => {
            map[keys[i]] =
              r.status === 'fulfilled'
                ? r.value.items
                    .filter((c) => c.status === SystemConfigChangeStatus.Scheduled)
                    .sort((a, b) => a.effectiveFromUtc.localeCompare(b.effectiveFromUtc))[0] ?? null
                : null;
          });
          return map;
        },
    [keys.join('|')]
  );

/** Access & Configuration / System Settings / List (US-098) – GET /api/v1/admin/settings. */
export const SystemSettingsListPage: React.FC = () => {
  const navigate = useNavigate();
  const params = useListParams();
  const search = params.get('q').toLowerCase();
  const query = useAdminQuery(() => adminSettingsApi.getAll(), [], (d) => d.configs.length === 0);
  const configs = useMemo(() => query.data?.configs ?? [], [query.data]);
  const next = useNextChanges(useMemo(() => configs.map((c) => c.key), [configs]));

  const rows = configs.filter((c) => search === '' || c.key.toLowerCase().includes(search) || (c.description ?? '').toLowerCase().includes(search));

  const columns: DataTableColumn<SystemConfigDto>[] = [
    { key: 'key', header: 'Key', width: 260, render: (r) => <CellMono>{r.key}</CellMono> },
    { key: 'value', header: 'Value in effect', width: 140, render: (r) => <CellText>{r.value}</CellText> },
    { key: 'type', header: 'Type', width: 80, render: (r) => <CellText>{r.dataType}</CellText> },
    { key: 'desc', header: 'Description', render: (r) => <CellMuted>{r.description ?? '—'}</CellMuted> },
    {
      key: 'next',
      header: 'Next change',
      width: 170,
      render: (r) => {
        const change = next.data?.[r.key];
        return <CellText>{next.status === 'loading' ? '…' : change ? `${change.value} from ${formatDate(change.effectiveFromUtc)}` : '—'}</CellText>;
      },
    },
    { key: 'actions', header: '', width: 74, render: (r) => <CellView to={`${BASE}/${encodeURIComponent(r.key)}`} /> },
  ];

  return (
    <ListScreen
      title="System Settings"
      subtitle={query.data ? `${formatNumber(configs.length)} parameters · values in effect now (ICT)` : '—'}
      top={
        <Banner tone="info">
          SystemConfigs (Key, Value, DataType, Description) + SystemConfigChanges (scheduled changes with effective date). The default value is never overwritten.
        </Banner>
      }
      toolbar={
        <ToolbarRow>
          <SearchBox placeholder="Search key or description…" width={360} value={params.get('q')} onSearch={(q) => params.set({ q: q || null })} />
        </ToolbarRow>
      }
    >
      <DataTable
        columns={columns}
        rows={query.status === 'ready' ? rows : []}
        rowKey={(r) => r.key}
        onRowClick={(r) => navigate(`${BASE}/${encodeURIComponent(r.key)}`)}
        placeholder={
          <QueryState
            status={query.status === 'ready' && rows.length === 0 ? 'empty' : query.status}
            error={query.error}
            onRetry={query.refetch}
            copy={{ emptyTitle: 'No parameters', emptyMessage: 'No key or description matches the search.', errorTitle: 'Could not load settings', service: 'AdminService' }}
          />
        }
      />
    </ListScreen>
  );
};
