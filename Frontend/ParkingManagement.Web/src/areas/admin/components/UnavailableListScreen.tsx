import React, { useState } from 'react';
import { useNavigate } from 'react-router-dom';
import { ListScreen } from './PageLayout';
import { FilterColumn, FilterPanel } from './FilterColumn';
import { SearchBox, ToolbarRow, ToolbarSelect } from './Toolbar';
import { DataTable } from './DataTable';
import { StateBlock } from './StateBlock';
import { Banner, type BannerTone } from './Banner';
import { AdminButton } from './AdminButton';
import { SubTabs, type SubTab } from './SubTabs';
import { FormDialog, type FormDialogSpec } from './FormDialog';
import { useListParams } from '../hooks/useListParams';
import { humanizeEnum } from '../utils/enumLabels';

export interface UnavailableFilterGroup {
  /** Tên query param trên URL (giữ sẵn để nối API sau). */
  param: string;
  title: string;
  allLabel?: string;
  /** Giá trị enum SharedKernel (humanise) hoặc nhãn hiển thị. */
  values: string[];
}

export interface UnavailableAction {
  label: string;
  variant?: 'primary' | 'onNavy';
  dialog?: FormDialogSpec;
  to?: string;
}

export interface UnavailableListSpec {
  title: string;
  subtitle: string;
  banner?: { tone: BannerTone; text: string };
  subTabs?: SubTab[];
  filterGroups?: UnavailableFilterGroup[];
  searchPlaceholder?: string;
  toolbarFilters?: string[];
  columns: string[];
  service: string;
  /** Endpoint còn thiếu (ghi trong "Vấn đề mở"). */
  missingEndpoint: string;
  actions?: UnavailableAction[];
}

/**
 * Màn danh sách mà backend CHƯA có endpoint: dựng đủ layout theo Figma (title bar, banner, cột lọc, toolbar,
 * header bảng) nhưng thân bảng là EmptyState thật – tuyệt đối không hiển thị dữ liệu mẫu.
 */
export const UnavailableListScreen: React.FC<{ spec: UnavailableListSpec }> = ({ spec }) => {
  const params = useListParams();
  const navigate = useNavigate();
  const [dialog, setDialog] = useState<FormDialogSpec | null>(null);

  const filters = spec.filterGroups && spec.filterGroups.length > 0 && (
    <FilterColumn>
      {spec.filterGroups.map((g) => (
        <FilterPanel
          key={g.param}
          title={g.title}
          value={params.get(g.param)}
          onChange={(v) => params.set({ [g.param]: v || null })}
          options={[{ value: '', label: g.allLabel ?? 'All' }, ...g.values.map((v) => ({ value: v, label: humanizeEnum(v) }))]}
        />
      ))}
    </FilterColumn>
  );

  const toolbar = (spec.searchPlaceholder || spec.toolbarFilters) && (
    <ToolbarRow>
      {spec.searchPlaceholder && (
        <SearchBox placeholder={spec.searchPlaceholder} value={params.get('q')} onSearch={(q) => params.set({ q: q || null })} />
      )}
      {spec.toolbarFilters?.map((label) => (
        <ToolbarSelect key={label} label={label} value="" options={[{ value: '', label: 'All' }]} onChange={() => undefined} disabled disabledReason="No data source yet" />
      ))}
    </ToolbarRow>
  );

  const actions = spec.actions?.map((a) => (
    <AdminButton
      key={a.label}
      variant={a.variant ?? 'primary'}
      onClick={() => (a.dialog ? setDialog(a.dialog) : a.to ? navigate(a.to) : undefined)}
    >
      {a.label}
    </AdminButton>
  ));

  return (
    <ListScreen
      title={spec.title}
      subtitle={spec.subtitle}
      actions={actions && actions.length > 0 ? actions : undefined}
      top={
        <>
          {spec.subTabs && <SubTabs tabs={spec.subTabs} />}
          {spec.banner && <Banner tone={spec.banner.tone}>{spec.banner.text}</Banner>}
        </>
      }
      filters={filters || undefined}
      toolbar={toolbar || undefined}
    >
      <DataTable
        columns={spec.columns.map((header, i) => ({ key: `${i}-${header}`, header, render: () => null }))}
        rows={[]}
        rowKey={() => 0}
        placeholder={
          <StateBlock
            type="unavailable"
            bare
            message={`${spec.service} has no endpoint for this screen yet (${spec.missingEndpoint}). Nothing is shown instead of sample data.`}
          />
        }
      />
      {dialog && <FormDialog spec={dialog} open onClose={() => setDialog(null)} />}
    </ListScreen>
  );
};
