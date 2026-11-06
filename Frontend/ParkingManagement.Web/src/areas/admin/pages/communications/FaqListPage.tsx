import React, { useMemo, useState } from 'react';
import { adminCommunicationsApi } from '@/api/adminCommunicationsApi';
import type { FaqAudience, FaqItemDto } from '@/types/adminSupport';
import { ListScreen, QueryState } from '../../components/PageLayout';
import { FilterColumn, FilterPanel } from '../../components/FilterColumn';
import { AdminButton } from '../../components/AdminButton';
import { CellAmount, CellStatus, CellText, DataTable } from '../../components/DataTable';
import { FormDialog } from '../../components/FormDialog';
import { useAdminQuery } from '../../hooks/useAdminQuery';
import { useListParams } from '../../hooks/useListParams';
import { humanizeEnum } from '../../utils/enumLabels';
import { formatNumber } from '../../utils/format';
import { createFaqDialog } from '../unavailableScreens';

const AUDIENCES: Array<{ value: FaqAudience; label: string }> = [
  { value: 'Driver', label: 'Driver' },
  { value: 'LotOwner', label: 'Lot owner' },
];

/**
 * Communications / FAQ – GET /api/v1/faqs?audience= (SupportService, chỉ bài đã công khai).
 * Audience "All" = gộp Driver + LotOwner (mỗi bài chỉ hiện một lần). Tạo/sửa bài chưa có endpoint.
 */
export const FaqListPage: React.FC = () => {
  const params = useListParams();
  const audienceParam = params.get('audience');
  const audience = AUDIENCES.some((a) => a.value === audienceParam) ? (audienceParam as FaqAudience) : null;
  const [createOpen, setCreateOpen] = useState(false);

  const query = useAdminQuery(
    async () => {
      const targets: FaqAudience[] = audience ? [audience] : ['Driver', 'LotOwner'];
      const results = await Promise.all(targets.map((a) => adminCommunicationsApi.getFaqs(a)));
      const byId = new Map<number, FaqItemDto>();
      results.forEach((r) => r.categories.forEach((c) => c.items.forEach((i) => byId.set(i.id, i))));
      return [...byId.values()].sort((a, b) => a.category.localeCompare(b.category) || a.sortOrder - b.sortOrder);
    },
    [audience],
    (d) => d.length === 0
  );
  const items = useMemo(() => query.data ?? [], [query.data]);

  return (
    <ListScreen
      title="FAQ"
      subtitle={query.data ? `FaqArticles · ${formatNumber(items.length)} published · US-092` : 'FaqArticles · US-092'}
      actions={<AdminButton onClick={() => setCreateOpen(true)}>Create article</AdminButton>}
      filters={
        <FilterColumn>
          <FilterPanel
            title="Audience"
            value={audience ?? ''}
            onChange={(v) => params.set({ audience: v || null })}
            options={[{ value: '', label: 'All', count: !audience && query.data ? items.length : undefined }, ...AUDIENCES.map((a) => ({ ...a, count: audience === a.value && query.data ? items.length : undefined }))]}
          />
        </FilterColumn>
      }
    >
      <DataTable
        rows={query.status === 'ready' ? items : []}
        rowKey={(r) => r.id}
        columns={[
          { key: 'cat', header: 'Category', width: 120, render: (r) => <CellText>{r.category}</CellText> },
          { key: 'q', header: 'Question', render: (r) => <CellText>{r.question}</CellText> },
          { key: 'aud', header: 'Audience', width: 110, render: (r) => <CellText>{humanizeEnum(r.audience)}</CellText> },
          { key: 'order', header: 'Order', width: 70, render: (r) => <CellText>{r.sortOrder}</CellText> },
          { key: 'pub', header: 'Published', width: 110, render: () => <CellStatus value="Published" /> },
          { key: 'views', header: 'Views', width: 80, align: 'right', render: (r) => <CellAmount>{formatNumber(r.viewCount)}</CellAmount> },
        ]}
        placeholder={
          <QueryState
            status={query.status}
            error={query.error}
            onRetry={query.refetch}
            copy={{ emptyTitle: 'No FAQ articles', emptyMessage: 'Create the first article for drivers or owners.', errorTitle: 'Could not load FAQ articles', service: 'SupportService' }}
          />
        }
      />
      {createOpen && <FormDialog spec={createFaqDialog} open onClose={() => setCreateOpen(false)} unavailableNote="No create endpoint in SupportService yet (only GET /api/v1/faqs)." />}
    </ListScreen>
  );
};
